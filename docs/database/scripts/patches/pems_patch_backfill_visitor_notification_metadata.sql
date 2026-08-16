-- =============================================================================
-- PRODUCTION-SAFE, DATA-DRIVEN BACKFILL: metadata_json (eventKey + params) for
-- historical VISITOR notifications, for every event the semantic notification
-- architecture currently supports.
-- =============================================================================
--
-- STATUS: every rule below was verified against the CURRENT producer code on
-- branch Canh_iter3_FixBug (backend/PEMS.Application/Notifications/Common/
-- NotificationEventKeys.cs + the 9 producer files that call BuildMetadata, plus
-- frontend/pems-react/src/features/notifications/utils/resolveNotificationPresentation.ts
-- for the exact KNOWN_EVENT_KEYS + required params). It was NOT verified against
-- a real production database — no such access exists from this environment.
-- Every claim about row counts/ids in a prior version of this file described ONE
-- DEV SNAPSHOT (pems_db, 2026-08-16) and is illustrative only. This script makes
-- NO assumption that production has the same counts, the same reconstructable
-- rows, or even the same set of notification_type strings. SECTION 1 below is
-- the authoritative precheck — run it on production first and read its output
-- before trusting anything else in this file.
--
-- =============================================================================
-- VERIFIED EVENTKEY MAPPING (NotificationType -> eventKey -> params -> source)
-- =============================================================================
-- notification_type is NOT a safe backfill key on its own: several of the types
-- below are reused by multiple, semantically different producers (confirmed by
-- reading every RecipientUserId call site under backend/PEMS.Application). Each
-- rule in Section 2/3 therefore filters on notification_type AND role=VISITOR
-- AND, where the type is shared with a non-Visitor producer, an extra
-- disambiguating signal (dedupe_key prefix or an exact Title string that is
-- unique to one producer, grep-verified below) — never on notification_type
-- alone.
--
--   VISIT_REQUEST_APPROVED -> CAMPUS_APPROVED
--     params: campusName (campuses.name via n.campus_id),
--             requestCode (visit_requests.request_code via n.visit_request_id),
--             hostName (see HOST NOTE below)
--     producer: CampusApprovalExecutor.cs BuildAndSendAsync/GuestSideRecipients loop.
--     Not shared with any other producer for a VISITOR recipient (the same type's
--     HO-visibility rows in the same method target role=HO only).
--
--   VISIT_REQUEST_REJECTED -> CAMPUS_REJECTED
--     params: campusName, requestCode, reason (see REASON NOTE below)
--     producer: RejectCampusInstanceCommandHandler.cs, GuestSideRecipients loop.
--     Not shared with any other producer for a VISITOR recipient.
--
--   VISIT_STATUS_CHANGED -> FEEDBACK_INVITE_VISITOR
--     params: requestCode only
--     producer: CompleteVisitStageCommandHandler.cs, AfterVisit transition.
--     VISIT_STATUS_CHANGED IS heavily shared (HO campus-decision visibility,
--     SubmitVisitFeedbackCommandHandler's Host-facing feedback-received alert,
--     TransferVisitHostCommandHandler's host-changed alert — see GAPS below).
--     Disambiguated by dedupe_key LIKE 'FEEDBACK_INVITE_VISITOR\_%' (ESCAPE'd
--     underscore), which only this producer ever writes (grep-verified unique).
--
--   VISIT_CLOSED -> VISIT_CLOSED
--     params: requestCode only
--     producer: CompleteVisitStageCommandHandler.cs, Closed transition.
--     Type is not used by any other producer codewide (grep-verified) — safe to
--     key on notification_type + role=VISITOR alone.
--
--   VISIT_CANCELLED -> VISIT_CANCELLED_BY_HOST
--     params: campusName, requestCode
--     producer: CancelVisitRequestCommandHandler.cs, "Host cancelled" branch,
--     GuestSideRecipients loop. VISIT_CANCELLED is used by 4 branches in the
--     SAME file (guest-cancel -> Host/StaffLeader/HO, host-cancel ->
--     guest/StaffLeader) but only the host-cancel -> guest branch ever targets a
--     VISITOR-role recipient (every other branch targets Host/StaffLeader/HO,
--     confirmed by reading all 6 call sites) — role=VISITOR alone is already an
--     unambiguous filter for this one type.
--
--   VISIT_REQUEST_SUBMITTED -> OPCONTACT_TRANSFER_FROM / OPCONTACT_TRANSFER_TO
--     params: campusLabel (campuses.name via n.visit_instance_id — see CAMPUS
--             VIA INSTANCE NOTE below), requestCode
--     producer: OperationalContactNotifier.cs AnnounceTransferAppliedAsync.
--     VISIT_REQUEST_SUBMITTED is the MOST overloaded type in the codebase
--     (9+ distinct producers, all "tell Staff a request needs review" alerts
--     that never target a Visitor, EXCEPT this one and AMENDMENT_* below).
--     Disambiguated by an exact Title match unique to this producer (grep-
--     verified across the whole backend): "Vai trò đầu mối vận hành đã được
--     chuyển giao" (FROM) / "Chuyển giao đầu mối vận hành đã hoàn tất" (TO).
--     This is a PRODUCER-IDENTITY classification key, not a data extraction —
--     see PROSE-MATCHING NOTE below.
--
--   VISIT_REQUEST_SUBMITTED -> AMENDMENT_APPROVED / AMENDMENT_REJECTED
--     params: none (both events render a fixed sentence, no interpolation)
--     producer: VisitAmendmentHandlers.cs NotifyDecisionAsync.
--     Same overloaded type as above. Disambiguated by an exact Title match
--     unique to this producer (grep-verified): "Đề xuất thay đổi đã được xử
--     lý". Approved-vs-rejected is read from visit_instance_amendments.status
--     (a structured column — APPROVED/REJECTED), matched to the right amendment
--     row by visit_instance_id + requested_by + a decided_at/created_at
--     timestamp window, exactly like the CAMPUS_REJECTED reason lookup below.
--     The Title is used only to identify WHICH producer created the row, never
--     to derive approved-vs-rejected. It is a SECONDARY belt-and-suspenders
--     check on top of the primary structural join (visit_instance_id +
--     requested_by + timestamp) — see PROSE-MATCHING NOTE below for why it was
--     not removed outright.
--
--   VISIT_STATUS_CHANGED -> HOST_CHANGED
--     params: campusName, requestCode, hostName (the NEW host, AT THE TIME of
--             this specific transfer — see below)
--     producer: TransferVisitHostCommandHandler.cs NotifyAfterCommitAsync, the
--     `if (visitorUserId is { } visitor)` branch — the campus's own operational
--     contact, told a different Host now runs their campus. Same overloaded
--     type as FEEDBACK_INVITE_VISITOR; disambiguated by its own Title
--     ("Host phụ trách chuyến thăm đã thay đổi", grep-verified unique to this
--     producer) since this producer sets no DedupeKey. campusName/requestCode
--     come straight off the notification's own campus_id/visit_request_id FK
--     (both ARE set here, unlike OPCONTACT_TRANSFER_*). hostName does NOT read
--     current_host_user_id (CURRENT state — wrong if transferred again since):
--     same historical-reconstruction discipline as CAMPUS_APPROVED, joined to
--     the audit_log_changes row the SAME handler writes in the SAME
--     transaction — action='HOST_TRANSFERRED', field_name='currentHostName',
--     new_value_text is the incoming host's name at THAT transfer (grep-
--     verified at TransferVisitHostCommandHandler.cs's audit.Changes.Add
--     calls). A row with no matching audit entry is NON_RECONSTRUCTABLE.
--
--   ACCOUNT_STATUS_CHANGED -> ACCOUNT_LOCKED / ACCOUNT_UNLOCKED
--     params: none (both events render a fixed sentence, no interpolation; the
--     admin's lock/unlock Reason is deliberately never included — the ORIGINAL
--     VI Message never showed it either, and it can be a sensitive security
--     note)
--     producer: ManageAccountStatusCommandHandler.cs, ADMIN-only SecurityAction.
--     Lock/Unlock branches (never the HO/Staff-Leader business-status branch,
--     which structurally excludes VISITOR from targetInScope — see ACCOUNT
--     GAPS CORRECTION below). Disambiguated by joining audit_logs on
--     entity_type='User' AND entity_id=recipient_user_id AND action IN
--     ('SECURITY_LOCK_ACCOUNT','SECURITY_UNLOCK_ACCOUNT'), a structured signal
--     (not Title/Message parsing) — this exact Action pair is written by this
--     exact handler (grep-verified) in the SAME transaction as the
--     notification.
--
-- UNIQUENESS-CANDIDATE NOTE (raised in review — an earlier version of this
-- script's JOINs did not guard against this):
--   A plain INNER JOIN that matches MORE THAN ONE candidate row per
--   notification (e.g. two audit_logs rows both within the timestamp window,
--   or two visit_instance_amendments rows) would still let a multi-table
--   UPDATE run — MySQL applies it once per matched pair, so the LAST match
--   silently wins with no error. Section 3 guards every JOIN whose target
--   table can hold more than one row per (visit_instance_id) — audit_logs
--   (CAMPUS_APPROVED, ACCOUNT_LOCKED/UNLOCKED) and visit_instance_amendments
--   (AMENDMENT_*) — with an explicit `(SELECT COUNT(*) ... ) = 1` condition, so
--   an ambiguous case is EXCLUDED (left NON_RECONSTRUCTABLE) rather than
--   resolved by undefined join-order luck. visit_request_campuses
--   (CAMPUS_REJECTED) and campuses/visit_requests (everywhere else) are 1:1 or
--   N:1 on their join keys by schema — no uniqueness guard is needed for them.
--
-- ACCOUNT GAPS CORRECTION (2026-08-17, supersedes an earlier claim in this
-- file's history):
--   A prior investigation read AccountProvisioningRules.ResolveAsync's
--   `case RoleCodes.Visitor:` branch in isolation and concluded
--   CreateAccountCommandHandler's ACCOUNT_CREATED notification was
--   Visitor-reachable. Tracing every caller of that method disproves it:
--   CreateAccountCommandHandler pre-gates an HO caller to targetRole IN
--   ('HO','STAFF') before ResolveAsync is ever invoked, and a Staff Leader
--   caller goes through a SEPARATE method (ResolveStaffLeaderTargetAsync)
--   whose switch has no VISITOR case at all (default throws). The only caller
--   that CAN pass an arbitrary NewRoleCode through to the Visitor case
--   (UpdateAccountRoleCommandHandler, an existing-account role CHANGE, not a
--   creation) has no INotificationService dependency at all — it sends email,
--   never an in-app notification. ACCOUNT_CREATED is therefore NOT backfilled
--   here: there is no live path that could have produced a Visitor-recipient
--   ACCOUNT_CREATED row, historical or future. (The producer code still
--   carries a defensive, currently-dead MetadataJson branch for it — harmless,
--   not a gap.)
--
-- HOST NOTE (CAMPUS_APPROVED hostName):
--   The host assigned by an approval decision is NOT necessarily the row's own
--   actor_user_id (the approver) — a Staff Leader can approve and assign a
--   DIFFERENT person as host. Do not do what an earlier version of this script
--   did (JOIN users ON actor_user_id, treat the approver as the host — wrong
--   whenever they differ). CampusApprovalExecutor.cs writes the real assigned
--   host into audit_logs.reason as literally 'decision=ASSIGNED;host={id}' in
--   THE SAME transaction as the notification (grep-verified at
--   CampusApprovalExecutor.cs, Action LIKE 'APPROVE_CAMPUS_INSTANCE%'), so
--   joining to that specific audit_logs row (matched by visit_instance_id +
--   Action + a tight timestamp window) reconstructs the host AS IT WAS AT
--   APPROVAL TIME — correct even if the host was transferred to someone else
--   afterward (TransferVisitHostCommandHandler never rewrites this audit_logs
--   row). A row with no matching audit_logs entry (older code path, or the
--   audit row was for some reason never written) is left NON_RECONSTRUCTABLE —
--   never guessed from current_host_user_id, which is CURRENT state, not
--   historical.
--
-- REASON NOTE (CAMPUS_REJECTED reason):
--   visit_request_campuses.decision_note holds only the LATEST decision on that
--   instance — a campus can be rejected, resubmitted, and rejected again, and
--   the column is overwritten each time (confirmed: RejectCampusInstanceCommand
--   Handler writes instance.DecisionNote = reason with no history table). An
--   EARLIER rejection notification's reason is only safely recoverable when the
--   CURRENT decision_note still belongs to THAT SAME decision — verified by
--   comparing visit_request_campuses.decided_at to the notification's
--   created_at within a tight window. If a later decision has since overwritten
--   it, the timestamps will not match and the row is correctly left
--   NON_RECONSTRUCTABLE rather than backfilled with a newer rejection's reason.
--
-- CAMPUS VIA INSTANCE NOTE:
--   OperationalContactNotifier.cs's AnnounceTransferAppliedAsync does not set
--   CampusId on the CreateNotificationRequest (grep-verified) — only
--   VisitInstanceId. So n.campus_id is NULL for these rows; campusLabel must be
--   joined via n.visit_instance_id -> visit_request_campuses.campus_id ->
--   campuses.name, not via n.campus_id directly.
--
-- PROSE-MATCHING NOTE (Title/Message use, and why it is not the forbidden kind):
--   Two event pairs above (OPCONTACT_TRANSFER_*, AMENDMENT_*) share an
--   overloaded notification_type with no DedupeKey to disambiguate, so an exact,
--   grep-verified-unique Title string is used to identify WHICH producer wrote
--   the row. This is a producer-identity lookup, not "parse the Vietnamese
--   sentence to invent a param value": every param value backfilled below still
--   comes from a relational column (campuses.name, visit_requests.request_code,
--   visit_instance_amendments.status), never from substring/regex extraction
--   out of Title/Message. AMENDMENT_APPROVED/REJECTED in particular have ZERO
--   params — the Title match only decides which of two fixed, pre-translated
--   sentences to render, and even that decision is confirmed against the
--   structured amendment status column, not the Vietnamese text.
--
-- SCHEMAVERSION DECISION:
--   NOT added. The shipped metadata shape is `{"eventKey":"...","params":{...}}`
--   (backend/PEMS.Application/Notifications/Common/NotificationEventKeys.cs
--   BuildMetadata) and the frontend parser (resolveNotificationPresentation.ts
--   parseEvent) does not read or require a schemaVersion field. Adding one here
--   would touch BuildMetadata + parseEvent together, which is a producer/
--   frontend code change, out of scope for a SQL-only pass. If ever needed,
--   add it additively in a change that touches both sides at once, not as a
--   silent SQL-only convention.
--
-- =============================================================================
-- KNOWN GAPS — all three CODE-LEVEL gaps this file's history flagged are now
-- CLOSED in producer code (HostChanged/AccountLocked/AccountUnlocked eventKeys
-- shipped, ACCOUNT_CREATED corrected to non-reachable — see ACCOUNT GAPS
-- CORRECTION above). None remain open as of this revision.
-- =============================================================================
-- Also NOT real historical data (confirmed dead): notification_type values
-- 'VISIT_APPROVED', 'VISITOR_CANCEL_CONFIRMED', 'VISIT_STILL_ACTIVE' appear
-- ONLY as literal strings in dev/demo seed SQL (docs/database/scripts/
-- PEMS_FULL_VS_31_07_NEW.sql and phase_1_candidate/00_fresh_target.sql),
-- grep-verified absent from every current NotificationConstants.cs value and
-- every live producer file. If production somehow contains rows with these
-- types (e.g. this demo seed was ever run against it), Section 1's precheck
-- will surface them under "missing_metadata" with no matching rule in Section
-- 2/3 — do not invent a reconstruction rule for them without first confirming,
-- on production, where they actually came from.
--
-- =============================================================================
-- SAFETY & IDEMPOTENCY
-- =============================================================================
--   * Every UPDATE in Section 3 is scoped by `metadata_json IS NULL` — a second
--     run touches zero rows once applied, and a row already enriched by the
--     live producer code (new notifications created after this refactor
--     shipped) is never touched.
--   * Every UPDATE only SETs metadata_json. No other column (recipient_user_id,
--     notification_type, created_at, is_read, read_at, action_url, dedupe_key,
--     related_id, ...) is ever written.
--   * No DELETE, no TRUNCATE, no DDL, no new rows.
--   * Every reconstruction rule INNER JOINs to the FK/audit rows it needs — a
--     historical row whose FK no longer resolves, or whose audit/decision
--     timestamp does not match closely enough, is silently EXCLUDED from the
--     UPDATE (never guessed) and is picked up by Section 4's postcheck as
--     NON_RECONSTRUCTABLE.
--   * JSON_OBJECT() always produces valid JSON; Section 4 additionally runs
--     JSON_VALID() over every backfilled row as a belt-and-suspenders check.
--
-- =============================================================================
-- OPERATIONAL SAFETY (read before running on production)
-- =============================================================================
--   1. Take a backup / snapshot of the `notifications` table (or the whole
--      database) before running Section 3 on production.
--   2. Run Section 1 (Inventory Precheck) first. Read every row of every
--      query's output. If a notification_type or a "missing_metadata" count
--      appears that is not discussed in this file's mapping above, STOP —
--      do not run Section 3 for it until you have traced its producer and
--      added a reviewed rule.
--   3. Run Section 2 (Dry Run) next. It is 100% read-only and shows exactly
--      which rows Section 3 would touch and with what values. Review it.
--   4. If the `notifications` table is large enough that a multi-second lock
--      matters in production, run Section 3 during a maintenance window, or
--      wrap each UPDATE individually:
--        START TRANSACTION;
--        UPDATE ...;              -- one event's UPDATE from Section 3
--        SELECT ROW_COUNT();      -- confirm the affected-row count is sane
--        -- operator reviews, then either:
--        COMMIT;                  -- or ROLLBACK; if the count looks wrong
--      Each UPDATE below is independent and can be committed one at a time.
--   5. Run Section 4 (Postcheck) after applying, and keep its output as the
--      record of what was actually backfilled vs. left NON_RECONSTRUCTABLE.
--
-- HOW TO RUN
--   mysql --default-character-set=utf8mb4 -u <user> -p <database> < pems_patch_backfill_visitor_notification_metadata.sql
-- =============================================================================


-- =============================================================================
-- SECTION 1: INVENTORY PRECHECK (READ-ONLY)
-- Exposes the actual shape of THIS database's data before anything is trusted.
-- =============================================================================

SELECT '--- 1.A: Total Notifications By Recipient Role ---' AS _section;
SELECT r.role_code, COUNT(*) AS total_notifications
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
GROUP BY r.role_code;

SELECT '--- 1.B: Every Visitor Notification Type, Metadata Status ---' AS _section;
-- Any notification_type appearing here that is not covered by this file's
-- "VERIFIED EVENTKEY MAPPING" header comment has NO backfill rule below —
-- review it before assuming Section 3 handles it.
SELECT n.notification_type,
       COUNT(*) AS total,
       SUM(CASE WHEN n.metadata_json IS NOT NULL THEN 1 ELSE 0 END) AS with_metadata,
       SUM(CASE WHEN n.metadata_json IS NULL THEN 1 ELSE 0 END) AS missing_metadata
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'VISITOR'
GROUP BY n.notification_type
ORDER BY missing_metadata DESC;

SELECT '--- 1.C: Current EventKey Distribution for Visitor (already-enriched rows) ---' AS _section;
SELECT JSON_UNQUOTE(JSON_EXTRACT(n.metadata_json, '$.eventKey')) AS eventKey,
       COUNT(*) AS count
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'VISITOR'
  AND n.metadata_json IS NOT NULL
GROUP BY eventKey;

SELECT '--- 1.D: Legacy Rows Sample (Visitor, Missing Metadata, up to 200) ---' AS _section;
SELECT n.notification_id, n.notification_type, n.recipient_user_id, n.dedupe_key,
       n.related_type, n.related_id, n.visit_request_id, n.visit_instance_id,
       n.campus_id, n.actor_user_id, n.created_at
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'VISITOR'
  AND n.metadata_json IS NULL
ORDER BY n.created_at DESC
LIMIT 200;


-- =============================================================================
-- SECTION 2: DRY RUN PER EVENT (READ-ONLY)
-- One SELECT per reconstruction rule, mirroring the Section 3 UPDATE exactly.
-- Review before running Section 3.
-- =============================================================================

SELECT '--- 2.A: DRY RUN — CAMPUS_APPROVED ---' AS _section;
SELECT n.notification_id, n.created_at AS notification_time,
       c.name AS campusName, vr.request_code AS requestCode,
       host_user.full_name AS hostName,
       al.audit_log_id AS matched_audit_log_id, al.created_at AS audit_log_time
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN campuses c ON c.campus_id = n.campus_id
JOIN audit_logs al ON al.visit_instance_id = n.visit_instance_id
                   AND al.action LIKE 'APPROVE_CAMPUS_INSTANCE%'
                   AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al.created_at)) <= 5
