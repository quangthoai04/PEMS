using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Locates, verifies and retargets the ONE canonical PEMS schema script.
///
/// Every rule here is fail-closed by design. The previous bootstrap pointed at a filename that no longer
/// existed and wrapped the import in <c>if (File.Exists(...))</c> with no <c>else</c>, so a renamed script
/// silently produced an EMPTY disposable database and the suite "ran" against nothing.
///
/// It also only replaced the single literal <c>USE `pems_db`;</c>, while the script still contains
/// <c>CREATE DATABASE IF NOT EXISTS `pems_db`</c> — enough to create/touch a real database on a shared
/// MySQL server. Retargeting therefore rewrites EVERY database-selection statement and then re-scans the
/// produced text before anything is sent to the server.
/// </summary>
public static class CanonicalSqlScript
{
    /// <summary>The only accepted schema script. No wildcards, no fallback to historical names.</summary>
    public const string FileName =
        "PEMS_FULL_VS_31_07_NEW.sql";

    /// <summary>
    /// SHA-256 of the canonical script this test suite is written against. Changing the schema is allowed,
    /// but it MUST be a deliberate act: update this constant in the same commit as the .sql change so a
    /// silent drift between schema and tests is impossible.
    /// (2026-07-25) Bumped with the P0 #1 change: users.status ENUM + PENDING_EMAIL_CONFIRMATION and the new
    /// account_email_confirmations table. Also realigns FileName to the canonical file that commit ebf0d69a
    /// left on disk (the previous LATEST name had been removed, so the schema-contract check could not resolve).
    /// </summary>
    /// (2026-07-25, second bump) Re-pinned after commit 59c86766 appended the demo-data refresh block
    /// (deletes + re-inserts of the 3001-3090 / 5001-5160 demo ranges) to the canonical script. The schema
    /// DDL itself is unchanged; only the trailing data block is new, and retargeting still rewrites every
    /// database-selection statement, so a disposable run never touches pems_db.
    /// (2026-07-25, third bump) Re-pinned after 36e22105 ("Fix upload photo") rewrote the
    /// visit-photo uploader trigger body to admit Host/Staff/Admin/Participant instead of Students only.
    /// No table, column or trigger was added or removed, so ExpectedBaseTableCount/ExpectedTriggerCount
    /// are unchanged — this bump records a deliberate behaviour change inside an existing trigger.
    /// (2026-07-26, fourth bump) Re-pinned for the visit-mutation work, which changed the schema in
    /// two deliberate places and nothing else:
    ///   • both revision-history source_type ENUMs gained 'PENDING_EDIT', so a full edit of a pending
    ///     request stops being recorded as a quick edit (migration 2026_07_26_visit_pending_edit_source_type);
    ///   • trg_visit_campuses_assignment_validate_bu now admits a deliberate Host handover on an
    ///     already-decided, not-yet-started campus, and applies the host_assigned_by = decided_by rule
    ///     only while the decision is being made (migration 2026_07_26_visit_host_transfer).
    /// No table, column, index or trigger was added or removed, so ExpectedBaseTableCount (82) and
    /// ExpectedTriggerCount (32) are unchanged.
    /// (2026-07-26, fifth bump) SEED TEXT ONLY. The 15 approved-campus rows carried a decision note
    /// reading "Đã đối chiếu lịch tiếp đón, thành phần và nguồn lực campus … cho …", which reads as if the
    /// system had assessed the campus's resources — it is a human's approval note, typed in the approve
    /// dialog and stored verbatim in visit_request_campuses.decision_note. Replaced with the short factual
    /// "Campus {name} xác nhận tiếp nhận đoàn. Người phụ trách tiếp đón đã được phân công." No DDL, no
    /// trigger, no row count changed.
    /// (2026-07-26, sixth bump) EMAIL TEMPLATE CATALOG. Seed data only — no DDL, no trigger, and
    /// ExpectedBaseTableCount (82) / ExpectedTriggerCount (32) are both unchanged, verified by a fresh
    /// import before this constant was touched. What changed:
    ///   • the two legacy email_templates INSERT blocks (16 rows, hard-coded email_template_id 1..16)
    ///     are replaced by ONE canonical block of 26 rows that writes no id at all;
    ///   • the 16 follow-up "UPDATE … WHERE email_template_id = N" statements are gone, as is the later
    ///     patch that set content by template_code for codes the seed never contained
    ///     (VISIT_INVITATION, NEWS_REVIEW);
    ///   • the 25 seeded sent_emails rows keep their id, subject, body_snapshot, recipients, status and
    ///     provider/thread metadata, but their email_template_id becomes NULL, because every one of them
    ///     referenced a template that is no longer part of the catalog. Nothing was re-pointed at a
    ///     different template.
    /// (2026-07-26, seventh bump) FOUR ACCOUNT TEMPLATES, seed text only. Reconciling the catalog with
    /// behaviour the code already had:
    ///   • ACCOUNT_EMAIL_CHANGED_OLD_NOTICE and ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE now carry NO
    ///     variables. They go to the address that was just unlinked, which may belong to somebody who
    ///     mistyped their own — naming the account holder or the new address to them is a leak the
    ///     handlers deliberately avoided, and the first draft of this catalog would have introduced it;
    ///   • ACCOUNT_STAFF_LEADER_ASSIGNED and _REPLACED gained {{reason}}, which both emails already
    ///     showed and which is a required input of the replace-leader command.
    /// No DDL, no trigger, no row count changed.
    /// (2026-07-28, eighth bump) MERGE of Dev into Cảnh-Iter1. Two things happened at once, and this is
    /// the first bump where the FILE ITSELF changed identity:
    ///   • Dev renamed the canonical script to PEMS_FULL_V2_NO_SEED_DATA_GALLERY_DOCUMENT_AI_FIXED.sql
    ///     while Cảnh-Iter1 was editing the old name — a rename/modify conflict. The renamed file is the
    ///     canonical one; FileName above moves with it and the old name no longer exists on disk;
    ///   • the merged file carries BOTH sides: Dev's Document-AI/OCR fixes, Department-Leader personnel
    ///     support and logistics proposed_quantity/proposed_usage_* columns, and Cảnh-Iter1's whole email
    ///     schema (email_templates, sent_email*, email_draft*, email_action_tokens,
    ///     account_email_confirmations) plus the template catalog.
    /// Seed text also changed in two deliberate places:
    ///   • LOGISTICS_CHANGE_PROPOSAL_TO_HOST now renders the proposal ITSELF — proposed quantity against
    ///     the original, the proposed window, the proposed content — instead of the rationale alone, which
    ///     forced the Host into the portal to see what they were approving;
    ///   • four DEPT_* templates were added (personnel disabled/enabled, leadership granted/handed over)
    ///     when the Department-Leader module stopped composing its own HTML and moved onto the dispatcher.
    ///     The catalog is 30 codes, and the code-side registry is asserted equal to it in both directions.
    /// ExpectedBaseTableCount (82) and ExpectedTriggerCount (32) are unchanged, verified by a fresh import
    /// into a disposable database before this constant was touched.
    /// (2026-07-29, ninth bump) NOT A SCHEMA CHANGE — the .sql file is byte-for-byte the one the eighth
    /// bump pinned. What changed is the HASHING RULE, from raw file bytes to the line-ending-normalised
    /// text (see <see cref="ComputeNormalizedSha256"/>).
    ///   • old raw-byte hash, Windows CRLF checkout: 322a8a94c2dc61192e46d14769acb41af287c486b8e942fbf5850655702d68a0
    ///   • old raw-byte hash, Linux LF checkout:     18e97d4dce754353f5d19decc304c46f4d8f8dab3364d24ebdec9ba907e286b8
    /// Both were "correct"; the pin could satisfy one platform at a time and no more. Every local gate on
    /// this branch was green on Windows while CI was red on Linux for this single reason. The normalised
    /// hash equals the LF value, because the repository stores the file with LF — so this is not a third
    /// form, it is the form git has held all along.
    /// A fresh import was re-run against a disposable database after the change: 82 tables, 32 triggers,
    /// 252 foreign keys, 30 templates, 0 duplicate codes — identical to the eighth bump's baseline.
    /// (2026-07-29, tenth bump) G11 / R-103 — ONE new table, <c>email_send_idempotency</c>, so that a
    /// report/invoice send carries a persistent reservation and a retried request cannot become a second
    /// outbound message. Exactly three hunks, all of them that table:
    ///   • its <c>DROP TABLE IF EXISTS</c> in the reset list, before `sent_emails` (it is a child of it);
    ///   • the <c>CREATE TABLE</c> itself, after `account_email_confirmations`;
    ///   • the file's own <c>merged_runtime_table_count</c> assertion, 81 → 83. That assertion was ALREADY
    ///     wrong before G11: it read 81 while the script produced 82, and this file's own header comment
    ///     said 82, so every import reported a permanent issue_count of 1. It is corrected here rather
    ///     than left one further out of date.
    /// No seed row, no template, no trigger and no other table changed — verified by diffing the hunks.
    /// Measured on a fresh disposable import after the change: 83 tables, 32 triggers, 254 foreign keys,
    /// 30 templates, 22 historical sent_emails. A pre-G11 database migrated with
    /// <c>docs/database/scripts/email_dispatch_idempotency/02_up_additive.sql</c> was then compared to the
    /// fresh import column by column, index by index, constraint by constraint, with comments compared as
    /// raw bytes: identical.
    /// (2026-07-30, eleventh bump) G12 — contact-guard closure. NO schema change at all: not one table,
    /// column, index, constraint or seed row differs. The diff is confined to the five primary-contact
    /// guard triggers and the self-test procedure that measures them.
    ///
    /// The headline finding is that the guards were never broken. The import had reported
    /// <c>contact_guard_negative_failures = 14</c> — every negative case — and the reason was the
    /// measuring instrument, not the database. Each handler ran
    /// <c>SET v_raised = TRUE;</c> BEFORE <c>GET DIAGNOSTICS CONDITION 1</c>. MySQL clears the
    /// diagnostics area on the first successful statement inside a handler, so the subsequent read
    /// returned NULL for both RETURNED_SQLSTATE and MESSAGE_TEXT; <c>v_sqlstate = '45000'</c> then
    /// evaluated to UNKNOWN, every case scored FAIL, and the report printed the exact opposite of the
    /// truth: "Operation unexpectedly succeeded". A direct probe against the same database rejected the
    /// same statement with 45000 and the right message. Reordering those two statements in all 21
    /// handlers turned the counters to 0/0 with no trigger change whatsoever.
    ///
    /// Three genuine defects in the trigger bodies were then found by probing, and fixed:
    ///   • <c>v_user_status</c> was VARCHAR(20) while users.status is an ENUM whose longest member,
    ///     PENDING_EMAIL_CONFIRMATION, is 26 characters. A visitor in that state — the state every new
    ///     account starts in — made the guard raise <c>22001 Data too long</c> from inside the trigger.
    ///     The write was still refused, but with a storage error in place of the business code.
    ///   • roles was INNER JOINed, so a user whose role row could not be read collapsed COUNT(*) to 0
    ///     and was reported as PRIMARY_CONTACT_USER_NOT_FOUND — untrue, and it hides the real fault.
    ///   • <c>trg_users_protect_active_primary_contact_bu</c> compared a role code that a zero-row
    ///     <c>SELECT ... INTO</c> leaves NULL. <c>NULL &lt;&gt; 'VISITOR'</c> is UNKNOWN, which IF treats
    ///     as false, so on that path the guard silently stopped guarding.
    ///
    /// Five self-test cases were added for the paths none of the original 21 reached: NEG-15 (user id
    /// that does not exist), NEG-16 and NEG-18 (the unconfirmed-account state, on both the request and
    /// identity-change guards), NEG-17 (an UPDATE that writes visitor_user_id alone, leaving the access
    /// status untouched) and POS-08 (a visitor linked only to a CANCELLED request may still be
    /// deactivated — the documented exclusion, asserted so the guard cannot start over-blocking).
    ///
    /// Measured on a fresh disposable import after the change: 83 tables, 32 triggers, 254 foreign keys,
    /// 30 templates, 22 historical sent_emails — identical to the tenth bump's baseline, and
    /// contact_guard_negative_failures = 0, contact_guard_positive_failures = 0 across 18 negative and
    /// 8 positive cases. A pre-G12 database migrated with
    /// <c>docs/database/scripts/contact_guard_closure/02_up_replace_triggers.sql</c> was compared to the
    /// fresh import: all 32 trigger bodies identical as raw bytes, template content digest identical.
    ///
    /// ── Twelfth bump (G11 final closure) ────────────────────────────────────────────────────────
    ///
    /// One additive column: <c>email_templates.revision INT UNSIGNED NOT NULL DEFAULT 1</c>. Nothing else
    /// in the script changed — no table, no index, no trigger, no seed row.
    ///
    /// It exists because the optimistic-concurrency token for UC-44 was <c>updated_at</c>, and that column
    /// cannot do the job: it is DATETIME with no fractional part, so two saves inside the SAME second
    /// stored an identical stamp, compared equal, and the second silently overwrote the first. The blind
    /// spot sat exactly at the resolution where concurrent edits collide. Content writes now issue a
    /// conditional UPDATE carrying <c>AND revision = :expected</c> and bump the column in the same
    /// statement, so the database decides the winner and the loser writes nothing at all.
    ///
    /// The same column is what makes restore-to-default safe: a restore is a full content overwrite, and
    /// restoring over a colleague's unseen edit is the same lost update as saving over it.
    ///
    /// Measured on a fresh disposable import after the change: 83 tables, 32 triggers, 254 foreign keys,
    /// 30 templates all revision 1, and contact_guard_negative_failures = 0 /
    /// contact_guard_positive_failures = 0 — unchanged from the eleventh bump, as an additive column
    /// should leave them. A pre-G11FC database migrated with
    /// <c>docs/database/scripts/email_template_revision/02_up_add_revision.sql</c> was compared to the
    /// fresh import: identical column definition, identical row count, identical template content digest.
    ///
    /// ── Thirteenth bump (merge-loss restoration) ─────────────────────────────────────────────────
    ///
    /// NOT new work. Commit 6be02a28 ("visit amendment workflow") carried a different lineage of this
    /// script forward and, in doing so, silently dropped THREE pieces the tenth, eleventh and twelfth
    /// bumps had already landed. The pin above pointed at a file that no longer existed on disk, so the
    /// schema contract had been failing for exactly this reason. Restored verbatim from 9b9b2a71:
    ///   • <c>email_templates.revision</c> — the twelfth bump's column. Without it EVERY query EF issues
    ///     against email_templates fails with "Unknown column 'e.revision' in 'field list'", which took
    ///     out template rendering and therefore every outbound mail: creating a pending account reported
    ///     "chưa gửi được email xác nhận", and the resend endpoint returned a raw 500.
    ///   • <c>email_send_idempotency</c> — the tenth bump's table, together with its DROP in the reset
    ///     list and the <c>merged_runtime_table_count</c> assertion (which had reverted to 81).
    ///   • the eleventh bump's five contact-guard triggers and their self-test. The regression here was
    ///     live, not cosmetic: <c>v_user_status</c> was back to VARCHAR(20) while users.status can hold
    ///     PENDING_EMAIL_CONFIRMATION (26 chars), so the guards raised 22001 "Data too long" instead of
    ///     their business code — on the state every newly created account occupies. The reverted script
    ///     measured contact_guard_negative_failures = 14.
    ///
    /// Measured on a fresh disposable import after the restoration: 83 tables, 32 triggers, 30 templates
    /// all revision 1, contact_guard_negative_failures = 0, contact_guard_positive_failures = 0,
    /// merged_runtime_table_count issue_count = 0 — identical to the twelfth bump's baseline, which is
    /// the point: this bump restores a known-good state rather than establishing a new one.
    ///
    /// ── Fourteenth bump (setup-progress template, and a re-pin the rename had left broken) ──────
    ///
    /// Two separate things, and it matters which is which.
    ///
    /// FIRST, a repair that is not this change's doing. Commit 74deff85 ("new sql") replaced the
    /// canonical script with <c>PEMS_FULL_VS_31_07_NEW.sql</c> and left <see cref="FileName"/> pointing
    /// at a file no longer on disk, so <see cref="ResolvePath"/> threw and EVERY database-backed test in
    /// the suite failed before it could connect — not a hash mismatch reported as drift, but the schema
    /// contract unable to resolve at all. FileName now names the file that exists.
    ///
    /// SECOND, the deliberate content change: ONE seed row, <c>VISIT_SETUP_PROGRESS_UPDATE</c>
    /// (email_template_id 70031), the template behind the Host's "Gửi cập nhật chuẩn bị". Seed only —
    /// no table, column, index, constraint or trigger differs, and no existing template row is touched.
    /// The catalog goes 30 → 31, matching the code-side registry that the contract test compares it to
    /// in both directions.
    ///
    /// THIRD, four seed rows repaired — a live defect, not tidying. That same "new sql" commit rewrote
    /// the four REPORT_* templates to declare <c>reportTitle</c> and <c>periodLabel</c>, while their
    /// callers have always supplied <c>periodFrom</c>/<c>periodTo</c> (and <c>personName</c>/
    /// <c>scopeLabel</c> for the personnel report). The renderer compares the declared set against the
    /// supplied set and fails closed, so every campus-operation, department-collaboration, invoice and
    /// personnel-performance email threw
    /// <c>BusinessRuleException: thiếu giá trị cho biến: periodLabel, reportTitle</c> instead of
    /// sending — on a branch where those sends are Mandatory and not wrapped in try/catch. The four
    /// rows are restored to the registry's contract using the wording already shipped in
    /// <c>email-template-defaults.json</c>, which was authored for exactly those variables, so nothing
    /// here is newly invented. Measured after the repair: 31 seeded codes, 31 registry codes, and every
    /// one of the 31 declaring the same variable set on both sides.
    ///
    /// Still outstanding and NOT addressed here: the same commit also reworded the other twenty-six
    /// templates, so the canonical seed and <c>email-template-defaults.json</c> still differ in prose
    /// for those. That is a content decision per row rather than a contract break — they render, they
    /// send, and only "restore to default" would show the difference.
    ///
    /// (2026-08-02, ninth bump) SEED TEXT ONLY, one template. The VI and EN bodies of
    /// VISIT_SETUP_PROGRESS_UPDATE gained <c>{{setupSummaryBlock}}</c> — the placeholder for the setup
    /// tables (overview, guests, participants, schedule with the party in charge, preparation status)
    /// that the backend builds and injects at render time. It is a TRUSTED BLOCK, not a variable: it is
    /// absent from <c>variables_text</c> on purpose, so the 31-code / identical-variable-set contract
    /// this file records above still holds in both directions and needed no change. No DDL, no trigger,
    /// no row added or removed; ExpectedBaseTableCount (82) and ExpectedTriggerCount (32) are unchanged.
    ///
    /// (2026-08-02, tenth bump) SEED TEXT ONLY, the same one template. VISIT_SETUP_PROGRESS_UPDATE
    /// gained <c>{{hostEmail}}</c> in both bodies and in <c>variables_text</c>. Unlike the ninth bump
    /// this IS a variable, deliberately: the body asks the guest to reply so the Host can act, but the
    /// draft/manual send path carries the system's configured Reply-To and accepts no per-message one,
    /// so naming the Host without printing an address pointed the instruction nowhere. The registry,
    /// the JSON defaults and 02_sync_templates.sql declare the same six variables, so the
    /// identical-variable-set contract holds in both directions. Catalog size is still 31. No DDL, no
    /// trigger, no row added or removed; ExpectedBaseTableCount (82)/ExpectedTriggerCount (32) unchanged.
    ///
    /// (2026-08-02, eleventh bump) RECOVERY of two migrations this file lost. When the canonical script
    /// was replaced wholesale (commit 74deff85 "new sql"), the replacement was generated from a base
    /// that predated 2026-07-26, so two already-shipped migrations silently vanished from the schema
    /// while the code that depends on them stayed. Both were re-applied here from the migration files
    /// still in docs/database/scripts/migrations/, not retyped:
    ///   • 2026_07_26_visit_pending_edit_source_type — 'PENDING_EDIT' restored to the source_type ENUM
    ///     of visit_instance_form_revision_history and visit_request_revision_history. Without it every
    ///     full edit of a pending request died on "Data truncated for column 'source_type'" (HTTP 500),
    ///     because FormRevisionSourceTypes.PendingEdit is what the edit service writes.
    ///   • 2026_07_26_visit_host_transfer — trg_visit_campuses_assignment_validate_bu regains the
    ///     v_is_host_transfer exemption, so a deliberate handover on a decided, not-yet-started campus
    ///     is allowed while an incidental host change still raises the original SIGNAL. Without it every
    ///     host transfer died on "Official host cannot be changed after first assignment" (HTTP 500).
    /// ENUM widening and a trigger body only: no table, column, index, trigger or row added or removed,
    /// so ExpectedBaseTableCount (83) and ExpectedTriggerCount (32) are unchanged — verified by a fresh
    /// import (83 tables / 32 triggers / 255 FKs / 31 templates) before this constant was touched.
    ///
    /// (2026-08-02, twelfth bump) SEED TEXT ONLY, four templates. VISIT_DEPARTMENT_STAFF_ASSIGNMENT,
    /// VISIT_REMINDER_HOST, VISIT_REMINDER_PARTICIPANTS and LOGISTICS_EXPENSE_REPORT_REMINDER each gained
    /// a trailing <c>{{actionBlock}}</c> in body_vi and body_en.
    ///
    /// All four senders were ALREADY building a trusted block and passing it — the department assignment
    /// mints real accept/decline tokens, the other three build a login-required link from
    /// App:FrontendBaseUrl — but no body had the placeholder, so the block was assembled and then
    /// silently dropped at render time. The assignment mail in particular reached its recipient with the
    /// two buttons it asks them to press missing entirely.
    ///
    /// The block is trusted markup, not a variable, so <c>variables_text</c> is deliberately unchanged
    /// (the nine templates that already carried the placeholder list it nowhere either, and 03_verify's
    /// E1/E2 checks pass on that basis). Catalog size is still 31. No DDL, no trigger, no row added or
    /// removed; ExpectedBaseTableCount (83)/ExpectedTriggerCount (32) unchanged.
    ///
    /// (2026-08-02, thirteenth bump) SEED CONTENT + REMOVAL OF TWO DEAD BLOCKS. Two changes, both
    /// seed-only.
    ///
    /// 1. CONTENT OWNERSHIP RESOLVED. The seed and email-template-defaults.json had disagreed on all
    ///    six text columns for 26 of the 31 templates since before this branch — the seed carrying
    ///    terse one-line placeholders, the JSON the full wording. The product owner's decision
    ///    (2026-08-02) is that the JSON holds the authoritative content, so those 26 rows were brought
    ///    up to it and 02_sync_templates.sql was regenerated from the resulting seed. One-directional:
    ///    JSON → seed → sync script. The four {{actionBlock}} placeholders added in the twelfth bump
    ///    survive because the JSON carries them too; ACCOUNT_ACTIVATED gains one for the same reason
    ///    those four did — its sender has always passed EmailComposition.LoginBlock, and without the
    ///    placeholder the sign-in button was built and then dropped at render time.
    ///
    /// 2. TWO DEAD SEED BLOCKS DELETED. Section C seeded sent_emails 99101-99108 plus their
    ///    recipients, and section E seeded the 15 email_action_tokens that point at them. Both sit
    ///    BEFORE the R0 catalog rebuild, which runs unconditional DELETEs against sent_emails,
    ///    sent_email_recipients and email_action_tokens and then re-seeds 31 messages and 39 tokens.
    ///    Neither block had ever reached an imported database; they existed only to be deleted. They
    ///    are removed as a unit because every section E row is an FK child of a section C row.
    ///
    /// Verified by a fresh import before this constant was touched: 83 tables / 32 triggers / 255 FKs
    /// / 31 templates, 31 sent_emails, 31 recipients, 39 action tokens — the sent_emails id set is
    /// byte-identical to the pre-change import (64001..64031), proving only never-surviving rows went.
    ///
    /// FOURTEENTH BUMP — the contact-policy block is moved OUT of the email_templates INSERT.
    ///
    /// The thirteenth bump pinned a script that could not be imported at all. The two
    /// INSERT INTO email_contact_policies statements had been pasted INTO the middle of row 70030's
    /// body_vi string literal, splitting the 31-row INSERT in half; MySQL stops at
    /// `ERROR 1064 ... near 'TEMPLATE', 'ACCOUNT_EMAIL_CONFIRMATION'` and every statement after it —
    /// the catalog rebuild, the policy seed, the closing ALTERs — never runs. A fresh import was
    /// therefore left holding the 16-template demo catalog from section 7, which is exactly what the
    /// developer database turned out to contain.
    ///
    /// This hash pinned that state, so the constant was not a guard here: it agreed with a file no
    /// database could accept. Nothing was lost in the repair and nothing was rewritten — the block is
    /// relocated to after the INSERT's terminator, and the repaired file is a character-for-character
    /// permutation of the pinned one.
    ///
    /// Verified by fresh import against MySQL 8.0.46 AFTER this constant was touched: exit 0,
    /// 31 email_templates, 32 email_contact_policies (31 TEMPLATE + 1 SYSTEM), and all 31 template
    /// bodies byte-identical to email-template-defaults.json.
    public const string ExpectedSha256 =
        "b7c431d4d3a76dab04aa8045653c2aae4a087780a2adca53d9ae747243d285ff";

