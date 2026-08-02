using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using MySql.Data.MySqlClient;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Database;

/// <summary>
/// The primary-contact guards, exercised from the application's own MySQL client rather than from
/// inside the import script (G12 / R-DB-CONTACT-GUARD).
///
/// <para>
/// This class exists because the canonical import's self-test was, for a long time, wrong about its
/// own subject. It reported <c>contact_guard_negative_failures = 14</c> — every negative case — while
/// the triggers were in fact rejecting all fourteen. The cause was that each handler ran
/// <c>SET v_raised = TRUE;</c> before <c>GET DIAGNOSTICS CONDITION 1</c>; MySQL clears the diagnostics
/// area on the first successful statement inside a handler, so the read came back NULL and the
/// comparison against '45000' evaluated to UNKNOWN. A self-test that can be wrong in that direction —
/// reporting failure where the database is sound — can equally be wrong in the other, so the guards
/// are asserted here too, through a different mechanism, where an exception is an exception.
/// </para>
///
/// <para>
/// Every mutation runs inside a transaction that is rolled back, so the database this class imports is
/// left exactly as the canonical script produced it. What is asserted is not merely "the write was
/// refused" but the SQLSTATE and the stable message: callers depend on those codes, and a guard that
/// rejects with an unrelated storage error is a guard whose failure nobody can act on.
/// </para>
/// </summary>
public sealed class ContactGuardTests : IClassFixture<ContactGuardTests.GuardDatabase>
{
    private readonly GuardDatabase _db;

    public ContactGuardTests(GuardDatabase db)
    {
        _db = db;
        _db.Require();
    }

    // ── Fixture ─────────────────────────────────────────────────────────────────────────────────

    public sealed class GuardDatabase : IDisposable
    {
        private const string BaseConnection =
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True";

        public string? DatabaseName { get; private set; }
        public string ConnectionString { get; private set; } = "";
        public string? Failure { get; private set; }

        /// <summary>A user who is an ACTIVE VISITOR and is the primary contact of a live request.</summary>
        public long LinkedVisitorUserId { get; private set; }

        /// <summary>That live request.</summary>
        public long LiveRequestId { get; private set; }

        /// <summary>A user who is NOT a VISITOR — any internal account will do.</summary>
        public long InternalUserId { get; private set; }

        /// <summary>The VISITOR role's id, resolved rather than assumed.</summary>
        public long VisitorRoleId { get; private set; }

        public GuardDatabase()
        {
            try
            {
                var name = CanonicalSqlScript.NewDisposableDatabaseName();
                var server = System.Text.RegularExpressions.Regex.Replace(
                    BaseConnection, @"database=[^;]+;", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                using (var conn = new MySqlConnection(server))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"CREATE DATABASE `{name}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                        cmd.ExecuteNonQuery();
                    }

                    var retargeted = CanonicalSqlScript.Retarget(CanonicalSqlScript.ReadVerified(), name);
                    using (var cmd = conn.CreateCommand()) { cmd.CommandText = $"USE `{name}`;"; cmd.ExecuteNonQuery(); }
                    new MySqlScript(conn, retargeted).Execute();
                }

                DatabaseName = name;
                ConnectionString = System.Text.RegularExpressions.Regex.Replace(
                    BaseConnection, @"database=[^;]+;", $"database={name};",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                ResolveFixtureIds();
                CreateSqlStateProbe();
            }
            catch (Exception ex)
            {
                Failure = ex.ToString();
                Cleanup();
            }
        }

