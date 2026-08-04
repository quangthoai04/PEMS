using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using PEMS.Application.Emails.Common;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Runs the four scripts in <c>docs/database/scripts/email_template_cc_bcc_sync/</c> against a database
/// that looks like a real deployment rather than a fresh import, and asserts what they promise.
///
/// <para>
/// The interesting case is never the empty database. It is the one that has been running for months:
/// some canonical templates missing, some carrying stale content, the nine retired codes still ACTIVE,
/// history holding foreign keys into them, and templates an operator wrote by hand that the sync has
/// no business touching. This class builds exactly that and then measures.
/// </para>
///
/// <para>
/// It imports its OWN database rather than sharing the suite's. The sync deliberately mutates
/// <c>email_templates</c> — deactivating codes, rewriting content — and
/// <see cref="SystemEmailTemplateContractTests"/> asserts the catalog is pristine. Sharing one database
/// would make the two classes' verdicts depend on which ran first.
/// </para>
/// </summary>
public sealed class EmailTemplateSyncScriptTests : IClassFixture<EmailTemplateSyncScriptTests.SyncDatabase>
{
    private readonly SyncDatabase _db;

    public EmailTemplateSyncScriptTests(SyncDatabase db)
    {
        _db = db;
        _db.Require();
    }

    // ── The fixture: one import, one fixture-application, for the whole class ────────────────────

    public sealed class SyncDatabase : IDisposable
    {
        // No GuidFormat here: that option belongs to Pomelo, and every connection in this class is a
        // MySql.Data MySqlConnection (MySqlScript needs one), which rejects the key outright.
        private const string BaseConnection =
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True";

        public string? DatabaseName { get; private set; }
        public string ConnectionString { get; private set; } = "";
        public string? Failure { get; private set; }

        public string ScriptsDirectory { get; } = Path.Combine(
            CanonicalSqlScript.FindRepositoryRoot(), "docs", "database", "scripts", "email_template_cc_bcc_sync");

        public SyncDatabase()
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

                    // Same guard the suite's own bootstrap uses: rewrite every database-selection statement,
                    // then re-scan the produced text before a byte reaches the server.
                    var retargeted = CanonicalSqlScript.Retarget(CanonicalSqlScript.ReadVerified(), name);
                    using (var cmd = conn.CreateCommand()) { cmd.CommandText = $"USE `{name}`;"; cmd.ExecuteNonQuery(); }
                    new MySqlScript(conn, retargeted).Execute();
                }