JOIN users host_user
     ON host_user.user_id = CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(al.reason, 'host=', -1), ';', 1) AS UNSIGNED)
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_REQUEST_APPROVED'
  AND n.metadata_json IS NULL
  -- UNIQUENESS GUARD: exclude if more than one audit_logs row matches this
  -- notification's instance+action+time-window — an ambiguous candidate must
  -- never be resolved by undefined join-order luck.
  AND (SELECT COUNT(*) FROM audit_logs al2
       WHERE al2.visit_instance_id = n.visit_instance_id
         AND al2.action LIKE 'APPROVE_CAMPUS_INSTANCE%'
         AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al2.created_at)) <= 5) = 1;

SELECT '--- 2.B: DRY RUN — CAMPUS_REJECTED ---' AS _section;
SELECT n.notification_id, n.created_at AS notification_time,
       c.name AS campusName, vr.request_code AS requestCode,
       vrc.decision_note AS reason, vrc.decided_at AS decision_time
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN campuses c ON c.campus_id = n.campus_id
JOIN visit_request_campuses vrc ON vrc.visit_instance_id = n.visit_instance_id
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_REQUEST_REJECTED'
  AND n.metadata_json IS NULL
  AND vrc.decision_note IS NOT NULL
  AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, vrc.decided_at)) <= 5;

