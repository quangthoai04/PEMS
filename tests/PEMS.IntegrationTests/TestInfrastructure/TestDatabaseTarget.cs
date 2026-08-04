using System;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Decides, and proves, WHICH database a test connection points at.
///
/// <para>
/// <see cref="CanonicalSqlScript"/> already guards the SCRIPT: every database-selection statement is
/// rewritten onto the disposable target and the produced text is re-scanned before a byte reaches the
/// server. What it never guarded is the CONNECTION — and a connection carries a default schema of its
/// own, which is what every statement before the first <c>USE</c> runs against.
/// </para>
///
/// <para>
/// Both places that built one did it by regular expression, replacing <c>database=[^;]+;</c>. That
/// pattern requires a TRAILING SEMICOLON, so a connection string ending
/// <c>...;database=pems_db</c> matches nothing and the replace returns the string unchanged — a
/// "disposable" connection still pointed at the real database, reported as success. The same pattern
/// does not know <c>Initial Catalog</c>, which MySql.Data accepts as a synonym for <c>Database</c>, so
/// that spelling survives every rewrite too, including the one that is supposed to strip the schema off
/// the server connection.
/// </para>
///
/// <para>
/// This class replaces the regular expressions with the driver's own parser and adds the check the
/// rewrite never had: the result is compared against a protected list, and a connection that would open
/// on a real database is refused rather than used.
/// </para>
/// </summary>
public static class TestDatabaseTarget
{
    /// <summary>
    /// Databases a test may never create, drop, import into or connect to.
    ///
    /// <para>
    /// <c>pems_db</c> is first because it is the one the canonical script names in its own
    /// <c>CREATE DATABASE IF NOT EXISTS</c> / <c>USE</c> header — the script is a deployment artefact
    /// and targets the deployment. Importing it "into a scratch database" without retargeting rebuilds
    /// the developer's working database instead: it opens with 87 <c>DROP TABLE</c>s.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<string> ProtectedDatabases = new[]
    {
        "pems_db",
        "pems",
        "pems_prod",
        "pems_production",
        "mysql",
        "information_schema",
        "performance_schema",
        "sys",
    };