        /// <summary>
        /// Reads the ids out of the imported data instead of hard-coding them. Seed ids drift, and a
        /// guard test that silently targets a row that no longer exists passes by doing nothing.
        /// </summary>
        private void ResolveFixtureIds()
        {
            LiveRequestId = ScalarLong(@"
SELECT MIN(visit_request_id) FROM visit_requests
WHERE visitor_user_id IS NOT NULL
  AND primary_contact_access_status = 'ACTIVE'
  AND status <> 'CANCELLED'");

            LinkedVisitorUserId = ScalarLong(
                $"SELECT visitor_user_id FROM visit_requests WHERE visit_request_id = {LiveRequestId}");

            InternalUserId = ScalarLong(@"
SELECT MIN(u.user_id) FROM users u JOIN roles r ON r.role_id = u.role_id WHERE r.role_code <> 'VISITOR'");

            VisitorRoleId = ScalarLong("SELECT role_id FROM roles WHERE role_code = 'VISITOR'");
        }

        private long ScalarLong(string sql)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var value = cmd.ExecuteScalar();
            return value is null or DBNull ? 0L : Convert.ToInt64(value);
        }

        public void Require()
        {
            Assert.True(DatabaseName is not null, "Could not build the contact-guard database. " + Failure);
            Assert.True(LiveRequestId > 0, "The canonical import carries no live request with an ACTIVE primary contact.");
            Assert.True(LinkedVisitorUserId > 0, "Could not resolve the linked VISITOR.");
            Assert.True(InternalUserId > 0, "The canonical import carries no non-VISITOR account.");
            Assert.True(VisitorRoleId > 0, "The canonical import carries no VISITOR role.");
        }

        /// <summary>
        /// Runs the statements on one connection inside a transaction that is always rolled back, and
        /// returns the MySqlException the guard raised — or null when the database accepted the write.
        /// </summary>
        public MySqlException? AttemptRolledBack(params string[] statements)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                foreach (var sql in statements)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }

                return null;
            }
            catch (MySqlException ex)
            {
                return ex;
            }
            finally
            {
                tx.Rollback();
            }
        }

        public long Count(string sql) => ScalarLong(sql);

        /// <summary>
        /// Created once, before any test transaction, because CREATE PROCEDURE forces an implicit
        /// commit and would otherwise destroy the isolation the probes depend on. It executes a
        /// statement through PREPARE/EXECUTE — which are not implicitly committing — and returns the
        /// SQLSTATE the server actually raised.
        /// </summary>
        private void CreateSqlStateProbe()
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            new MySqlScript(conn, @"
DROP PROCEDURE IF EXISTS pems_guard_sqlstate_probe;
DELIMITER $$
CREATE PROCEDURE pems_guard_sqlstate_probe(IN p_sql TEXT)
BEGIN
  DECLARE v_sqlstate CHAR(5) DEFAULT NULL;
  DECLARE v_message VARCHAR(1000) DEFAULT NULL;
  DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
  BEGIN
    GET DIAGNOSTICS CONDITION 1 v_sqlstate = RETURNED_SQLSTATE, v_message = MESSAGE_TEXT;
  END;
  SET @pems_guard_probe_sql = p_sql;
  PREPARE pems_guard_probe_stmt FROM @pems_guard_probe_sql;
  EXECUTE pems_guard_probe_stmt;
  DEALLOCATE PREPARE pems_guard_probe_stmt;
  SELECT COALESCE(v_sqlstate, 'NONE') AS sqlstate_value, COALESCE(v_message, 'ACCEPTED') AS message_value;
END$$
DELIMITER ;").Execute();
        }

        /// <summary>
        /// Runs <paramref name="statements"/> in a rolled-back transaction and returns the SQLSTATE and
        /// message the LAST one produced, read on the server through GET DIAGNOSTICS.
        ///
        /// <para>
        /// This exists because MySql.Data does not populate <c>MySqlException.SqlState</c> — it reports
        /// error number 1644 and the message, and leaves SqlState null. 1644 is ER_SIGNAL_EXCEPTION, so
        /// it is the faithful client-side equivalent, but the contract these guards publish is written
        /// in terms of SQLSTATE 45000. Reading it on the server asserts the thing that was promised
        /// rather than something correlated with it.
        /// </para>
        /// </summary>
        public (string SqlState, string Message) ProbeSqlStateRolledBack(params string[] statements)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                var sqlState = "NONE";
                var message = "ACCEPTED";

                foreach (var sql in statements)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "CALL pems_guard_sqlstate_probe(@sql)";
                    cmd.Parameters.AddWithValue("@sql", sql);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        sqlState = reader.GetString(0);
                        message = reader.GetString(1);
                    }

                    reader.Close();

                    // Stop at the first refusal: later statements in a setup chain would otherwise
                    // overwrite the diagnosis with their own (often unrelated) outcome.
                    if (sqlState != "NONE") break;
                }

                return (sqlState, message);
            }
            finally
            {
                tx.Rollback();
            }
        }

        public string TriggerBody(string triggerName)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT action_statement FROM information_schema.triggers " +
                "WHERE trigger_schema = DATABASE() AND trigger_name = @n";
            cmd.Parameters.AddWithValue("@n", triggerName);
            return cmd.ExecuteScalar() as string ?? "";
        }

        public void Cleanup()
        {
            if (DatabaseName is null) return;
            try
            {
                DisposableDatabaseManager.DropDisposableDatabase(BaseConnection, DatabaseName);
            }
            catch { /* a leaked disposable database is noise; the test result is the signal */ }
            DatabaseName = null;
        }

        public void Dispose() => Cleanup();
    }

    private const string PendingVisitorInsert = @"