SELECT '--- 2.C: DRY RUN — FEEDBACK_INVITE_VISITOR ---' AS _section;
SELECT n.notification_id, n.created_at AS notification_time,
       vr.request_code AS requestCode
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_STATUS_CHANGED'
  AND n.metadata_json IS NULL
  AND n.dedupe_key LIKE 'FEEDBACK\_INVITE\_VISITOR\_%' ESCAPE '\\';

SELECT '--- 2.D: DRY RUN — VISIT_CLOSED ---' AS _section;
SELECT n.notification_id, n.created_at AS notification_time,
       vr.request_code AS requestCode
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_CLOSED'
  AND n.metadata_json IS NULL;

SELECT '--- 2.E: DRY RUN — VISIT_CANCELLED_BY_HOST ---' AS _section;
SELECT n.notification_id, n.created_at AS notification_time,
       c.name AS campusName, vr.request_code AS requestCode
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN campuses c ON c.campus_id = n.campus_id
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_CANCELLED'
  AND n.metadata_json IS NULL;

SELECT '--- 2.F: DRY RUN — OPCONTACT_TRANSFER_FROM ---' AS _section;
SELECT n.notification_id, n.created_at AS notification_time,
       c.name AS campusLabel, vr.request_code AS requestCode
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN visit_request_campuses vic ON vic.visit_instance_id = n.visit_instance_id
JOIN campuses c ON c.campus_id = vic.campus_id
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_REQUEST_SUBMITTED'
  AND n.metadata_json IS NULL
  AND n.title = 'Vai trò đầu mối vận hành đã được chuyển giao';