    public static bool IsProtected(string? database) =>
        !string.IsNullOrWhiteSpace(database) &&
        ProtectedDatabases.Any(p => string.Equals(p, database.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The keys MySQL drivers accept for "which schema", normalised for comparison.</summary>
    private static readonly string[] DatabaseKeys = { "database", "initialcatalog" };

    private static string NormalizeKey(string key) =>
        new string(key.Where(c => !char.IsWhiteSpace(c) && c != '_').ToArray()).ToLowerInvariant();

    /// <summary>
    /// Splits a connection string into its key/value pairs, PRESERVING every key it does not recognise.
    ///
    /// <para>
    /// Written by hand rather than delegating to <c>MySqlConnectionStringBuilder</c>, and the reason is
    /// concrete: these connection strings carry <c>GuidFormat</c>, which belongs to Pomelo, and
    /// MySql.Data's builder REJECTS an unknown key outright. Round-tripping a Pomelo connection string
    /// through it therefore throws — which is how the first version of this guard broke every suite
    /// whose connection string carried the option, reporting the database as unreachable.
    /// </para>
    /// <para>
    /// A tolerant parser is also the honest tool for the job. The question here is only ever "which
    /// schema does this name", and answering it must never depend on understanding the other keys.
    /// </para>
    /// </summary>
    private static List<KeyValuePair<string, string>> Split(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Refusing to use an empty connection string.");

        var pairs = new List<KeyValuePair<string, string>>();

        foreach (var part in connectionString.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;

            var eq = part.IndexOf('=');
            if (eq < 0)
                throw new InvalidOperationException(
                    $"Refusing to use a connection string with the unparseable segment '{part.Trim()}' — " +
                    "a partially understood string is how a rewrite silently leaves the original " +
                    "database in place.");

            pairs.Add(new KeyValuePair<string, string>(part[..eq].Trim(), part[(eq + 1)..].Trim()));
        }

        if (pairs.Count == 0)
            throw new InvalidOperationException("Refusing to use a connection string with no settings.");

        return pairs;
    }

    private static string Join(IEnumerable<KeyValuePair<string, string>> pairs) =>
        string.Join(";", pairs.Select(p => $"{p.Key}={p.Value}")) + ";";

    /// <summary>The database a connection string would open on, or an empty string when it names none.</summary>
    public static string DatabaseOf(string connectionString) =>
        Split(connectionString)
            .Where(p => DatabaseKeys.Contains(NormalizeKey(p.Key)))
            .Select(p => p.Value)
            .LastOrDefault() ?? "";

    /// <summary>
    /// A connection to the SERVER, carrying no default schema at all.
    ///
    /// <para>
    /// Clearing it matters more than it looks: with a default schema, any statement issued before the
    /// first <c>USE</c> — and every statement in a script whose <c>USE</c> was stripped rather than
    /// rewritten — lands in that schema. Empty is the only value that cannot land anywhere.
    /// </para>
    /// </summary>
    public static string ForServer(string connectionString)
    {
        // GuidFormat is Pomelo's; MySql.Data rejects the key outright, and the server connection is the
        // one MySqlScript opens — so this is the one place the option must be dropped rather than kept.
        var kept = Split(connectionString)
            .Where(p => !DatabaseKeys.Contains(NormalizeKey(p.Key)))
            .Where(p => NormalizeKey(p.Key) != "guidformat")
            .ToList();

        var result = Join(kept);

        var leftover = DatabaseOf(result);
        if (!string.IsNullOrEmpty(leftover))
            throw new InvalidOperationException(
                $"Refusing to open a server connection that still names a database ('{leftover}').");

        return result;
    }

    /// <summary>
    /// The same connection string, retargeted onto a disposable database, having proved the result.
    ///
    /// <para>
    /// The target must match the disposable name pattern, must not be protected, and — the check the
    /// regular expression could not make — the STRING PRODUCED is re-parsed and its database compared to
    /// the one asked for. A rewrite that quietly did nothing fails here instead of running.
    /// </para>
    /// </summary>
    public static string ForDisposable(string connectionString, string disposableDatabase)
    {
        if (!CanonicalSqlScript.DisposableNamePattern.IsMatch(disposableDatabase))
            throw new InvalidOperationException(
                $"Refusing to target '{disposableDatabase}': it is not a disposable " +
                "pems_test_run_<32hex> name.");

        if (IsProtected(disposableDatabase))
            throw new InvalidOperationException(
                $"Refusing to target the protected database '{disposableDatabase}'.");

        // Every other key is carried through untouched — including Pomelo's, because this string is the
        // one Pomelo opens.
        var pairs = Split(connectionString);
        var replaced = false;

        for (var i = 0; i < pairs.Count; i++)
        {
            if (!DatabaseKeys.Contains(NormalizeKey(pairs[i].Key))) continue;
            pairs[i] = new KeyValuePair<string, string>(pairs[i].Key, disposableDatabase);
            replaced = true;
        }

        if (!replaced)
            pairs.Add(new KeyValuePair<string, string>("database", disposableDatabase));

        var result = Join(pairs);

        var actual = DatabaseOf(result);
        if (!string.Equals(actual, disposableDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Retargeting produced a connection string pointing at '{actual}' rather than " +
                $"'{disposableDatabase}'. The rewrite did not take effect.");

        return result;
    }

    /// <summary>
    /// Refuses a connection string that would open on a protected database. Call this before opening,
    /// never after: the point is that the statement is not issued.
    /// </summary>
    public static void AssertNotProtected(string connectionString, string what)
    {
        var database = DatabaseOf(connectionString);

        if (IsProtected(database))
            throw new InvalidOperationException(
                $"Refusing {what}: the connection targets the protected database '{database}'. " +
                "Integration tests run against a disposable pems_test_run_<32hex> database created for " +
                "the run; a protected name here means a retarget did not take effect.");
    }

    /// <summary>
    /// Asks the SERVER which database a connection actually landed on, and refuses a protected answer.
    ///
    /// <para>
    /// The string-level check can be defeated by anything that changes the schema after connecting — a
    /// <c>USE</c> the retarget missed, a stored routine, a pooled connection handed back dirty. This one
    /// cannot: it is the server's own answer, taken on the live connection.
    /// </para>
    /// </summary>
    public static void AssertConnectedDatabaseIsNotProtected(MySqlConnection connection, string what)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT DATABASE();";
        var database = cmd.ExecuteScalar()?.ToString() ?? "";

        if (IsProtected(database))
            throw new InvalidOperationException(
                $"Refusing {what}: the OPEN connection is on the protected database '{database}'.");
    }
}