                DatabaseName = name;
                ConnectionString = System.Text.RegularExpressions.Regex.Replace(
                    BaseConnection, @"database=[^;]+;", $"database={name};",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                ApplyExistingDeploymentFixture();
            }
            catch (Exception ex)
            {
                Failure = ex.ToString();
                Cleanup();
            }
        }

        public void Require() => Assert.True(DatabaseName is not null,
            "Could not build the sync-test database. " + Failure);

        /// <summary>
        /// Turns the freshly-imported canonical database into a plausible "existing deployment":
        /// three canonical templates absent, four stale or wrongly deactivated, the nine retired codes
        /// ACTIVE, history referencing two of them, and two operator-authored templates.
        /// </summary>
        private void ApplyExistingDeploymentFixture()
        {
            Execute(@"
DELETE FROM email_templates WHERE template_code IN
  ('DEPT_LEADERSHIP_GRANTED','DEPT_LEADERSHIP_HANDED_OVER','LOGISTICS_CHANGE_PROPOSAL_TO_HOST');

UPDATE email_templates
SET subject_vi='STALE SUBJECT', body_vi='<p>noi dung cu</p>', variables_text='wrongVariable'
WHERE template_code='ACCOUNT_EMAIL_CONFIRMATION';
UPDATE email_templates SET status='INACTIVE' WHERE template_code='AUTH_PASSWORD_RESET_OTP';
UPDATE email_templates SET name='Ten cu', description=NULL WHERE template_code='VISIT_REQUEST_OTP';
UPDATE email_templates SET body_en='<p>outdated</p>' WHERE template_code='REPORT_DEPARTMENT_INVOICE';");

            foreach (var code in LegacyCodes)
                Execute($@"
INSERT INTO email_templates
  (template_code, name, purpose, campus_id, description, status,
   subject_vi, body_vi, subject_en, body_en, body_format, variables_text, created_at)
VALUES ('{code}', 'Legacy {code}', 'ACCOUNT', NULL, 'legacy', 'ACTIVE',
        'Legacy VI', '<p>legacy vi</p>', 'Legacy EN', '<p>legacy en</p>', 'HTML', 'fullName', NOW());");

            Execute(@"
INSERT INTO email_templates
  (template_code, name, purpose, campus_id, description, status,
   subject_vi, body_vi, subject_en, body_en, body_format, variables_text, created_at)
VALUES
  ('CUSTOM_CAMPUS_NEWSLETTER','Ban tin tu soan','ACCOUNT',NULL,'operator authored','ACTIVE',
   'Ban tin','<p>Xin chao {{fullName}}</p>','Newsletter','<p>Hello {{fullName}}</p>','HTML','fullName',NOW()),
  ('CUSTOM_ARCHIVED_ANNOUNCEMENT','Thong bao cu','ACCOUNT',NULL,'retired by operator','INACTIVE',
   'Thong bao','<p>Noi dung</p>','Announcement','<p>Content</p>','HTML',NULL,NOW());

INSERT INTO sent_emails
  (sent_email_id, email_template_id, related_type, related_id, subject, body_snapshot,
   provider_thread_id, provider_message_id, retry_count, status, sent_by, sent_at, created_at)
SELECT 900001, t.email_template_id, 'USER', 3, 'History on a legacy template',
       '<p>history snapshot must not change</p>', 'thread-legacy-1','msg-legacy-1', 0, 'SENT', 1,
       '2026-03-01 09:00:00','2026-03-01 09:00:00'
FROM email_templates t WHERE t.template_code='ACCOUNT_CREATED_INTERNAL';

INSERT INTO sent_emails
  (sent_email_id, email_template_id, related_type, related_id, subject, body_snapshot,
   provider_thread_id, provider_message_id, retry_count, status, sent_by, sent_at, created_at)
SELECT 900002, t.email_template_id, 'USER', 4, 'History on a canonical template',
       '<p>second history snapshot</p>', 'thread-canon-1','msg-canon-1', 0, 'SENT', 1,
       '2026-03-02 09:00:00','2026-03-02 09:00:00'
FROM email_templates t WHERE t.template_code='ACCOUNT_EMAIL_CONFIRMATION';

INSERT INTO sent_email_recipients
  (sent_email_id, recipient_email, recipient_name, recipient_type, delivery_status, sent_at, created_at)
VALUES
  (900001,'legacy.to@fpt.edu.vn','Legacy TO','TO','SENT','2026-03-01 09:00:00','2026-03-01 09:00:00'),
  (900001,'legacy.bcc@fpt.edu.vn','Legacy BCC','BCC','SENT','2026-03-01 09:00:00','2026-03-01 09:00:00'),
  (900002,'canon.to@fpt.edu.vn','Canon TO','TO','SENT','2026-03-02 09:00:00','2026-03-02 09:00:00');

-- A second, different legacy code that something still points at. This used to be an email_drafts
-- row; with the draft tables gone, history is the only thing left that holds a foreign key into
-- email_templates, so the reference lives here instead. Keeping two distinct legacy codes referenced
-- is the point — check B2 (""a referenced legacy row is deactivated, not deleted"") would still pass
-- on a script that special-cased one code.
INSERT INTO sent_emails
  (sent_email_id, email_template_id, related_type, related_id, subject, body_snapshot,
   provider_thread_id, provider_message_id, retry_count, status, sent_by, sent_at, created_at)
SELECT 900003, t.email_template_id, 'USER', 5, 'History on a second legacy template',
       '<p>third history snapshot</p>', 'thread-legacy-2','msg-legacy-2', 0, 'SENT', 1,
       '2026-03-04 09:00:00','2026-03-04 09:00:00'
FROM email_templates t WHERE t.template_code='LOGISTICS_REQUEST';

INSERT INTO sent_email_recipients
  (sent_email_id, recipient_email, recipient_name, recipient_type, delivery_status, sent_at, created_at)
VALUES
  (900003,'legacy2.to@fpt.edu.vn','Legacy2 TO','TO','SENT','2026-03-04 09:00:00','2026-03-04 09:00:00'),
  (900003,'legacy2.bcc@fpt.edu.vn','Legacy2 BCC','BCC','SENT','2026-03-04 09:00:00','2026-03-04 09:00:00');");
        }

        public void Execute(string sql)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            new MySqlScript(conn, sql).Execute();
        }

        /// <summary>Runs one of the four scripts on a single session, so session variables survive into it.</summary>
        public void RunScript(string fileName, string? sessionPrelude = null)
        {
            var path = Path.Combine(ScriptsDirectory, fileName);
            Assert.True(File.Exists(path), $"Script not found: {path}");

            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            if (sessionPrelude is not null)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sessionPrelude;
                cmd.ExecuteNonQuery();
            }

            new MySqlScript(conn, File.ReadAllText(path)).Execute();
        }

        public List<Dictionary<string, object?>> Query(string sql)
        {
            using var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();

            var rows = new List<Dictionary<string, object?>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }

        public string? Scalar(string sql) => Query(sql).FirstOrDefault()?.Values.FirstOrDefault()?.ToString();

        /// <summary>
        /// A diffable image of everything the sync must either change deliberately or leave alone.
        /// Content is hashed rather than compared verbatim so a difference is a difference, not 900 lines.
        /// </summary>
        public string Snapshot()
        {
            var parts = new List<string>();
            foreach (var r in Query(@"
SELECT CONCAT_WS('|','TEMPLATE',template_code,email_template_id,status,
  SHA2(CONCAT_WS('',name,purpose,IFNULL(campus_id,'~'),IFNULL(description,'~'),IFNULL(subject_vi,'~'),
       IFNULL(body_vi,'~'),IFNULL(subject_en,'~'),IFNULL(body_en,'~'),body_format,IFNULL(variables_text,'~')),256),
  IFNULL(DATE_FORMAT(updated_at,'%Y-%m-%d %H:%i:%s'),'never')) AS line
FROM email_templates ORDER BY template_code;")) parts.Add(r["line"]!.ToString()!);

            foreach (var r in Query(@"
SELECT CONCAT_WS('|','SENT',sent_email_id,IFNULL(email_template_id,'~'),status,
  SHA2(CONCAT_WS('',subject,IFNULL(body_snapshot,'~')),256)) AS line
FROM sent_emails ORDER BY sent_email_id;")) parts.Add(r["line"]!.ToString()!);

            foreach (var r in Query(@"
SELECT CONCAT_WS('|','RCPT',sent_email_id,recipient_type,recipient_email,delivery_status) AS line
FROM sent_email_recipients ORDER BY sent_email_id,recipient_type,recipient_email;")) parts.Add(r["line"]!.ToString()!);

            foreach (var r in Query(@"
SELECT CONCAT_WS('|','COUNT','email_action_tokens',COUNT(*)) AS line FROM email_action_tokens
UNION ALL SELECT CONCAT_WS('|','COUNT','email_send_idempotency',COUNT(*)) FROM email_send_idempotency
UNION ALL SELECT CONCAT_WS('|','COUNT','sent_email_attachments',COUNT(*)) FROM sent_email_attachments
UNION ALL SELECT CONCAT_WS('|','COUNT','users',COUNT(*)) FROM users
UNION ALL SELECT CONCAT_WS('|','COUNT','visit_requests',COUNT(*)) FROM visit_requests
UNION ALL SELECT CONCAT_WS('|','COUNT','files',COUNT(*)) FROM files
UNION ALL SELECT CONCAT_WS('|','COUNT','news',COUNT(*)) FROM news
UNION ALL SELECT CONCAT_WS('|','COUNT','faqs',COUNT(*)) FROM faqs
UNION ALL SELECT CONCAT_WS('|','COUNT','gallery_items',COUNT(*)) FROM gallery_items;"))
                parts.Add(r["line"]!.ToString()!);

            return string.Join("\n", parts);
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

        public static readonly string[] LegacyCodes =
        {
            "ACCOUNT_CREATED_INTERNAL", "VISIT_REQUEST_APPROVED", "VISIT_REQUEST_REJECTED",
            "VISIT_CANCELLED", "HOST_ASSIGNMENT", "VISIT_REQUEST_SUBMITTED_NOTIFY",
            "LOGISTICS_REQUEST", "LOGISTICS_REQUEST_SUBMITTED_NOTIFY", "OTP_VISIT_REQUEST",
        };
    }

    // ── Ordering ────────────────────────────────────────────────────────────────────────────────
    // xUnit builds a fresh test-class instance per fact but shares the IClassFixture. These facts
    // therefore run against one database whose state advances, so each one does its own sync run
    // rather than depending on a previous fact having run first.

    private void Sync() => _db.RunScript("02_sync_templates.sql",
        $"SET @pems_sync_confirm_database = '{_db.DatabaseName}';");

    private void Verify() => _db.RunScript("03_verify.sql");

    // ── 1. The scripts exist and the preflight is genuinely read-only ───────────────────────────

    [Fact]
    public void All_four_scripts_are_present()
    {
        foreach (var f in new[] { "01_preflight.sql", "02_sync_templates.sql", "03_verify.sql", "04_rollback_guidance.md" })
            Assert.True(File.Exists(Path.Combine(_db.ScriptsDirectory, f)), $"missing: {f}");
    }

    [Fact]
    public void Preflight_changes_nothing()
    {
        var before = _db.Snapshot();
        _db.RunScript("01_preflight.sql");
        Assert.Equal(before, _db.Snapshot());
    }

    [Fact]
    public void Preflight_contains_no_mutating_statement()
    {
        var sql = File.ReadAllText(Path.Combine(_db.ScriptsDirectory, "01_preflight.sql"));
        var body = string.Join("\n", sql.Split('\n').Where(l => !l.TrimStart().StartsWith("--")));

        foreach (var verb in new[] { "INSERT ", "UPDATE ", "DELETE ", "DROP ", "ALTER ", "TRUNCATE ", "CREATE " })
            Assert.DoesNotContain(verb, body, StringComparison.OrdinalIgnoreCase);
    }

    // ── 2. The guard ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sync_refuses_to_run_without_an_explicitly_named_target()
    {
        var before = _db.Snapshot();

        // No @pems_sync_confirm_database — the operator has not named what they are about to modify.
        var ex = Assert.ThrowsAny<MySqlException>(() => _db.RunScript("02_sync_templates.sql"));
        Assert.Contains("Refusing to sync", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(before, _db.Snapshot());
    }

    /// <summary>
    /// The confirmation is a session variable, and a session can outlive one use of it — a pooled
    /// connection, or a client left open across several files. This regression exists because it
    /// happened: with the whole class running, an earlier sync left the variable set on a pooled
    /// connection and this test's "unconfirmed" run went straight through. The script now clears the
    /// variable as its last statement, so one confirmation authorises exactly one run.
    /// </summary>
    [Fact]
    public void Sync_spends_the_confirmation_so_the_same_session_cannot_reuse_it()
    {
        using var conn = new MySqlConnection(_db.ConnectionString);
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SET @pems_sync_confirm_database = '{_db.DatabaseName}';";
            cmd.ExecuteNonQuery();
        }

        var script = File.ReadAllText(Path.Combine(_db.ScriptsDirectory, "02_sync_templates.sql"));
        new MySqlScript(conn, script).Execute();          // first run: confirmed, proceeds

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT @pems_sync_confirm_database;";
            Assert.True(cmd.ExecuteScalar() is null or DBNull,
                "the confirmation survived the run, so the same session could sync again unconfirmed");
        }

        // Same connection, same session, no fresh confirmation: must refuse.
        var ex = Assert.ThrowsAny<MySqlException>(() => new MySqlScript(conn, script).Execute());
        Assert.Contains("Refusing to sync", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sync_refuses_when_the_named_target_is_a_different_database()
    {
        var before = _db.Snapshot();

        var ex = Assert.ThrowsAny<MySqlException>(() =>
            _db.RunScript("02_sync_templates.sql", "SET @pems_sync_confirm_database = 'pems_db';"));
        Assert.Contains("Refusing to sync", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(before, _db.Snapshot());
    }

    // ── 3. What the sync converges on ───────────────────────────────────────────────────────────

    [Fact]
    public void Sync_makes_every_registered_template_present_and_active()
    {
        Sync();

        var active = _db.Query("SELECT template_code FROM email_templates WHERE status='ACTIVE';")
            .Select(r => r["template_code"]!.ToString()!).ToHashSet(StringComparer.Ordinal);

        foreach (var code in SystemEmailTemplates.AllCodes)
            Assert.True(active.Contains(code), $"registry code not ACTIVE after sync: {code}");
    }

    [Fact]
    public void Sync_retires_every_legacy_code_without_deleting_it()
    {
        Sync();

        foreach (var code in SyncDatabase.LegacyCodes)
        {
            var status = _db.Scalar($"SELECT status FROM email_templates WHERE template_code='{code}';");
            Assert.True(status is not null, $"legacy template was DELETED, orphaning its references: {code}");
            Assert.Equal("INACTIVE", status);
        }
    }

    [Fact]
    public void Sync_matches_on_code_so_existing_rows_keep_their_id()
    {
        // Rows that exist before the sync and are rewritten by it. If the upsert went by numeric id —
        // or deleted and re-inserted — these ids would move and every foreign key would follow.
        var before = _db.Query(
            "SELECT template_code, email_template_id FROM email_templates ORDER BY template_code;")
            .ToDictionary(r => r["template_code"]!.ToString()!, r => r["email_template_id"]!.ToString()!);

        Sync();

        var after = _db.Query(
            "SELECT template_code, email_template_id FROM email_templates ORDER BY template_code;")
            .ToDictionary(r => r["template_code"]!.ToString()!, r => r["email_template_id"]!.ToString()!);

        foreach (var (code, id) in before)
        {
            Assert.True(after.ContainsKey(code), $"row disappeared: {code}");
            Assert.Equal(id, after[code]);
        }
    }

    [Fact]
    public void Sync_overwrites_stale_canonical_content()
    {
        Sync();

        var subject = _db.Scalar(
            "SELECT subject_vi FROM email_templates WHERE template_code='ACCOUNT_EMAIL_CONFIRMATION';");
        Assert.NotNull(subject);
        Assert.DoesNotContain("STALE", subject!, StringComparison.OrdinalIgnoreCase);

        // The same row's variables_text was wrong too; the whole row converges, not just the subject.
        var vars = _db.Scalar(
            "SELECT variables_text FROM email_templates WHERE template_code='ACCOUNT_EMAIL_CONFIRMATION';");
        Assert.NotEqual("wrongVariable", vars);

        // A canonical template that had been switched off comes back on.
        Assert.Equal("ACTIVE",
            _db.Scalar("SELECT status FROM email_templates WHERE template_code='AUTH_PASSWORD_RESET_OTP';"));
    }

    // ── 4. What the sync must not touch ─────────────────────────────────────────────────────────

    [Fact]
    public void Sync_leaves_operator_authored_templates_alone()
    {
        var before = _db.Query(
            "SELECT template_code, status, subject_vi, body_vi, variables_text FROM email_templates " +
            "WHERE template_code LIKE 'CUSTOM_%' ORDER BY template_code;");
        Assert.Equal(2, before.Count);

        Sync();

        var after = _db.Query(
            "SELECT template_code, status, subject_vi, body_vi, variables_text FROM email_templates " +
            "WHERE template_code LIKE 'CUSTOM_%' ORDER BY template_code;");

        Assert.Equal(before.Count, after.Count);
        for (var i = 0; i < before.Count; i++)
            foreach (var key in before[i].Keys)
                Assert.Equal(before[i][key]?.ToString(), after[i][key]?.ToString());

        // Specifically: the ACTIVE one stays ACTIVE. Deactivating "everything not in the catalog" is
        // the shortcut this script refuses to take.
        Assert.Equal("ACTIVE",
            _db.Scalar("SELECT status FROM email_templates WHERE template_code='CUSTOM_CAMPUS_NEWSLETTER';"));
    }

    [Fact]
    public void Sync_leaves_history_and_everything_outside_email_templates_untouched()
    {
        static string NonTemplate(string snapshot) =>
            string.Join("\n", snapshot.Split('\n').Where(l => !l.StartsWith("TEMPLATE|", StringComparison.Ordinal)));

        var before = NonTemplate(_db.Snapshot());

        Sync();

        Assert.Equal(before, NonTemplate(_db.Snapshot()));
    }

    [Fact]
    public void Sync_preserves_the_body_snapshot_of_a_history_row_on_a_retired_template()
    {
        var before = _db.Scalar("SELECT body_snapshot FROM sent_emails WHERE sent_email_id=900001;");

        Sync();

        // The template it was sent from is now INACTIVE. What was sent is unchanged, and still linked.
        Assert.Equal(before, _db.Scalar("SELECT body_snapshot FROM sent_emails WHERE sent_email_id=900001;"));
        Assert.NotNull(_db.Scalar("SELECT email_template_id FROM sent_emails WHERE sent_email_id=900001;"));
    }

    // ── 5. Idempotency ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Second_sync_run_changes_nothing_at_all()
    {
        Sync();
        var afterFirst = _db.Snapshot();

        Sync();

        // Includes updated_at: a blanket UPDATE would move it on every row and churn the binlog even
        // though the content is already correct.
        Assert.Equal(afterFirst, _db.Snapshot());
    }

    [Fact]
    public void Second_sync_run_reports_zero_inserted_updated_and_deactivated()
    {
        Sync();
        Sync();

        // Read the same predicates the script uses, after it has converged: nothing left to do.
        Assert.Equal("0", _db.Scalar(@"
SELECT COUNT(*) FROM email_templates
WHERE template_code IN ('ACCOUNT_CREATED_INTERNAL','VISIT_REQUEST_APPROVED','VISIT_REQUEST_REJECTED',
  'VISIT_CANCELLED','HOST_ASSIGNMENT','VISIT_REQUEST_SUBMITTED_NOTIFY','LOGISTICS_REQUEST',
  'LOGISTICS_REQUEST_SUBMITTED_NOTIFY','OTP_VISIT_REQUEST') AND status <> 'INACTIVE';"));

        Assert.Equal("0", _db.Scalar(
            "SELECT COUNT(*) FROM email_templates WHERE template_code LIKE 'CUSTOM_%' AND status='INACTIVE' " +
            "AND template_code='CUSTOM_CAMPUS_NEWSLETTER';"));
    }

    // ── 5b. The generated catalog IS the canonical catalog ──────────────────────────────────────

    /// <summary>
    /// On a database imported straight from the canonical script and never touched, the sync must have
    /// nothing to do.
    ///
    /// <para>
    /// This is the assertion the suite was missing, and its absence is what let a real defect ship: the
    /// staged rows drifted from the seed by 29 fields across 14 templates — every one of them missing
    /// <c>{{contactInformationBlock}}</c>, plus one stale <c>variables_text</c> — and running the bundle
    /// would have STRIPPED the block from fourteen templates, five of which the reply-contact policy
    /// marks REQUIRED and which the renderer then refuses to send at all. Every other fact in this class
    /// passed throughout, because they all measure the sync against ITSELF: build a deployment that
    /// disagrees with the staged rows, sync, and assert the database now matches the staged rows. The
    /// one thing never compared was the staged rows against the seed they claim to be copied from.
    /// </para>
    /// <para>
    /// Its own pristine database, not the class fixture: the fixture is deliberately a messy deployment,
    /// and "the sync changes nothing" is only meaningful against a clean canonical import.
    /// </para>
    /// </summary>
    [Fact]
    public void Sync_changes_nothing_on_a_database_freshly_imported_from_the_canonical_script()
    {
        using var pristine = DisposableDatabaseManager.CreatePristineDatabase(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True");

        var name = System.Text.RegularExpressions.Regex
            .Match(pristine.ConnectionString, @"database=([^;]+)").Groups[1].Value;

        string Digest()
        {
            using var conn = new MySqlConnection(pristine.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT GROUP_CONCAT(line ORDER BY line SEPARATOR '\n') FROM (
  SELECT CONCAT_WS('|', template_code, status,
    SHA2(CONCAT_WS('', name, purpose, IFNULL(campus_id,'~'), IFNULL(description,'~'),
         IFNULL(subject_vi,'~'), IFNULL(body_vi,'~'), IFNULL(subject_en,'~'), IFNULL(body_en,'~'),
         body_format, IFNULL(variables_text,'~')), 256)) AS line
  FROM email_templates) x;";
            return cmd.ExecuteScalar()?.ToString() ?? "";
        }

        void RunSync()
        {
            using var conn = new MySqlConnection(pristine.ConnectionString);
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SET @pems_sync_confirm_database = '{name}';";
                cmd.ExecuteNonQuery();
            }
            new MySqlScript(conn, File.ReadAllText(
                Path.Combine(_db.ScriptsDirectory, "02_sync_templates.sql"))).Execute();
        }

        var imported = Digest();
        Assert.False(string.IsNullOrWhiteSpace(imported));

        RunSync();
        var afterFirst = Digest();
        Assert.Equal(imported, afterFirst);

        RunSync();
        Assert.Equal(afterFirst, Digest());
    }

    /// <summary>
    /// Every template the reply-contact policy marks REQUIRED carries the block in BOTH languages after
    /// the sync, and no template the policy says renders nothing gains one.
    ///
    /// <para>
    /// Checked on the database rather than on the file, because the body that matters is the one a send
    /// reads. <c>EmailTemplateRenderer</c> refuses a REQUIRED template whose body has nowhere to put the
    /// block, so a catalog that fails this is a catalog that cannot send those templates at all.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_required_contact_policy_has_its_block_in_both_languages_after_the_sync()
    {
        Sync();

        const string marker = "{{contactInformationBlock}}";
        var missing = new List<string>();
        var unexpected = new List<string>();

        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var row = _db.Query(
                $"SELECT body_vi, body_en FROM email_templates WHERE template_code='{code}';").Single();
            var vi = row["body_vi"]?.ToString() ?? "";
            var en = row["body_en"]?.ToString() ?? "";

            var policy = PEMS.Application.Emails.Contact.EmailContactPolicyDefaults.For(code);

            if (policy.Requirement == PEMS.Domain.Enums.EmailContactRequirement.REQUIRED)
            {
                if (!vi.Contains(marker, StringComparison.Ordinal)) missing.Add(code + ".body_vi");
                if (!en.Contains(marker, StringComparison.Ordinal)) missing.Add(code + ".body_en");
            }
            else if (!policy.RendersBlock &&
                     (vi.Contains(marker, StringComparison.Ordinal) || en.Contains(marker, StringComparison.Ordinal)))
            {
                unexpected.Add(code);
            }
        }

        Assert.True(missing.Count == 0,
            "REQUIRED reply-contact policy with no place to put the block: " + string.Join(", ", missing));
        Assert.True(unexpected.Count == 0,
            "the block was added to a template whose policy renders none: " + string.Join(", ", unexpected));
    }

    /// <summary>
    /// After the sync, a body writes <c>{{actionBlock}}</c> exactly where the registry declares an
    /// action — the database-side half of the guard
    /// <c>EmailTemplateContractTests.A_shipped_body_writes_the_action_block_exactly_when_the_registry_declares_one</c>
    /// makes over the shipped defaults.
    /// </summary>
    [Fact]
    public void The_synced_bodies_write_the_action_block_exactly_where_the_registry_declares_one()
    {
        Sync();

        const string marker = "{{actionBlock}}";
        var offenders = new List<string>();

        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var row = _db.Query(
                $"SELECT body_vi, body_en FROM email_templates WHERE template_code='{code}';").Single();
            var vi = (row["body_vi"]?.ToString() ?? "").Contains(marker, StringComparison.Ordinal);
            var en = (row["body_en"]?.ToString() ?? "").Contains(marker, StringComparison.Ordinal);
            var registered = EmailActionTemplates.For(code) is not null;

            if (vi != registered || en != registered)
                offenders.Add($"{code} (vi={vi}, en={en}, registry={registered})");
        }

        Assert.True(offenders.Count == 0,
            "body and action registry disagree: " + string.Join("; ", offenders));
    }

    // ── 6. The verify script is a real gate ─────────────────────────────────────────────────────

    [Fact]
    public void Verify_passes_after_the_sync()
    {
        Sync();
        Verify();   // its final SIGNAL throws when any check FAILs, so reaching here is the assertion
    }

    [Fact]
    public void Verify_fails_when_a_canonical_template_is_deactivated_behind_its_back()
    {
        Sync();
        _db.Execute("UPDATE email_templates SET status='INACTIVE' WHERE template_code='ACCOUNT_ACTIVATED';");

        var ex = Assert.ThrowsAny<MySqlException>(Verify);
        Assert.Contains("FAILED", ex.Message, StringComparison.OrdinalIgnoreCase);

        Sync();     // put it back so the next fact sees a converged database
        Verify();
    }

    [Fact]
    public void Verify_fails_when_a_retired_template_is_reactivated()
    {
        Sync();
        _db.Execute("UPDATE email_templates SET status='ACTIVE' WHERE template_code='LOGISTICS_REQUEST';");

        var ex = Assert.ThrowsAny<MySqlException>(Verify);
        Assert.Contains("FAILED", ex.Message, StringComparison.OrdinalIgnoreCase);

        Sync();
        Verify();
    }

    [Fact]
    public void Verify_fails_when_variables_text_stops_matching_the_body()
    {
        Sync();
        _db.Execute(
            "UPDATE email_templates SET variables_text='fullName, roleName, campusName, expiresInHours, ghostVariable' " +
            "WHERE template_code='ACCOUNT_EMAIL_CONFIRMATION';");

        var ex = Assert.ThrowsAny<MySqlException>(Verify);
        Assert.Contains("FAILED", ex.Message, StringComparison.OrdinalIgnoreCase);

        Sync();
        Verify();
    }

    // ── 7. The sync script cannot drift from the seed it syncs to ───────────────────────────────

    [Fact]
    public void Sync_script_carries_exactly_the_registered_codes()
    {
        var sql = File.ReadAllText(Path.Combine(_db.ScriptsDirectory, "02_sync_templates.sql"));

        // The staged VALUES rows each begin a line with ('CODE',
        var staged = System.Text.RegularExpressions.Regex
            .Matches(sql, @"^\s{2}\('([A-Z_0-9]+)',", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(SystemEmailTemplates.AllCodes.OrderBy(c => c, StringComparer.Ordinal),
                     staged.OrderBy(c => c, StringComparer.Ordinal));
    }

    [Fact]
    public void Sync_script_never_writes_a_numeric_template_id()
    {
        var sql = File.ReadAllText(Path.Combine(_db.ScriptsDirectory, "02_sync_templates.sql"));
        var body = string.Join("\n", sql.Split('\n').Where(l => !l.TrimStart().StartsWith("--")));

        Assert.DoesNotContain("email_template_id =", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email_template_id=", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sync_script_never_touches_history_reservations_or_tokens()
    {
        var sql = File.ReadAllText(Path.Combine(_db.ScriptsDirectory, "02_sync_templates.sql"));
        var body = string.Join("\n", sql.Split('\n').Where(l => !l.TrimStart().StartsWith("--")));

        foreach (var table in new[]
        {
            "sent_emails", "sent_email_recipients", "sent_email_attachments",
            "email_send_idempotency", "email_action_tokens", "files",
        })
        {
            foreach (var verb in new[] { "INSERT INTO", "UPDATE", "DELETE FROM" })
                Assert.DoesNotContain($"{verb} {table}", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Sync_script_deletes_nothing_anywhere()
    {
        var sql = await File.ReadAllTextAsync(Path.Combine(_db.ScriptsDirectory, "02_sync_templates.sql"));
        var body = string.Join("\n", sql.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("--"))
            // The staging table is a TEMPORARY table this script creates itself.
            .Where(l => !l.Contains("_pems_canonical_templates", StringComparison.Ordinal)));

        Assert.DoesNotContain("DELETE", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every script in the package declares its connection character set.
    ///
    /// <para>
    /// Found the hard way (G11): the sync script did not, and a run through the mysql CLI on Windows —
    /// which defaults to the console codepage, not UTF-8 — rewrote all thirty templates as mojibake
    /// ("Tài khoản" stored as "T├ái khoß║ún") and reported 30 rows updated on a database that was
    /// already converged. Nothing in this suite caught it, because these tests connect through
    /// MySql.Data, which is UTF-8 already; and a CLI snapshot taken before and after compares mangled
    /// text to mangled text and finds no difference.
    /// </para>
    /// <para>
    /// A file-text assertion rather than a behavioural one, deliberately: the behaviour only misfires
    /// under a client this suite does not use, so the only thing that can be checked from here is that
    /// the script does not depend on the client's default in the first place.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("01_preflight.sql")]
    [InlineData("02_sync_templates.sql")]
    [InlineData("03_verify.sql")]
    public void Every_script_sets_its_connection_character_set(string fileName)
    {
        var sql = File.ReadAllText(Path.Combine(_db.ScriptsDirectory, fileName));
        var body = string.Join("\n", sql.Split('\n').Where(l => !l.TrimStart().StartsWith("--")));

        Assert.Contains("SET NAMES utf8mb4", body, StringComparison.OrdinalIgnoreCase);
    }
}