    /// <summary>The database name the canonical script targets by default — never usable from tests.</summary>
    private const string ForbiddenTargetDatabase = "pems_db";

    /// <summary>Disposable databases must match this shape before we ever create or drop one.</summary>
    public static readonly Regex DisposableNamePattern =
        new(@"^pems_test_run_[0-9a-fA-F]{32}$", RegexOptions.Compiled);

    /// <summary>Walks up from the test binaries to the repository root (the folder holding backend/ and tests/).</summary>
    public static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null &&
               !(Directory.Exists(Path.Combine(dir.FullName, "backend")) &&
                 Directory.Exists(Path.Combine(dir.FullName, "tests"))))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not locate the repository root from '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    /// Resolves the canonical script path. Throws when it is missing, or when more than one candidate
    /// schema script exists in the scripts folder (ambiguity must never be resolved by guessing).
    /// </summary>
    public static string ResolvePath(string? repositoryRoot = null)
    {
        var root = repositoryRoot ?? FindRepositoryRoot();
        var scriptsDir = Path.Combine(root, "docs", "database", "scripts");

        if (!Directory.Exists(scriptsDir))
            throw new FileNotFoundException($"Canonical SQL folder not found: {scriptsDir}");

        // Only top-level scripts count; patches/migrations live in sub-folders.
        var candidates = Directory
            .GetFiles(scriptsDir, "PEMS_FULL_*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (candidates.Count == 0)
            throw new FileNotFoundException(
                $"No canonical schema script found in '{scriptsDir}'. Expected '{FileName}'.");

        if (candidates.Count > 1)
            throw new InvalidOperationException(
                "Ambiguous canonical schema: multiple PEMS_FULL_*.sql scripts exist in " +
                $"'{scriptsDir}' ({string.Join(", ", candidates.Select(Path.GetFileName))}). " +
                "Exactly one canonical script must be present.");

        var path = candidates[0];
        if (!string.Equals(Path.GetFileName(path), FileName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Canonical schema filename changed. Expected '{FileName}' but found " +
                $"'{Path.GetFileName(path)}'. Update {nameof(CanonicalSqlScript)} deliberately.");

        return path;
    }

    /// <summary>UTF-8 without a BOM, and strict: a decoding error must fail loudly, not produce U+FFFD.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Canonicalises the script text so that the SAME SQL yields the SAME hash on every platform.
    ///
    /// <para>
    /// Exactly three things are neutralised, all of them artefacts of how a file was checked out rather
    /// than of what it says: a leading BOM, CRLF line endings, and lone CR line endings. Nothing else is
    /// touched — no trimming, no whitespace collapsing, no Unicode normalisation — because every one of
    /// those would let a real content change slip through the guard. A space added inside a SIGNAL message
    /// or a trailing space after a column definition still changes the hash, exactly as it should.
    /// </para>
    /// </summary>
    public static string NormalizeForHashing(string text)
    {
        // Exactly one leading BOM, and only at position 0. A U+FEFF anywhere else is real content.
        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text[1..];

        // CRLF first so the second pass only sees genuinely lone CRs (old-Mac endings).
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    /// <summary>Lower-case hex SHA-256 of the line-ending-normalised text, encoded UTF-8 without a BOM.</summary>
    public static string ComputeNormalizedSha256OfText(string text) =>
        Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(NormalizeForHashing(text)))).ToLowerInvariant();

    /// <summary>
    /// Lower-case hex SHA-256 of the canonical script, independent of how it was checked out.
    ///
    /// <para>
    /// This replaces a raw-byte hash of the file. The repository stores this script with LF (<c>.gitattributes</c>
    /// declares <c>* text=auto</c>), so a Windows worktree holds it with CRLF and a Linux runner holds it with LF —
    /// the same 1.7 MB of SQL, two different SHA-256 values. A raw-byte pin can therefore be green on one platform
    /// only, which is precisely how CI first went red on a branch whose every local gate passed. Hashing what the
    /// file SAYS rather than how its lines happen to end is what makes the schema contract portable.
    /// </para>
    /// </summary>
    public static string ComputeNormalizedSha256(string path) =>
        ComputeNormalizedSha256OfText(StrictUtf8.GetString(File.ReadAllBytes(path)));

    /// <summary>
    /// Reads the canonical script, failing when its hash does not match <see cref="ExpectedSha256"/>.
    ///
    /// <para>
    /// Returns the NORMALISED text, which is also the text that was hashed. That matters beyond tidiness:
    /// two of this script's CRLFs fall INSIDE string literals — the seeded <c>note_to_fptu</c> values for
    /// visit requests 1002 and 3048/3049, each built with <c>CONCAT</c> around an embedded newline — and
    /// <c>MySqlScript</c>, which <see cref="DisposableDatabaseManager"/> imports through, passes them to the
    /// server unchanged (measured, not assumed). So before this change the suite's disposable database held
    /// those two rows with <c>\r\n</c> on Windows and <c>\n</c> on Linux. Two rows is a small discrepancy,
    /// but it is exactly the kind that survives for years: it lives in seed data nobody diffs, on a path
    /// where the <c>mysql</c> command-line client — which strips CR line by line, so manual and CI imports
    /// never showed it — behaves differently from the driver.
    /// Feeding the normalised text to the import makes a disposable database byte-identical wherever the
    /// suite runs, and keeps "what was verified" and "what was imported" the same string.
    /// </para>
    /// </summary>
    public static string ReadVerified(string? repositoryRoot = null)
    {
        var path = ResolvePath(repositoryRoot);
        var actual = ComputeNormalizedSha256(path);

        if (!string.Equals(actual, ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Canonical SQL hash mismatch for '{Path.GetFileName(path)}'.{Environment.NewLine}" +
                $"  expected normalized SHA-256: {ExpectedSha256}{Environment.NewLine}" +
                $"  actual   normalized SHA-256: {actual}{Environment.NewLine}" +
                "This hash ignores line endings and a leading BOM, so a CRLF/LF checkout difference cannot " +
                "cause it. The script's content really did change." + Environment.NewLine +
                $"If the schema changed on purpose, update {nameof(CanonicalSqlScript)}.{nameof(ExpectedSha256)} " +
                "in the same commit.");

        return NormalizeForHashing(StrictUtf8.GetString(File.ReadAllBytes(path)));
    }

    /// <summary>
    /// Rewrites every database-selection statement onto <paramref name="targetDatabase"/> and then proves,
    /// by re-scanning the produced text, that nothing can still reach a real database.
    /// </summary>
    public static string Retarget(string sql, string targetDatabase)
    {
        if (!DisposableNamePattern.IsMatch(targetDatabase))
            throw new InvalidOperationException(
                $"Refusing to retarget onto '{targetDatabase}': it is not a disposable pems_test_run_<32hex> name.");

        // CREATE/DROP/USE DATABASE `x` | 'x' | x  →  the disposable target.
        var rewritten = Regex.Replace(
            sql,
            @"(?im)^[ \t]*(CREATE\s+DATABASE(?:\s+IF\s+NOT\s+EXISTS)?|DROP\s+DATABASE(?:\s+IF\s+EXISTS)?|USE)\s+[`'""]?[A-Za-z0-9_$]+[`'""]?",
            m => $"{m.Groups[1].Value} `{targetDatabase}`");

        AssertSafeToImport(rewritten, targetDatabase);
        return rewritten;
    }

    /// <summary>
    /// Matches a database-selection statement only where MySQL can actually parse one: at the start of a
    /// statement, i.e. the start of a line or immediately after a <c>;</c>. This mirrors <see cref="Retarget"/>,
    /// which rewrites exactly those positions. An unanchored search instead flagged the ordinary English word
    /// "use" inside string literals and SIGNAL messages ("...must use SELF_SERVICE source"), which no server
    /// ever executes. MySQL rejects USE mid-statement and inside stored programs, so anchoring loses no real
    /// reachability.
    /// </summary>
    private static readonly Regex DatabaseStatementPattern = new(
        @"(?im)(?:^|;)[ \t]*(CREATE\s+DATABASE|DROP\s+DATABASE|USE)\b\s+(?:IF\s+(?:NOT\s+)?EXISTS\s+)?[`'""]?([A-Za-z0-9_$]+)[`'""]?",
        RegexOptions.Compiled);

    /// <summary>
    /// Matches the mysql client include directives (<c>SOURCE path</c> and its <c>\.</c> shorthand). The
    /// argument must look like a path, because a bare SOURCE word also begins a legitimate column definition
    /// in this schema (<c>source ENUM('MANUAL','OCR',...)</c>).
    /// </summary>
    private static readonly Regex ClientIncludePattern = new(
        @"(?i)^\s*(?:SOURCE\s+|\\\.\s*)\S*(?:[\\/]|\.sql\b)",
        RegexOptions.Compiled);

    /// <summary>
    /// Post-retarget safety gate. Throws when the script could still touch a protected database or pull in
    /// another file. Comment lines are ignored: the canonical script legitimately mentions pems_db in prose.
    /// </summary>
    public static void AssertSafeToImport(string sql, string targetDatabase)
    {
        var offenders = new List<string>();
        var lines = sql.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("--", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            // Any database-selection statement must name the disposable target.
            foreach (Match dbStatement in DatabaseStatementPattern.Matches(line))
            {
                if (!string.Equals(dbStatement.Groups[2].Value, targetDatabase, StringComparison.Ordinal))
                    offenders.Add($"line {i + 1}: database statement targets '{dbStatement.Groups[2].Value}'");
            }

            // Qualified references to the forbidden database, e.g. `pems_db`.users
            if (Regex.IsMatch(line, $@"(?i)[`'""]?\b{ForbiddenTargetDatabase}\b[`'""]?\s*\."))
                offenders.Add($"line {i + 1}: qualified reference to '{ForbiddenTargetDatabase}'");

            // Client-side include directives must never appear.
            if (ClientIncludePattern.IsMatch(line))
                offenders.Add($"line {i + 1}: client include directive");
        }

        if (offenders.Count > 0)
            throw new InvalidOperationException(
                "Refusing to import: the retargeted script can still reach a database outside the disposable " +
                $"target '{targetDatabase}'.{Environment.NewLine}  " +
                string.Join(Environment.NewLine + "  ", offenders.Take(20)));
    }

    /// <summary>Generates a fresh disposable database name matching the allowlist.</summary>
    public static string NewDisposableDatabaseName() => "pems_test_run_" + Guid.NewGuid().ToString("N");
}
