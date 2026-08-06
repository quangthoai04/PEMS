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
    /// 83 since G11 added <c>email_send_idempotency</c> (R-103).
    /// 84 since <c>email_contact_policies</c> — added to the script with the contact-block work, but
    /// only reached by an import now that the statement splitting the catalog INSERT is repaired. The
    /// table had been in the script all along; every import simply stopped at ERROR 1064 before
    /// creating it, which is why this constant still agreed with reality while it was wrong.
    /// 81 since the three <c>email_draft*</c> tables were dropped: the composer holds a message in the
    /// browser until it is sent, so there is no half-written record on the server to store.
    /// </remarks>
    public const int ExpectedBaseTableCount = 81;

    /// <summary>
    /// Number of triggers the canonical schema must produce.
    /// 33 since the per-campus operational-contact cutover: the three request-level primary-contact
    /// guards (<c>trg_visit_requests_primary_contact_guard_bi</c>/<c>_bu</c>,
    /// <c>trg_users_protect_active_primary_contact_bu</c>) were replaced by four per-campus ones
    /// (<c>trg_visit_campuses_op_contact_guard_bi</c>/<c>_bu</c>,
    /// <c>trg_visit_requests_contact_gate_guard_bu</c>, <c>trg_users_protect_operational_contact_bu</c>).
    /// Net +1, measured against a fresh import.
    /// </summary>
    public const int ExpectedTriggerCount = 33;

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

                    // The server's own answer, on the live connection: no default schema means no
                    // statement can land in a real database before the script's retargeted USE runs.
                    TestDatabaseTarget.AssertConnectedDatabaseIsNotProtected(conn, "the canonical import");

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
            _disposableConnectionString = TestDatabaseTarget.ForDisposable(originalConnectionString, dbName);

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
    /// A database of its own, imported from the canonical script and dropped when the returned handle is
    /// disposed. Nothing else in the run can see it.
    ///
    /// <para>
    /// The shared disposable database is not a pristine copy of the seed for long: suites edit template
    /// content, borrow rows and put them back, and one that fails part way can leave an edit behind. A
    /// test that means to compare the SHIPPED defaults against the CANONICAL seed must not read that —
    /// it would be comparing against whatever ran before it, which is how the defaults-parity check came
    /// to pass alone and fail in a full run, reporting a drift in the seed that did not exist.
    /// </para>
    /// </summary>
    public static PristineCanonicalDatabase CreatePristineDatabase(string originalConnectionString)
    {
        var sql = CanonicalSqlScript.ReadVerified();
        var dbName = CanonicalSqlScript.NewDisposableDatabaseName();
        var masterConnStr = ToServerConnectionString(originalConnectionString);
        var created = false;

        try
        {
            using var conn = new MySqlConnection(masterConnStr);
            conn.Open();

            TestDatabaseTarget.AssertConnectedDatabaseIsNotProtected(conn, "the pristine canonical import");

            Execute(conn, $"CREATE DATABASE `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
            created = true;

            new MySqlScript(conn, CanonicalSqlScript.Retarget(sql, dbName)).Execute();
            AssertSchemaImported(conn, dbName);
        }
        catch
        {
            if (created)
            {
                try { DropDisposableDatabase(originalConnectionString, dbName); }
                catch { /* the original failure is the one worth surfacing */ }
            }

            throw;
        }

        var connectionString = TestDatabaseTarget.ForDisposable(originalConnectionString, dbName);

        return new PristineCanonicalDatabase(dbName, connectionString, originalConnectionString);
    }

    /// <summary>A private canonical database, dropped on dispose.</summary>
    public sealed class PristineCanonicalDatabase : IDisposable
    {
        private readonly string _original;

        internal PristineCanonicalDatabase(string name, string connectionString, string original)
        {
            Name = name;
            ConnectionString = connectionString;
            _original = original;
        }

        public string Name { get; }
        public string ConnectionString { get; }

        public void Dispose()
        {
            try { DropDisposableDatabase(_original, Name); }
            catch { /* a leftover throw-away database must never fail a test run */ }
        }
    }

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

    /// <summary>
    /// Strips database/GuidFormat so the connection targets the server, not a specific schema.
    ///
    /// <para>
    /// This used to be two <c>Regex.Replace</c> calls. Both required a trailing semicolon
    /// (<c>database=[^;]+;?</c> made it optional, but only by also matching to end-of-string, which the
    /// sibling rewrite did not), and neither knew <c>Initial Catalog</c> — the synonym MySql.Data accepts
    /// for the same key. A connection string spelled either way kept its schema through the strip.
    /// <see cref="TestDatabaseTarget.ForServer"/> parses with the driver and then re-reads its own output
    /// to prove the schema is gone.
    /// </para>
    /// </summary>
    private static string ToServerConnectionString(string connectionString)
        => TestDatabaseTarget.ForServer(connectionString);

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