SELECT '--- 2.G: DRY RUN — OPCONTACT_TRANSFER_TO ---' AS _section;
SELECT n.notification_id, n.created_at AS notification_time,
       c.name AS campusLabel, vr.request_code AS requestCode
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN visit_request_campuses vic ON vic.visit_instance_id = n.visit_instance_id
JOIN campuses c ON c.campus_id = vic.campus_id
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_REQUEST_SUBMITTED'
  AND n.metadata_json IS NULL
  AND n.title = 'Chuyển giao đầu mối vận hành đã hoàn tất';

SELECT '--- 2.H: DRY RUN — AMENDMENT_APPROVED / AMENDMENT_REJECTED ---' AS _section;
SELECT n.notification_id, n.created_at AS notification_time,
       a.amendment_id, a.status AS amendment_status,
       CASE a.status WHEN 'APPROVED' THEN 'AMENDMENT_APPROVED'
                     WHEN 'REJECTED' THEN 'AMENDMENT_REJECTED'
                     ELSE NULL END AS resolved_eventKey
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_instance_amendments a
     ON a.visit_instance_id = n.visit_instance_id
    AND a.requested_by = n.recipient_user_id
    AND a.status IN ('APPROVED', 'REJECTED')
    AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, a.decided_at)) <= 5
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_REQUEST_SUBMITTED'
  AND n.metadata_json IS NULL
  AND n.title = 'Đề xuất thay đổi đã được xử lý'
  -- UNIQUENESS GUARD: an instance can hold more than one amendment row
  -- (amendment_no increments) — exclude if the timestamp window matches more
  -- than one.
  AND (SELECT COUNT(*) FROM visit_instance_amendments a2
       WHERE a2.visit_instance_id = n.visit_instance_id
         AND a2.requested_by = n.recipient_user_id
         AND a2.status IN ('APPROVED', 'REJECTED')
         AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, a2.decided_at)) <= 5) = 1;

