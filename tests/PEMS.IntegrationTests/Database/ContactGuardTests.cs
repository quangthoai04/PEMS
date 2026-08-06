using System;
using System.Collections.Generic;
using System.IO;
using MySql.Data.MySqlClient;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Database;

/// <summary>
/// The operational-contact guards, exercised from the application's own MySQL client rather than from
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
/// Rewritten for the hard cutover to per-campus operational contacts. The subject changed shape, not
/// just names: the contact now lives on <c>visit_request_campuses.operational_contact_user_id</c>, one
/// per campus, and the request-level <c>visitor_user_id</c> / <c>primary_contact_access_status</c> pair
/// the previous version of this file drove no longer exists. Two rules were deliberately dropped with
/// it and are asserted here as POSITIVES so a future "restoration" of the old guard cannot pass
/// unnoticed: an operational contact is <em>not</em> forced to hold the VISITOR role, and changing that
/// account's role does <em>not</em> disturb the campus it is contact for.
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

        /// <summary>A live campus instance that has a confirmed operational contact.</summary>
        public long LiveInstanceId { get; private set; }

        /// <summary>That campus's parent request.</summary>
        public long LiveRequestId { get; private set; }

        /// <summary>The account confirmed as that campus's operational contact.</summary>
        public long LinkedContactUserId { get; private set; }

        /// <summary>A campus still waiting for its own contact to confirm, and its parent request.</summary>
        public long GatedInstanceId { get; private set; }

        public long GatedRequestId { get; private set; }

        /// <summary>A campus that is past the gate but whose parent request is still behind it.</summary>
        public long ConfirmedBehindGateInstanceId { get; private set; }

        /// <summary>A user who is NOT a VISITOR — any internal account will do.</summary>
        public long InternalUserId { get; private set; }

        /// <summary>The VISITOR role's id, resolved rather than assumed.</summary>
        public long VisitorRoleId { get; private set; }

        public GuardDatabase()
        {
            try
            {
                var name = CanonicalSqlScript.NewDisposableDatabaseName();
                var server = TestDatabaseTarget.ForServer(BaseConnection);

                using (var conn = new MySqlConnection(server))
                {
                    conn.Open();
                    TestDatabaseTarget.AssertConnectedDatabaseIsNotProtected(conn, "the contact-guard import");

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
                ConnectionString = TestDatabaseTarget.ForDisposable(BaseConnection, name);

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
            LiveInstanceId = ScalarLong(@"
SELECT MIN(vrc.visit_instance_id)
FROM visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
WHERE vrc.operational_contact_user_id IS NOT NULL
  AND vrc.status NOT IN ('CANCELLED','REJECTED','CLOSED')
  AND vr.status <> 'CANCELLED'");

            LiveRequestId = ScalarLong(
                $"SELECT visit_request_id FROM visit_request_campuses WHERE visit_instance_id = {LiveInstanceId}");

            LinkedContactUserId = ScalarLong(
                "SELECT operational_contact_user_id FROM visit_request_campuses " +
                $"WHERE visit_instance_id = {LiveInstanceId}");

            // A campus still behind the gate. Its parent request is PENDING_CONTACT_CONFIRMATION by
            // construction — the aggregate trigger cannot leave it anywhere else.
            GatedInstanceId = ScalarLong(@"
SELECT MIN(vrc.visit_instance_id)
FROM visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
WHERE vrc.status = 'WAITING_CONTACT_CONFIRMATION'
  AND vr.status = 'PENDING_CONTACT_CONFIRMATION'");

            GatedRequestId = ScalarLong(
                $"SELECT visit_request_id FROM visit_request_campuses WHERE visit_instance_id = {GatedInstanceId}");

            // The interesting shape for the decision guard: this campus has confirmed, a SIBLING has not,
            // so the request as a whole is still behind the gate. Nothing may be decided here yet.
            ConfirmedBehindGateInstanceId = ScalarLong(@"
SELECT MIN(vrc.visit_instance_id)
FROM visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
WHERE vrc.status = 'WAITING_REQUEST_APPROVAL'
  AND vr.status = 'PENDING_CONTACT_CONFIRMATION'");

            InternalUserId = ScalarLong(@"
SELECT MIN(u.user_id) FROM users u JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code <> 'VISITOR' AND u.status = 'ACTIVE'");

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
            Assert.True(LiveInstanceId > 0, "The canonical import carries no live campus with a confirmed operational contact.");
            Assert.True(LinkedContactUserId > 0, "Could not resolve the linked operational contact.");
            Assert.True(GatedInstanceId > 0, "The canonical import carries no campus still awaiting contact confirmation.");
            Assert.True(InternalUserId > 0, "The canonical import carries no active non-VISITOR account.");
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

    /// <summary>A standalone account, created in whatever state the case needs.</summary>
    private const string ProbeUserInsert = @"
INSERT INTO users (user_id, role_id, sub_role, email, password_hash, full_name, status,
                   primary_campus_id, department_id, created_at, updated_at)
VALUES ({0}, {1}, NULL, '{2}', 'x', 'Contact Guard Probe', '{3}', NULL, NULL, NOW(), NULL)";

    /// <summary>MySQL's error number for a user SIGNAL whose SQLSTATE is not one it maps itself.</summary>
    private const int ErSignalException = 1644;

    private void AssertGuardRejected(MySqlException? ex, string expectedMessage, string what)
    {
        Assert.True(ex is not null, $"{what}: the database ACCEPTED a write the guard must refuse.");

        // MySql.Data leaves MySqlException.SqlState null; 1644 is what SIGNAL SQLSTATE '45000'
        // surfaces as on this client. The SQLSTATE itself is asserted server-side in
        // Every_guard_refusal_carries_sqlstate_45000.
        Assert.Equal(ErSignalException, ex!.Number);
        Assert.Equal(expectedMessage, ex.Message);
    }

    // ── The guards must exist, on the right table, at the right time ─────────────────────────────

    [Theory]
    [InlineData("trg_visit_campuses_op_contact_guard_bi")]
    [InlineData("trg_visit_campuses_op_contact_guard_bu")]
    [InlineData("trg_visit_requests_contact_gate_guard_bu")]
    [InlineData("trg_users_protect_operational_contact_bu")]
    [InlineData("trg_visit_request_identity_changes_user_guard_bi")]
    [InlineData("trg_visit_request_identity_changes_user_guard_bu")]
    public void Guard_trigger_is_installed(string triggerName)
    {
        Assert.NotEqual("", _db.TriggerBody(triggerName));
    }

    /// <summary>
    /// The two body properties G12 fixed, asserted as properties of the installed trigger. A database
    /// restored from a pre-G12 dump would still pass every behavioural test whose account happens to be
    /// ACTIVE, and fail only on the one state this checks for.
    /// </summary>
    [Theory]
    [InlineData("trg_visit_campuses_op_contact_guard_bi")]
    [InlineData("trg_visit_campuses_op_contact_guard_bu")]
    [InlineData("trg_visit_request_identity_changes_user_guard_bi")]
    [InlineData("trg_visit_request_identity_changes_user_guard_bu")]
    public void Contact_guard_reads_status_into_a_wide_enough_variable(string triggerName)
    {
        var body = _db.TriggerBody(triggerName);

        // users.status' longest ENUM member, PENDING_EMAIL_CONFIRMATION, is 26 characters. At
        // VARCHAR(20) the SELECT ... INTO raised 22001 instead of the business code.
        Assert.Contains("v_user_status VARCHAR(30)", body);
        Assert.DoesNotContain("v_user_status VARCHAR(20)", body);

        // NULL <> 'ACTIVE' is UNKNOWN, and IF treats UNKNOWN as false.
        Assert.Contains("<=>", body);
    }

    // ── visit_request_campuses: status and contact must agree ───────────────────────────────────

    [Fact]
    public void A_campus_still_awaiting_confirmation_must_not_carry_a_contact()
    {
        var ex = _db.AttemptRolledBack(
            $"UPDATE visit_request_campuses SET operational_contact_user_id = {_db.InternalUserId} " +
            $"WHERE visit_instance_id = {_db.GatedInstanceId}");

        AssertGuardRejected(ex,
            "WAITING_CONTACT_CONFIRMATION_MUST_NOT_HAVE_OPERATIONAL_CONTACT",
            "contact set while the campus is still WAITING_CONTACT_CONFIRMATION");
    }

    [Fact]
    public void A_campus_past_confirmation_must_keep_a_contact()
    {
        var ex = _db.AttemptRolledBack(
            "UPDATE visit_request_campuses SET operational_contact_user_id = NULL " +
            $"WHERE visit_instance_id = {_db.LiveInstanceId}");

        AssertGuardRejected(ex,
            "CAMPUS_BEYOND_CONFIRMATION_REQUIRES_OPERATIONAL_CONTACT",
            "clearing the contact of a campus past the gate");
    }

    [Fact]
    public void Non_existent_user_cannot_become_the_operational_contact()
    {
        var ex = _db.AttemptRolledBack(
            "UPDATE visit_request_campuses SET operational_contact_user_id = 99999999 " +
            $"WHERE visit_instance_id = {_db.LiveInstanceId}");

        // The guard must answer before the foreign key does, or the caller gets a constraint error
        // instead of something they can map to a message.
        AssertGuardRejected(ex, "OPERATIONAL_CONTACT_USER_NOT_FOUND", "non-existent user as operational contact");
    }

    /// <summary>
    /// The case G12 fixed. Every account is created in PENDING_EMAIL_CONFIRMATION, so this is an
    /// ordinary state, and it used to surface as 22001 "Data too long for column 'v_user_status'".
    /// </summary>
    [Fact]
    public void Unconfirmed_account_is_refused_with_the_business_code_not_a_storage_error()
    {
        var ex = _db.AttemptRolledBack(
            string.Format(ProbeUserInsert, 99795, _db.VisitorRoleId,
                          "guard.it.pending@example.test", "PENDING_EMAIL_CONFIRMATION"),
            "UPDATE visit_request_campuses SET operational_contact_user_id = 99795 " +
            $"WHERE visit_instance_id = {_db.LiveInstanceId}");

        AssertGuardRejected(ex, "OPERATIONAL_CONTACT_ACCOUNT_INACTIVE", "unconfirmed account as operational contact");
        Assert.DoesNotContain("Data too long", ex!.Message);
    }

    [Fact]
    public void Inactive_account_cannot_become_the_operational_contact()
    {
        var ex = _db.AttemptRolledBack(
            string.Format(ProbeUserInsert, 99796, _db.VisitorRoleId,
                          "guard.it.inactive@example.test", "INACTIVE"),
            "UPDATE visit_request_campuses SET operational_contact_user_id = 99796 " +
            $"WHERE visit_instance_id = {_db.LiveInstanceId}");

        AssertGuardRejected(ex, "OPERATIONAL_CONTACT_ACCOUNT_INACTIVE", "inactive account as operational contact");
    }

    // ── The global confirmation gate ────────────────────────────────────────────────────────────

    [Fact]
    public void A_request_cannot_leave_the_gate_while_a_campus_is_unconfirmed()
    {
        var ex = _db.AttemptRolledBack(
            "UPDATE visit_requests SET status = 'PENDING_APPROVAL' " +
            $"WHERE visit_request_id = {_db.GatedRequestId}");

        AssertGuardRejected(ex, "CONTACT_CONFIRMATION_REQUIRED", "request leaving the gate with a campus unconfirmed");
    }

    /// <summary>
    /// The decision guard, driven through the transition approval actually produces.
    ///
    /// <para>
    /// A campus whose own contact has confirmed sits at WAITING_REQUEST_APPROVAL even while a SIBLING
    /// campus is still unconfirmed — so the parent request is behind the gate and no campus may be
    /// decided yet (plan §3.4: "cổng xác nhận vẫn mở" is a precondition of approve). Approving now lands
    /// on ASSIGNED, so ASSIGNED is the transition this guard has to cover; checking only BEFORE_VISIT
    /// leaves the whole approve path unguarded, because approve no longer produces BEFORE_VISIT.
    /// </para>
    /// </summary>
    [Fact]
    public void A_campus_cannot_be_approved_while_the_request_is_behind_the_gate()
    {
        Assert.True(_db.ConfirmedBehindGateInstanceId > 0,
            "The canonical import carries no confirmed campus under a request that is still behind the gate.");

        var ex = _db.AttemptRolledBack(
            "UPDATE visit_request_campuses SET status = 'ASSIGNED' " +
            $"WHERE visit_instance_id = {_db.ConfirmedBehindGateInstanceId}");

        AssertGuardRejected(ex, "CONTACT_CONFIRMATION_REQUIRED", "approving a campus while the request is behind the gate");
    }

    [Fact]
    public void A_campus_cannot_be_rejected_while_the_request_is_behind_the_gate()
    {
        Assert.True(_db.ConfirmedBehindGateInstanceId > 0,
            "The canonical import carries no confirmed campus under a request that is still behind the gate.");

        var ex = _db.AttemptRolledBack(
            "UPDATE visit_request_campuses SET status = 'REJECTED' " +
            $"WHERE visit_instance_id = {_db.ConfirmedBehindGateInstanceId}");

        AssertGuardRejected(ex, "CONTACT_CONFIRMATION_REQUIRED", "rejecting a campus while the request is behind the gate");
    }

    // ── users: a linked contact may not be switched off ─────────────────────────────────────────

    [Fact]
    public void Linked_operational_contact_cannot_be_deactivated()
    {
        var ex = _db.AttemptRolledBack(
            $"UPDATE users SET status = 'INACTIVE' WHERE user_id = {_db.LinkedContactUserId}");

        AssertGuardRejected(ex, "LINKED_OPERATIONAL_CONTACT_CANNOT_BE_DEACTIVATED", "deactivating a linked contact");
    }

    // ── visit_request_identity_changes: the confirmation/transfer target ────────────────────────

    [Fact]
    public void Identity_change_cannot_target_a_non_existent_account()
    {
        var ex = _db.AttemptRolledBack(IdentityChangeInsert(99797, newUserId: "99999999"));

        AssertGuardRejected(ex, "OPERATIONAL_CONTACT_USER_NOT_FOUND", "identity change to a non-existent account");
    }

    [Fact]
    public void Identity_change_cannot_target_an_unconfirmed_account()
    {
        var ex = _db.AttemptRolledBack(
            string.Format(ProbeUserInsert, 99798, _db.VisitorRoleId,
                          "guard.it.transfer@example.test", "PENDING_EMAIL_CONFIRMATION"),
            IdentityChangeInsert(99799, newUserId: "99798"));

        AssertGuardRejected(ex, "OPERATIONAL_CONTACT_ACCOUNT_INACTIVE", "identity change to an unconfirmed account");
        Assert.DoesNotContain("Data too long", ex!.Message);
    }

    /// <summary>
    /// A TRANSFER row on the live campus, already APPLIED so it does not collide with the
    /// one-PENDING-per-campus unique key, and always naming the contact it takes the role from —
    /// <c>trg_identity_changes_transfer_bi</c> requires <c>old_user_id</c> on a TRANSFER.
    /// </summary>
    private string IdentityChangeInsert(long id, string newUserId) =>
        "INSERT INTO visit_request_identity_changes " +
        "(identity_change_id, visit_request_id, visit_instance_id, change_kind, token_version, " +
        " confirmation_method, old_user_id, new_user_id, old_email_normalized, new_email_normalized, " +
        " new_email_masked, pending_snapshot_json, status, expected_request_row_version, requested_by, " +
        " requested_at, expires_at, applied_at, reason, resend_count, created_at, updated_at) VALUES " +
        $"({id}, {_db.LiveRequestId}, {_db.LiveInstanceId}, 'TRANSFER', 1, 'GOOGLE_SSO', " +
        $" {_db.LinkedContactUserId}, {newUserId}, 'old@example.test', 'new@example.test', " +
        "  'n***@example.test', JSON_OBJECT('probe','it'), 'APPLIED', 0, " +
        $" {_db.LinkedContactUserId}, NOW(), NOW(), NOW(), 'integration probe', 0, NOW(), NOW())";

    // ── The other half: valid relations must NOT be refused ─────────────────────────────────────
    // A guard that rejects everything passes every negative test and is still useless.

    [Fact]
    public void A_valid_confirmed_contact_relation_is_accepted()
    {
        var ex = _db.AttemptRolledBack(
            $"UPDATE visit_requests SET updated_at = NOW() WHERE visit_request_id = {_db.LiveRequestId}");

        Assert.Null(ex);
    }

    /// <summary>
    /// The deliberate behaviour change of this cutover, asserted as a positive so a re-introduced
    /// "must be a VISITOR" rule cannot slip back in unnoticed: an internal account is allowed to be a
    /// campus's operational contact, as long as it is ACTIVE (plan §1.7).
    /// </summary>
    [Fact]
    public void An_internal_account_may_be_the_operational_contact()
    {
        var ex = _db.AttemptRolledBack(
            $"UPDATE visit_request_campuses SET operational_contact_user_id = {_db.InternalUserId} " +
            $"WHERE visit_instance_id = {_db.LiveInstanceId}");

        Assert.Null(ex);
    }

    /// <summary>
    /// The second dropped rule. The old model pinned the contact's ROLE, because request-level access
    /// was derived from it; per-campus access is read from operational_contact_user_id, so a role change
    /// no longer threatens anything and must not be refused.
    /// </summary>
    [Fact]
    public void A_linked_contacts_role_may_change()
    {
        var ex = _db.AttemptRolledBack(
            "UPDATE users SET role_id = (SELECT role_id FROM roles WHERE role_code = 'STAFF'), " +
            "sub_role = 'STAFF', " +
            "department_id = (SELECT MIN(department_id) FROM departments), " +
            "primary_campus_id = (SELECT MIN(campus_id) FROM campuses) " +
            $"WHERE user_id = {_db.LinkedContactUserId}");

        Assert.Null(ex);
    }

    [Fact]
    public void Identity_change_may_target_an_active_internal_account()
    {
        var ex = _db.AttemptRolledBack(IdentityChangeInsert(99803, newUserId: _db.InternalUserId.ToString()));

        Assert.Null(ex);
    }

    [Fact]
    public void An_account_linked_to_nothing_may_be_deactivated()
    {
        var ex = _db.AttemptRolledBack(
            string.Format(ProbeUserInsert, 99801, _db.VisitorRoleId,
                          "guard.it.unlinked@example.test", "ACTIVE"),
            "UPDATE users SET status = 'INACTIVE' WHERE user_id = 99801");

        Assert.Null(ex);
    }

    /// <summary>
    /// The documented exclusion: a campus that is over does not pin its contact's account forever.
    /// The probe asserts the fixture really was linked to such a campus, so it cannot pass by quietly
    /// deactivating an account that was linked to nothing.
    /// </summary>
    [Fact]
    public void An_account_linked_only_to_a_finished_campus_may_be_deactivated()
    {
        var overCampuses = _db.Count(@"
SELECT COUNT(*) FROM visit_request_campuses vrc
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
WHERE vrc.operational_contact_user_id IS NOT NULL
  AND (vrc.status IN ('CANCELLED','REJECTED','CLOSED') OR vr.status = 'CANCELLED')");
        Assert.True(overCampuses > 0, "The canonical import carries no finished campus with an operational contact.");

        // The campus is moved out from under the guard first — CLOSED campuses are outside its scope —
        // and only then is the account switched off.
        var ex = _db.AttemptRolledBack(
            string.Format(ProbeUserInsert, 99802, _db.VisitorRoleId,
                          "guard.it.finished@example.test", "ACTIVE"),
            "UPDATE visit_request_campuses SET operational_contact_user_id = 99802 " +
            "WHERE visit_instance_id = (SELECT i FROM (SELECT MIN(vrc.visit_instance_id) AS i " +
            " FROM visit_request_campuses vrc WHERE vrc.status IN ('CANCELLED','REJECTED','CLOSED') " +
            " AND vrc.operational_contact_user_id IS NOT NULL) t)",
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
            ("non-existent user", "OPERATIONAL_CONTACT_USER_NOT_FOUND", new[]
            {
                "UPDATE visit_request_campuses SET operational_contact_user_id = 99999999 " +
                $"WHERE visit_instance_id = {_db.LiveInstanceId}",
            }),
            ("unconfirmed account", "OPERATIONAL_CONTACT_ACCOUNT_INACTIVE", new[]
            {
                string.Format(ProbeUserInsert, 99810, _db.VisitorRoleId,
                              "guard.ss.pending@example.test", "PENDING_EMAIL_CONFIRMATION"),
                "UPDATE visit_request_campuses SET operational_contact_user_id = 99810 " +
                $"WHERE visit_instance_id = {_db.LiveInstanceId}",
            }),
            ("inactive account", "OPERATIONAL_CONTACT_ACCOUNT_INACTIVE", new[]
            {
                string.Format(ProbeUserInsert, 99811, _db.VisitorRoleId,
                              "guard.ss.inactive@example.test", "INACTIVE"),
                "UPDATE visit_request_campuses SET operational_contact_user_id = 99811 " +
                $"WHERE visit_instance_id = {_db.LiveInstanceId}",
            }),
            ("contact on a campus still behind the gate", "WAITING_CONTACT_CONFIRMATION_MUST_NOT_HAVE_OPERATIONAL_CONTACT", new[]
            {
                $"UPDATE visit_request_campuses SET operational_contact_user_id = {_db.InternalUserId} " +
                $"WHERE visit_instance_id = {_db.GatedInstanceId}",
            }),
            ("campus past the gate with no contact", "CAMPUS_BEYOND_CONFIRMATION_REQUIRES_OPERATIONAL_CONTACT", new[]
            {
                "UPDATE visit_request_campuses SET operational_contact_user_id = NULL " +
                $"WHERE visit_instance_id = {_db.LiveInstanceId}",
            }),
            ("request leaving the gate early", "CONTACT_CONFIRMATION_REQUIRED", new[]
            {
                $"UPDATE visit_requests SET status = 'PENDING_APPROVAL' WHERE visit_request_id = {_db.GatedRequestId}",
            }),
            ("campus approved behind the gate", "CONTACT_CONFIRMATION_REQUIRED", new[]
            {
                "UPDATE visit_request_campuses SET status = 'ASSIGNED' " +
                $"WHERE visit_instance_id = {_db.ConfirmedBehindGateInstanceId}",
            }),
            ("deactivating a linked contact", "LINKED_OPERATIONAL_CONTACT_CANNOT_BE_DEACTIVATED", new[]
            {
                $"UPDATE users SET status = 'INACTIVE' WHERE user_id = {_db.LinkedContactUserId}",
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
    [InlineData("OPERATIONAL_CONTACT_USER_NOT_FOUND")]
    [InlineData("OPERATIONAL_CONTACT_ACCOUNT_INACTIVE")]
    [InlineData("WAITING_CONTACT_CONFIRMATION_MUST_NOT_HAVE_OPERATIONAL_CONTACT")]
    [InlineData("CAMPUS_BEYOND_CONFIRMATION_REQUIRES_OPERATIONAL_CONTACT")]
    [InlineData("CONTACT_CONFIRMATION_REQUIRED")]
    [InlineData("LINKED_OPERATIONAL_CONTACT_CANNOT_BE_DEACTIVATED")]
    public void Stable_code_is_signalled_by_at_least_one_guard(string code)
    {
        var guards = new[]
        {
            "trg_visit_campuses_op_contact_guard_bi",
            "trg_visit_campuses_op_contact_guard_bu",
            "trg_visit_requests_contact_gate_guard_bu",
            "trg_users_protect_operational_contact_bu",
            "trg_visit_request_identity_changes_user_guard_bi",
            "trg_visit_request_identity_changes_user_guard_bu",
        };

        Assert.Contains(guards, g => _db.TriggerBody(g).Contains(code, StringComparison.Ordinal));
    }

    /// <summary>
    /// The old model's stable codes must be gone from every installed trigger. They named a
    /// request-level relation that no longer exists, and a body still signalling them would mean a
    /// pre-cutover dump had been restored under a post-cutover application.
    /// </summary>
    [Theory]
    [InlineData("PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR")]
    [InlineData("ACTIVE_PRIMARY_CONTACT_REQUIRES_VISITOR_USER")]
    [InlineData("PENDING_PRIMARY_CONTACT_MUST_NOT_HAVE_VISITOR_USER")]
    [InlineData("LINKED_PRIMARY_CONTACT_ROLE_CANNOT_CHANGE")]
    [InlineData("LINKED_PRIMARY_CONTACT_CANNOT_BE_DEACTIVATED")]
    public void Retired_code_is_signalled_by_no_trigger(string code)
    {
        var count = _db.Count(
            "SELECT COUNT(*) FROM information_schema.triggers WHERE trigger_schema = DATABASE() " +
            $"AND action_statement LIKE '%{code}%'");

        Assert.Equal(0, count);
    }
}