INSERT INTO users (user_id, role_id, sub_role, email, password_hash, full_name, status,
                   primary_campus_id, department_id, created_at, updated_at)
VALUES ({0}, {1}, NULL, '{2}', 'x', 'Contact Guard Probe', '{3}', NULL, NULL, NOW(), NULL)";

    /// <summary>MySQL's error number for a user SIGNAL whose SQLSTATE is not one it maps itself.</summary>
    private const int ErSignalException = 1644;

    /// <summary>
    /// Promotes the linked VISITOR to STAFF while satisfying every OTHER rule on the users table —
    /// department and campus included. Without them, <c>trg_users_validate_bu</c> runs first (it is
    /// action_order 1) and refuses with "STAFF/DEPARTMENT must have department_id", so the test would
    /// be measuring an unrelated validator and would still have looked like a pass on SQLSTATE alone.
    /// </summary>
    private string PromoteLinkedVisitorToStaff =>
        "UPDATE users SET role_id = (SELECT role_id FROM roles WHERE role_code = 'STAFF'), " +
        "sub_role = 'STAFF', " +
        "department_id = (SELECT MIN(department_id) FROM departments), " +
        "primary_campus_id = (SELECT MIN(campus_id) FROM campuses) " +
        $"WHERE user_id = {_db.LinkedVisitorUserId}";

    private void AssertGuardRejected(MySqlException? ex, string expectedMessage, string what)
    {
        Assert.True(ex is not null, $"{what}: the database ACCEPTED a write the guard must refuse.");

        // MySql.Data leaves MySqlException.SqlState null; 1644 is what SIGNAL SQLSTATE '45000'
        // surfaces as on this client. The SQLSTATE itself is asserted server-side in
        // Every_guard_refusal_carries_sqlstate_45000.
        Assert.Equal(ErSignalException, ex!.Number);
        Assert.Equal(expectedMessage, ex.Message);
    }

    // ── The five guards must exist, on the right table, at the right time ────────────────────────

    [Theory]
    [InlineData("trg_visit_requests_primary_contact_guard_bi")]
    [InlineData("trg_visit_requests_primary_contact_guard_bu")]
    [InlineData("trg_users_protect_active_primary_contact_bu")]
    [InlineData("trg_visit_request_identity_changes_user_guard_bi")]
    [InlineData("trg_visit_request_identity_changes_user_guard_bu")]
    public void Guard_trigger_is_installed(string triggerName)
    {
        Assert.NotEqual("", _db.TriggerBody(triggerName));
    }

    /// <summary>
    /// The three body properties that G12 fixed, asserted as properties of the installed trigger. A
    /// database restored from a pre-G12 dump would still pass every behavioural test whose account
    /// happens to be ACTIVE, and fail only on the one state this checks for.
    /// </summary>
    [Theory]
    [InlineData("trg_visit_requests_primary_contact_guard_bi")]
    [InlineData("trg_visit_requests_primary_contact_guard_bu")]
    [InlineData("trg_visit_request_identity_changes_user_guard_bi")]
    [InlineData("trg_visit_request_identity_changes_user_guard_bu")]
    public void Visitor_guard_reads_status_into_a_wide_enough_variable(string triggerName)
    {
        var body = _db.TriggerBody(triggerName);

        // users.status' longest ENUM member, PENDING_EMAIL_CONFIRMATION, is 26 characters. At
        // VARCHAR(20) the SELECT ... INTO raised 22001 instead of the business code.
        Assert.Contains("v_user_status VARCHAR(30)", body);
        Assert.DoesNotContain("v_user_status VARCHAR(20)", body);

        // An inner join reported "user not found" for a user that exists but whose role cannot be read.
        Assert.Contains("LEFT JOIN roles", body);

        // NULL <> 'VISITOR' is UNKNOWN, and IF treats UNKNOWN as false.
        Assert.Contains("<=>", body);
    }

    [Fact]
    public void Users_guard_counts_the_role_it_looked_up()
    {
        var body = _db.TriggerBody("trg_users_protect_active_primary_contact_bu");

        // A bare SELECT ... INTO over zero rows leaves the variable NULL and the guard stopped guarding.
        Assert.Contains("v_new_role_count", body);
        Assert.Contains("<=>", body);
    }

    // ── visit_requests: who may be the primary contact ──────────────────────────────────────────

    [Fact]
    public void Internal_account_cannot_become_the_primary_contact()
    {
        var ex = _db.AttemptRolledBack(
            $"UPDATE visit_requests SET visitor_user_id = {_db.InternalUserId}, " +
            $"primary_contact_access_status = 'ACTIVE' WHERE visit_request_id = {_db.LiveRequestId}");

        AssertGuardRejected(ex, "PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR", "internal account as primary contact");
    }

    [Fact]
    public void Updating_visitor_user_id_alone_is_still_guarded()
    {
        // The access status is deliberately NOT written here. A guard keyed on that column alone
        // would let an internal account in through this path.
        var ex = _db.AttemptRolledBack(
            $"UPDATE visit_requests SET visitor_user_id = {_db.InternalUserId} " +
            $"WHERE visit_request_id = {_db.LiveRequestId}");

        AssertGuardRejected(ex, "PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR", "visitor_user_id-only update");
    }

    [Fact]
    public void Non_existent_user_cannot_become_the_primary_contact()
    {
        var ex = _db.AttemptRolledBack(
            $"UPDATE visit_requests SET visitor_user_id = 99999999 WHERE visit_request_id = {_db.LiveRequestId}");

        // The guard must answer before the foreign key does, or the caller gets a constraint error
        // instead of something they can map to a message.
        AssertGuardRejected(ex, "PRIMARY_CONTACT_USER_NOT_FOUND", "non-existent user as primary contact");
    }

    /// <summary>
    /// The case G12 fixed. Every account is created in PENDING_EMAIL_CONFIRMATION, so this is an
    /// ordinary state, and it used to surface as 22001 "Data too long for column 'v_user_status'".
    /// </summary>
    [Fact]
    public void Unconfirmed_visitor_is_refused_with_the_business_code_not_a_storage_error()
    {
        var ex = _db.AttemptRolledBack(
            string.Format(PendingVisitorInsert, 99795, _db.VisitorRoleId,
                          "guard.it.pending@example.test", "PENDING_EMAIL_CONFIRMATION"),
            $"UPDATE visit_requests SET visitor_user_id = 99795 WHERE visit_request_id = {_db.LiveRequestId}");

        AssertGuardRejected(ex, "PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE", "unconfirmed visitor as primary contact");
        Assert.DoesNotContain("Data too long", ex!.Message);
    }

    [Fact]
    public void Inactive_visitor_cannot_become_the_primary_contact()
    {
        var ex = _db.AttemptRolledBack(
            string.Format(PendingVisitorInsert, 99796, _db.VisitorRoleId,
                          "guard.it.inactive@example.test", "INACTIVE"),
            $"UPDATE visit_requests SET visitor_user_id = 99796 WHERE visit_request_id = {_db.LiveRequestId}");

        AssertGuardRejected(ex, "PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE", "inactive visitor as primary contact");
    }

    [Fact]
    public void Active_access_status_requires_a_visitor_user()
    {
        var ex = _db.AttemptRolledBack(
            $"UPDATE visit_requests SET visitor_user_id = NULL WHERE visit_request_id = {_db.LiveRequestId}");

        AssertGuardRejected(ex, "ACTIVE_PRIMARY_CONTACT_REQUIRES_VISITOR_USER", "ACTIVE without a visitor");
    }

    [Fact]
    public void Pending_confirmation_must_not_carry_a_visitor_user()
    {
        var ex = _db.AttemptRolledBack(
            "UPDATE visit_requests SET primary_contact_access_status = 'PENDING_CONFIRMATION' " +
            $"WHERE visit_request_id = {_db.LiveRequestId}");

        AssertGuardRejected(ex, "PENDING_PRIMARY_CONTACT_MUST_NOT_HAVE_VISITOR_USER", "PENDING with a visitor");
    }

    // ── users: a linked contact may not be converted or switched off ────────────────────────────

    [Fact]
    public void Linked_primary_contact_cannot_be_converted_to_an_internal_role()
    {
        var ex = _db.AttemptRolledBack(PromoteLinkedVisitorToStaff);

        AssertGuardRejected(ex, "LINKED_PRIMARY_CONTACT_ROLE_CANNOT_CHANGE", "role change on a linked contact");
    }

    [Fact]
    public void Linked_primary_contact_cannot_be_deactivated()
    {
        var ex = _db.AttemptRolledBack(
            $"UPDATE users SET status = 'INACTIVE' WHERE user_id = {_db.LinkedVisitorUserId}");

        AssertGuardRejected(ex, "LINKED_PRIMARY_CONTACT_CANNOT_BE_DEACTIVATED", "deactivating a linked contact");
    }

    // ── visit_request_identity_changes: the claim/transfer target ───────────────────────────────

    [Fact]
    public void Identity_change_cannot_target_an_internal_account()
    {
        var ex = _db.AttemptRolledBack(
            "INSERT INTO visit_request_identity_changes " +
            "(identity_change_id, visit_request_id, change_kind, target_relation, confirmation_method, " +
            " old_user_id, new_user_id, old_email_normalized, new_email_normalized, new_email_masked, " +
            " pending_snapshot_json, status, expected_request_row_version, requested_by, requested_at, " +
            " expires_at, applied_at, reason, resend_count, created_at, updated_at) VALUES " +
            $"(99797, {_db.LiveRequestId}, 'TRANSFER', 'PRIMARY_CONTACT', 'GOOGLE_SSO', " +
            $" {_db.LinkedVisitorUserId}, {_db.InternalUserId}, 'old@example.test', 'new@example.test', " +
            "  'n***@example.test', JSON_OBJECT('probe','it'), 'APPLIED', 0, " +
            $" {_db.LinkedVisitorUserId}, NOW(), NOW(), NOW(), 'integration probe', 0, NOW(), NOW())");

        AssertGuardRejected(ex, "PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR", "identity change to an internal account");
    }

    [Fact]
    public void Identity_change_cannot_target_an_unconfirmed_visitor()
    {
        var ex = _db.AttemptRolledBack(
            string.Format(PendingVisitorInsert, 99798, _db.VisitorRoleId,
                          "guard.it.transfer@example.test", "PENDING_EMAIL_CONFIRMATION"),
            "INSERT INTO visit_request_identity_changes " +
            "(identity_change_id, visit_request_id, change_kind, target_relation, confirmation_method, " +
            " old_user_id, new_user_id, old_email_normalized, new_email_normalized, new_email_masked, " +
            " pending_snapshot_json, status, expected_request_row_version, requested_by, requested_at, " +
            " expires_at, applied_at, reason, resend_count, created_at, updated_at) VALUES " +
            $"(99799, {_db.LiveRequestId}, 'TRANSFER', 'PRIMARY_CONTACT', 'GOOGLE_SSO', " +
            $" {_db.LinkedVisitorUserId}, 99798, 'old@example.test', 'guard.it.transfer@example.test', " +
            "  'g***@example.test', JSON_OBJECT('probe','it'), 'APPLIED', 0, " +
            $" {_db.LinkedVisitorUserId}, NOW(), NOW(), NOW(), 'integration probe', 0, NOW(), NOW())");

        AssertGuardRejected(ex, "PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE", "identity change to an unconfirmed visitor");
        Assert.DoesNotContain("Data too long", ex!.Message);
    }

    // ── The other half: valid relations must NOT be refused ─────────────────────────────────────
    // A guard that rejects everything passes every negative test and is still useless.

    [Fact]
    public void A_valid_active_visitor_relation_is_accepted()
    {
        var ex = _db.AttemptRolledBack(
            $"UPDATE visit_requests SET updated_at = NOW() WHERE visit_request_id = {_db.LiveRequestId}");

        Assert.Null(ex);
    }

    [Fact]
    public void Swapping_in_another_active_visitor_is_accepted()
    {
        var ex = _db.AttemptRolledBack(
            string.Format(PendingVisitorInsert, 99800, _db.VisitorRoleId,
                          "guard.it.replacement@example.test", "ACTIVE"),
            $"UPDATE visit_requests SET visitor_user_id = 99800 WHERE visit_request_id = {_db.LiveRequestId}");

        Assert.Null(ex);
    }

    [Fact]
    public void A_visitor_linked_to_nothing_may_be_deactivated()
    {
        var ex = _db.AttemptRolledBack(
            string.Format(PendingVisitorInsert, 99801, _db.VisitorRoleId,
                          "guard.it.unlinked@example.test", "ACTIVE"),
            "UPDATE users SET status = 'INACTIVE' WHERE user_id = 99801");

        Assert.Null(ex);
    }

    /// <summary>
    /// The documented exclusion: a cancelled request does not pin its contact's account forever.
    /// The probe asserts the fixture really was linked to a CANCELLED request, so it cannot pass by
    /// quietly deactivating a visitor who was linked to nothing.
    /// </summary>
    [Fact]
    public void A_visitor_linked_only_to_a_cancelled_request_may_be_deactivated()
    {
        var cancelled = _db.Count(
            "SELECT COUNT(*) FROM visit_requests WHERE status = 'CANCELLED' AND primary_contact_access_status = 'ACTIVE'");
        Assert.True(cancelled > 0, "The canonical import carries no cancelled request with an ACTIVE primary contact.");

        var ex = _db.AttemptRolledBack(
            string.Format(PendingVisitorInsert, 99802, _db.VisitorRoleId,
                          "guard.it.cancelled@example.test", "ACTIVE"),
            "UPDATE visit_requests SET visitor_user_id = 99802 WHERE visit_request_id = " +
            "(SELECT r FROM (SELECT MIN(visit_request_id) AS r FROM visit_requests " +
            " WHERE status = 'CANCELLED' AND primary_contact_access_status = 'ACTIVE') t)",
            "UPDATE users SET status = 'INACTIVE' WHERE user_id = 99802");

        Assert.Null(ex);
    }

    // ── The import's own self-test must agree ───────────────────────────────────────────────────

    /// <summary>
    /// Every guard self-test handler in the canonical import reads GET DIAGNOSTICS BEFORE it sets its
    /// raised flag. Setting the flag first loses the SQLSTATE and message of the condition being
    /// handled, which is what the pre-G12 script did — so its self-test could report "guard fired"
    /// without being able to say what it fired with.
    ///
    /// <para>
    /// Resolved through <see cref="CanonicalSqlScript.ResolvePath"/> rather than a filename typed in
    /// here. This test used to name PEMS_FULL_V2_NO_SEED_DATA_GALLERY_DOCUMENT_AI_FIXED.sql, which
    /// commit 74deff85 replaced and deleted — so it had stopped checking anything and was failing on
    /// FileNotFoundException instead. CanonicalSqlScript already tracks the live filename (see its
    /// FileName remarks); going through it means the next rename cannot strand this again.
    /// </para>
    /// </summary>
    [Fact]
    public void Canonical_self_test_handlers_read_diagnostics_before_setting_any_flag()
    {
        var script = File.ReadAllText(CanonicalSqlScript.ResolvePath()).Replace("\r\n", "\n");

        const string wrongOrder =
            "SET v_raised = TRUE;\n      GET DIAGNOSTICS CONDITION 1 v_sqlstate = RETURNED_SQLSTATE";
        Assert.DoesNotContain(wrongOrder, script);

        const string rightOrder =
            "GET DIAGNOSTICS CONDITION 1 v_sqlstate = RETURNED_SQLSTATE, v_message = MESSAGE_TEXT;\n      SET v_raised = TRUE;";
        Assert.Contains(rightOrder, script);
    }

    /// <summary>
    /// The contract these guards publish is "SQLSTATE 45000 plus a stable message". This asserts the
    /// SQLSTATE itself, read on the server, for every refusal path the guards own — not the client's
    /// 1644 stand-in, and not merely "an exception happened".
    /// </summary>
    [Fact]
    public void Every_guard_refusal_carries_sqlstate_45000()
    {
        var cases = new (string What, string ExpectedMessage, string[] Statements)[]
        {
            ("internal account as contact", "PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR", new[]
            {
                $"UPDATE visit_requests SET visitor_user_id = {_db.InternalUserId} WHERE visit_request_id = {_db.LiveRequestId}",
            }),
            ("non-existent user", "PRIMARY_CONTACT_USER_NOT_FOUND", new[]
            {
                $"UPDATE visit_requests SET visitor_user_id = 99999999 WHERE visit_request_id = {_db.LiveRequestId}",
            }),
            ("unconfirmed visitor", "PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE", new[]
            {
                string.Format(PendingVisitorInsert, 99810, _db.VisitorRoleId,
                              "guard.ss.pending@example.test", "PENDING_EMAIL_CONFIRMATION"),
                $"UPDATE visit_requests SET visitor_user_id = 99810 WHERE visit_request_id = {_db.LiveRequestId}",
            }),
            ("inactive visitor", "PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE", new[]
            {
                string.Format(PendingVisitorInsert, 99811, _db.VisitorRoleId,
                              "guard.ss.inactive@example.test", "INACTIVE"),
                $"UPDATE visit_requests SET visitor_user_id = 99811 WHERE visit_request_id = {_db.LiveRequestId}",
            }),
            ("ACTIVE without a visitor", "ACTIVE_PRIMARY_CONTACT_REQUIRES_VISITOR_USER", new[]
            {
                $"UPDATE visit_requests SET visitor_user_id = NULL WHERE visit_request_id = {_db.LiveRequestId}",
            }),
            ("PENDING with a visitor", "PENDING_PRIMARY_CONTACT_MUST_NOT_HAVE_VISITOR_USER", new[]
            {
                "UPDATE visit_requests SET primary_contact_access_status = 'PENDING_CONFIRMATION' " +
                $"WHERE visit_request_id = {_db.LiveRequestId}",
            }),
            ("role change on a linked contact", "LINKED_PRIMARY_CONTACT_ROLE_CANNOT_CHANGE", new[]
            {
                PromoteLinkedVisitorToStaff,
            }),
            ("deactivating a linked contact", "LINKED_PRIMARY_CONTACT_CANNOT_BE_DEACTIVATED", new[]
            {
                $"UPDATE users SET status = 'INACTIVE' WHERE user_id = {_db.LinkedVisitorUserId}",
            }),
        };

        var wrong = new List<string>();

        foreach (var (what, expectedMessage, statements) in cases)
        {
            var (sqlState, message) = _db.ProbeSqlStateRolledBack(statements);
            if (sqlState != "45000" || message != expectedMessage)
                wrong.Add($"{what}: got {sqlState} \"{message}\", expected 45000 \"{expectedMessage}\"");
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    /// <summary>Every stable code the guards signal must still be reachable in an installed body.</summary>
    [Theory]
    [InlineData("PRIMARY_CONTACT_USER_NOT_FOUND")]
    [InlineData("PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR")]
    [InlineData("PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE")]
    [InlineData("ACTIVE_PRIMARY_CONTACT_REQUIRES_VISITOR_USER")]
    [InlineData("PENDING_PRIMARY_CONTACT_MUST_NOT_HAVE_VISITOR_USER")]
    [InlineData("LINKED_PRIMARY_CONTACT_ROLE_CANNOT_CHANGE")]
    [InlineData("LINKED_PRIMARY_CONTACT_CANNOT_BE_DEACTIVATED")]
    public void Stable_code_is_signalled_by_at_least_one_guard(string code)
    {
        var guards = new[]
        {
            "trg_visit_requests_primary_contact_guard_bi",
            "trg_visit_requests_primary_contact_guard_bu",
            "trg_users_protect_active_primary_contact_bu",
            "trg_visit_request_identity_changes_user_guard_bi",
            "trg_visit_request_identity_changes_user_guard_bu",
        };

        Assert.Contains(guards, g => _db.TriggerBody(g).Contains(code, StringComparison.Ordinal));
    }
}