SELECT '--- 2.I: DRY RUN — HOST_CHANGED ---' AS _section;
SELECT n.notification_id, n.created_at AS notification_time,
       c.name AS campusName, vr.request_code AS requestCode,
       alc.new_value_text AS hostName
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN campuses c ON c.campus_id = n.campus_id
JOIN audit_logs al ON al.visit_instance_id = n.visit_instance_id
                   AND al.action = 'HOST_TRANSFERRED'
                   AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al.created_at)) <= 5
JOIN audit_log_changes alc ON alc.audit_log_id = al.audit_log_id
                            AND alc.field_name = 'currentHostName'
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_STATUS_CHANGED'
  AND n.metadata_json IS NULL
  AND n.title = 'Host phụ trách chuyến thăm đã thay đổi'
  AND (SELECT COUNT(*) FROM audit_logs al2
       WHERE al2.visit_instance_id = n.visit_instance_id
         AND al2.action = 'HOST_TRANSFERRED'
         AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al2.created_at)) <= 5) = 1;

SELECT '--- 2.J: DRY RUN — ACCOUNT_LOCKED ---' AS _section;
SELECT n.notification_id, n.created_at AS notification_time,
       al.audit_log_id AS matched_audit_log_id
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN audit_logs al ON al.entity_type = 'User'
                   AND al.entity_id = n.recipient_user_id
                   AND al.action = 'SECURITY_LOCK_ACCOUNT'
                   AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al.created_at)) <= 5
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'ACCOUNT_STATUS_CHANGED'
  AND n.metadata_json IS NULL
  AND (SELECT COUNT(*) FROM audit_logs al2
       WHERE al2.entity_type = 'User' AND al2.entity_id = n.recipient_user_id
         AND al2.action = 'SECURITY_LOCK_ACCOUNT'
         AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al2.created_at)) <= 5) = 1;

