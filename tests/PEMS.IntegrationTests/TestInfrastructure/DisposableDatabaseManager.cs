using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Creates the throw-away MySQL database the integration suite runs against, imports the verified
/// canonical schema into it, and proves the result before any test touches it.
///
/// Everything is fail-closed. A missing/renamed script, a hash mismatch, an ambiguous candidate, a
/// statement that could still reach a real database, or a post-import invariant that does not hold all
/// abort the run with a specific message — the suite must never "pass" against an empty or partial schema.
/// </summary>
public static class DisposableDatabaseManager
{
    private static readonly object _lock = new();
    private static string? _disposableConnectionString;
    private static string? _disposableDbName;

    /// <summary>Number of persistent base tables the canonical schema must produce.</summary>
    /// <remarks>
    /// 82 since the P0 account-email-confirmation work added <c>account_email_confirmations</c>; the constant
    /// had been left at 81, which made every disposable import abort before a single test ran.
    /// </remarks>
    public const int ExpectedBaseTableCount = 82;

    /// <summary>Number of triggers the canonical schema must produce.</summary>
    public const int ExpectedTriggerCount = 32;

    public static string GetDisposableConnectionString(string originalConnectionString)
    {
        if (_disposableConnectionString != null)
            return _disposableConnectionString;

        lock (_lock)
        {
            if (_disposableConnectionString != null)
                return _disposableConnectionString;

            // Verify the schema BEFORE creating anything, so a bad script never leaves a stray database.
            var sql = CanonicalSqlScript.ReadVerified();

            var dbName = CanonicalSqlScript.NewDisposableDatabaseName();
            var masterConnStr = ToServerConnectionString(originalConnectionString);

            var created = false;
            try
            {
                using (var conn = new MySqlConnection(masterConnStr))
                {
                    conn.Open();

                    Execute(conn, $"CREATE DATABASE `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
                    created = true;

                    // Rewrites every database-selection statement and re-scans the result (throws if unsafe).
                    var retargeted = CanonicalSqlScript.Retarget(sql, dbName);

                    var script = new MySqlScript(conn, retargeted);
                    script.Execute();

                    AssertSchemaImported(conn, dbName);
                }
            }
            catch
            {
                // Never leak a half-built database when import or verification fails.
                if (created)
                {
                    try { DropDisposableDatabase(originalConnectionString, dbName); }
                    catch { /* the original failure is the one worth surfacing */ }
                }

                throw;
            }

            _disposableDbName = dbName;
            _disposableConnectionString = Regex.Replace(
                originalConnectionString, @"database=[^;]+;", $"database={dbName};", RegexOptions.IgnoreCase);

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try { DropDisposableDatabase(masterConnStr, dbName); }
                catch { /* best-effort emergency cleanup */ }
            };

            return _disposableConnectionString;
        }
    }

    /// <summary>The disposable database created for this run, or null when none has been created yet.</summary>
    public static string? CurrentDatabaseName => _disposableDbName;

    /// <summary>
    /// Verifies the imported schema really is the canonical Pure V2 one. Any mismatch throws, because a
    /// green test run against a wrong schema is worse than a red one.
    /// </summary>
    public static void AssertSchemaImported(MySqlConnection conn, string expectedDatabase)
    {
        var actualDb = ScalarString(conn, $"SELECT DATABASE() FROM DUAL;", expectedDatabase);
        if (!string.Equals(actualDb, expectedDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Import targeted '{actualDb}' instead of the disposable database '{expectedDatabase}'.");

        var failures = new List<string>();

        void Expect(string label, string sql, long expected)
        {
            var actual = ScalarLong(conn, sql, expectedDatabase);
            if (actual != expected)
                failures.Add($"{label}: expected {expected}, found {actual}");
        }

        Expect("base tables",
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_type='BASE TABLE';",
            ExpectedBaseTableCount);

        Expect("views",
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_type='VIEW';",
            0);

        Expect("seed helper objects (pems_seed_*)",
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND LEFT(table_name,10)='pems_seed_';",
            0);

        Expect("triggers",
            "SELECT COUNT(*) FROM information_schema.triggers WHERE trigger_schema=DATABASE();",
            ExpectedTriggerCount);

        // Pure V2: the form-version discriminator must not exist on EITHER table.
        Expect("form_schema_version columns",
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() " +
            "AND column_name='form_schema_version';",
            0);

        // Pure V2: none of the 10 legacy global-form columns may exist on visit_requests.
        Expect("legacy global-form columns on visit_requests",
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() " +
            "AND table_name='visit_requests' AND column_name IN ('delegation_name','visit_type'," +
            "'visit_type_other','purpose','working_content','working_language','transportation_note'," +
            "'media_consent_status','media_consent_note','note_to_fptu');",
            0);

        // Every campus instance owns exactly one form detail.
        Expect("campus instances missing a form detail",
            "SELECT COUNT(*) FROM visit_request_campuses c " +
            "LEFT JOIN visit_instance_form_details d ON d.visit_instance_id=c.visit_instance_id " +
            "WHERE d.visit_instance_id IS NULL;",
            0);

        Expect("orphan form details",
            "SELECT COUNT(*) FROM visit_instance_form_details d " +
            "LEFT JOIN visit_request_campuses c ON c.visit_instance_id=d.visit_instance_id " +
            "WHERE c.visit_instance_id IS NULL;",
            0);

        Expect("requests without a campus instance",
            "SELECT COUNT(*) FROM visit_requests r " +
            "LEFT JOIN visit_request_campuses c ON c.visit_request_id=r.visit_request_id " +
            "WHERE c.visit_request_id IS NULL;",
            0);

        if (failures.Count > 0)
            throw new InvalidOperationException(
                $"Canonical schema import verification failed for '{expectedDatabase}':{Environment.NewLine}  " +
                string.Join(Environment.NewLine + "  ", failures));
    }

    public static void DropDisposableDatabase(string originalConnectionString, string dbName)
    {
        if (string.IsNullOrWhiteSpace(dbName) || !CanonicalSqlScript.DisposableNamePattern.IsMatch(dbName))
            throw new InvalidOperationException(
                $"Attempted to drop a database with an invalid or protected name: {dbName}");

        using var conn = new MySqlConnection(ToServerConnectionString(originalConnectionString));
        conn.Open();
        Execute(conn, $"DROP DATABASE IF EXISTS `{dbName}`;");
    }

    /// <summary>Strips database/GuidFormat so the connection targets the server, not a specific schema.</summary>
    private static string ToServerConnectionString(string connectionString)
    {
        var s = Regex.Replace(connectionString, @"database=[^;]+;?", "", RegexOptions.IgnoreCase);
        return Regex.Replace(s, @"GuidFormat=[^;]+;?", "", RegexOptions.IgnoreCase);
    }

    private static void Execute(MySqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static long ScalarLong(MySqlConnection conn, string sql, string database)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"USE `{database}`; {sql}";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static string ScalarString(MySqlConnection conn, string sql, string database)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"USE `{database}`; {sql}";
        return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
    }
}