SELECT '--- 2.K: DRY RUN — ACCOUNT_UNLOCKED ---' AS _section;
SELECT n.notification_id, n.created_at AS notification_time,
       al.audit_log_id AS matched_audit_log_id
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN audit_logs al ON al.entity_type = 'User'
                   AND al.entity_id = n.recipient_user_id
                   AND al.action = 'SECURITY_UNLOCK_ACCOUNT'
                   AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al.created_at)) <= 5
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'ACCOUNT_STATUS_CHANGED'
  AND n.metadata_json IS NULL
  AND (SELECT COUNT(*) FROM audit_logs al2
       WHERE al2.entity_type = 'User' AND al2.entity_id = n.recipient_user_id
         AND al2.action = 'SECURITY_UNLOCK_ACCOUNT'
         AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al2.created_at)) <= 5) = 1;


-- =============================================================================
-- SECTION 3: OPTIONAL BACKFILL (UPDATEs)
-- Each UPDATE mirrors its Section 2 dry run exactly. Independent and safe to
-- run/commit one at a time — see OPERATIONAL SAFETY above.
-- =============================================================================

-- EVENT: CAMPUS_APPROVED
UPDATE notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN campuses c ON c.campus_id = n.campus_id
JOIN audit_logs al ON al.visit_instance_id = n.visit_instance_id
                   AND al.action LIKE 'APPROVE_CAMPUS_INSTANCE%'
                   AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al.created_at)) <= 5
JOIN users host_user
     ON host_user.user_id = CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(al.reason, 'host=', -1), ';', 1) AS UNSIGNED)
SET n.metadata_json = JSON_OBJECT(
    'eventKey', 'CAMPUS_APPROVED',
    'params', JSON_OBJECT(
        'campusName', c.name,
        'requestCode', vr.request_code,
        'hostName', host_user.full_name
    )
)
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_REQUEST_APPROVED'
  AND n.metadata_json IS NULL
  AND (SELECT COUNT(*) FROM audit_logs al2
       WHERE al2.visit_instance_id = n.visit_instance_id
         AND al2.action LIKE 'APPROVE_CAMPUS_INSTANCE%'
         AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al2.created_at)) <= 5) = 1;

-- EVENT: CAMPUS_REJECTED
UPDATE notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN campuses c ON c.campus_id = n.campus_id
JOIN visit_request_campuses vrc ON vrc.visit_instance_id = n.visit_instance_id
SET n.metadata_json = JSON_OBJECT(
    'eventKey', 'CAMPUS_REJECTED',
    'params', JSON_OBJECT(
        'campusName', c.name,
        'requestCode', vr.request_code,
        'reason', vrc.decision_note
    )
)
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_REQUEST_REJECTED'
  AND n.metadata_json IS NULL
  AND vrc.decision_note IS NOT NULL
  AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, vrc.decided_at)) <= 5;

-- EVENT: FEEDBACK_INVITE_VISITOR
UPDATE notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
SET n.metadata_json = JSON_OBJECT(
    'eventKey', 'FEEDBACK_INVITE_VISITOR',
    'params', JSON_OBJECT('requestCode', vr.request_code)
)
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_STATUS_CHANGED'
  AND n.metadata_json IS NULL
  AND n.dedupe_key LIKE 'FEEDBACK\_INVITE\_VISITOR\_%' ESCAPE '\\';

-- EVENT: VISIT_CLOSED
UPDATE notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
SET n.metadata_json = JSON_OBJECT(
    'eventKey', 'VISIT_CLOSED',
    'params', JSON_OBJECT('requestCode', vr.request_code)
)
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_CLOSED'
  AND n.metadata_json IS NULL;

-- EVENT: VISIT_CANCELLED_BY_HOST
UPDATE notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN campuses c ON c.campus_id = n.campus_id
SET n.metadata_json = JSON_OBJECT(
    'eventKey', 'VISIT_CANCELLED_BY_HOST',
    'params', JSON_OBJECT(
        'campusName', c.name,
        'requestCode', vr.request_code
    )
)
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_CANCELLED'
  AND n.metadata_json IS NULL;

-- EVENT: OPCONTACT_TRANSFER_FROM
UPDATE notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN visit_request_campuses vic ON vic.visit_instance_id = n.visit_instance_id
JOIN campuses c ON c.campus_id = vic.campus_id
SET n.metadata_json = JSON_OBJECT(
    'eventKey', 'OPCONTACT_TRANSFER_FROM',
    'params', JSON_OBJECT(
        'campusLabel', c.name,
        'requestCode', vr.request_code
    )
)
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_REQUEST_SUBMITTED'
  AND n.metadata_json IS NULL
  AND n.title = 'Vai trò đầu mối vận hành đã được chuyển giao';

-- EVENT: OPCONTACT_TRANSFER_TO
UPDATE notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN visit_request_campuses vic ON vic.visit_instance_id = n.visit_instance_id
JOIN campuses c ON c.campus_id = vic.campus_id
SET n.metadata_json = JSON_OBJECT(
    'eventKey', 'OPCONTACT_TRANSFER_TO',
    'params', JSON_OBJECT(
        'campusLabel', c.name,
        'requestCode', vr.request_code
    )
)
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_REQUEST_SUBMITTED'
  AND n.metadata_json IS NULL
  AND n.title = 'Chuyển giao đầu mối vận hành đã hoàn tất';

-- EVENT: AMENDMENT_APPROVED / AMENDMENT_REJECTED (zero params; the eventKey
-- itself is chosen from the structured visit_instance_amendments.status column)
UPDATE notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_instance_amendments a
     ON a.visit_instance_id = n.visit_instance_id
    AND a.requested_by = n.recipient_user_id
    AND a.status IN ('APPROVED', 'REJECTED')
    AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, a.decided_at)) <= 5
SET n.metadata_json = JSON_OBJECT(
    'eventKey', CASE a.status WHEN 'APPROVED' THEN 'AMENDMENT_APPROVED' ELSE 'AMENDMENT_REJECTED' END,
    'params', JSON_OBJECT()
)
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_REQUEST_SUBMITTED'
  AND n.metadata_json IS NULL
  AND n.title = 'Đề xuất thay đổi đã được xử lý'
  AND (SELECT COUNT(*) FROM visit_instance_amendments a2
       WHERE a2.visit_instance_id = n.visit_instance_id
         AND a2.requested_by = n.recipient_user_id
         AND a2.status IN ('APPROVED', 'REJECTED')
         AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, a2.decided_at)) <= 5) = 1;

-- EVENT: HOST_CHANGED
UPDATE notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN visit_requests vr ON vr.visit_request_id = n.visit_request_id
JOIN campuses c ON c.campus_id = n.campus_id
JOIN audit_logs al ON al.visit_instance_id = n.visit_instance_id
                   AND al.action = 'HOST_TRANSFERRED'
                   AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al.created_at)) <= 5
JOIN audit_log_changes alc ON alc.audit_log_id = al.audit_log_id
                            AND alc.field_name = 'currentHostName'
SET n.metadata_json = JSON_OBJECT(
    'eventKey', 'HOST_CHANGED',
    'params', JSON_OBJECT(
        'campusName', c.name,
        'requestCode', vr.request_code,
        'hostName', alc.new_value_text
    )
)
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'VISIT_STATUS_CHANGED'
  AND n.metadata_json IS NULL
  AND n.title = 'Host phụ trách chuyến thăm đã thay đổi'
  AND (SELECT COUNT(*) FROM audit_logs al2
       WHERE al2.visit_instance_id = n.visit_instance_id
         AND al2.action = 'HOST_TRANSFERRED'
         AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al2.created_at)) <= 5) = 1;

-- EVENT: ACCOUNT_LOCKED (zero params)
UPDATE notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN audit_logs al ON al.entity_type = 'User'
                   AND al.entity_id = n.recipient_user_id
                   AND al.action = 'SECURITY_LOCK_ACCOUNT'
                   AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al.created_at)) <= 5
SET n.metadata_json = JSON_OBJECT('eventKey', 'ACCOUNT_LOCKED', 'params', JSON_OBJECT())
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'ACCOUNT_STATUS_CHANGED'
  AND n.metadata_json IS NULL
  AND (SELECT COUNT(*) FROM audit_logs al2
       WHERE al2.entity_type = 'User' AND al2.entity_id = n.recipient_user_id
         AND al2.action = 'SECURITY_LOCK_ACCOUNT'
         AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al2.created_at)) <= 5) = 1;

-- EVENT: ACCOUNT_UNLOCKED (zero params)
UPDATE notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
JOIN audit_logs al ON al.entity_type = 'User'
                   AND al.entity_id = n.recipient_user_id
                   AND al.action = 'SECURITY_UNLOCK_ACCOUNT'
                   AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al.created_at)) <= 5
SET n.metadata_json = JSON_OBJECT('eventKey', 'ACCOUNT_UNLOCKED', 'params', JSON_OBJECT())
WHERE r.role_code = 'VISITOR'
  AND n.notification_type = 'ACCOUNT_STATUS_CHANGED'
  AND n.metadata_json IS NULL
  AND (SELECT COUNT(*) FROM audit_logs al2
       WHERE al2.entity_type = 'User' AND al2.entity_id = n.recipient_user_id
         AND al2.action = 'SECURITY_UNLOCK_ACCOUNT'
         AND ABS(TIMESTAMPDIFF(SECOND, n.created_at, al2.created_at)) <= 5) = 1;


-- =============================================================================
-- SECTION 4: POSTCHECK (READ-ONLY)
-- =============================================================================

SELECT '--- 4.A: Backfill Summary By Type ---' AS _section;
SELECT n.notification_type,
       COUNT(*) AS total,
       SUM(CASE WHEN n.metadata_json IS NOT NULL THEN 1 ELSE 0 END) AS backfilled_or_existing,
       SUM(CASE WHEN n.metadata_json IS NULL THEN 1 ELSE 0 END) AS remaining_null
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'VISITOR'
GROUP BY n.notification_type
ORDER BY remaining_null DESC;

SELECT '--- 4.B: EventKey Distribution After Backfill ---' AS _section;
SELECT JSON_UNQUOTE(JSON_EXTRACT(n.metadata_json, '$.eventKey')) AS eventKey,
       COUNT(*) AS count
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'VISITOR'
  AND n.metadata_json IS NOT NULL
GROUP BY eventKey;

SELECT '--- 4.C: JSON Shape Sanity Check (should return 0 rows) ---' AS _section;
-- Any row here would mean a backfilled value is not valid JSON, or has no
-- eventKey — should never happen given JSON_OBJECT() above, but checked anyway.
SELECT n.notification_id, n.metadata_json
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'VISITOR'
  AND n.metadata_json IS NOT NULL
  AND (JSON_VALID(n.metadata_json) = 0
       OR JSON_EXTRACT(n.metadata_json, '$.eventKey') IS NULL);

SELECT '--- 4.D: Remaining NON_RECONSTRUCTABLE Rows (Visitor, still NULL) ---' AS _section;
-- Left NULL intentionally: no rule above matched (unknown/dead type, or a
-- known type whose FK/audit/decision evidence no longer resolves or no longer
-- matches within the timestamp window). The frontend's generic localized
-- fallback (resolveNotificationPresentation.ts) covers these safely for both
-- languages — see file header. Do NOT invent a value for any of these.
SELECT n.notification_id, n.notification_type, n.title, n.created_at,
       n.visit_request_id, n.visit_instance_id, n.campus_id, n.dedupe_key
FROM notifications n
JOIN users u ON u.user_id = n.recipient_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'VISITOR'
  AND n.metadata_json IS NULL
ORDER BY n.created_at DESC
LIMIT 500;
