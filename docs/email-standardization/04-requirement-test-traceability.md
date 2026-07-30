---
type: traceability
status: approved
updated: 2026-07-29
links:
  - docs/email-standardization/00-preflight-baseline.md
  - docs/email-standardization/01-email-caller-template-audit.md
  - docs/email-standardization/02-decisions-and-contracts.md
  - docs/email-standardization/03-system-template-catalog.md
  - docs/email-standardization/05-final-verification-report.md
---

# Requirement → test traceability

Every requirement of the email standardisation, and the named test that proves it. One rule was applied
throughout: **a test only appears against a requirement if failing that requirement would fail that test.**
A suite that merely runs nearby is not evidence, so "the email suite is green" appears nowhere below.

Layers: `U` unit · `I` integration (real MySQL, real MIME on disk) · `E2E` real-stack journey ·
`SQL` database script gate · `FE` frontend (vitest + Testing Library) · `BUILD` compiler/typecheck gate.

---

## 1. Content lives in the database, not in code

| # | Requirement | Where it is implemented | Test | Layer | State |
|---|---|---|---|---|---|
| R-01 | Subject/body come from `email_templates`; no hard-coded email content on any send path | `EmailTemplateRenderer` | `Renders_subject_and_body_from_the_database_row` | I | ✅ |
| R-02 | A code with no row fails; it never falls back to hard-coded content | `EmailTemplateRenderer` | `A_registered_code_with_no_row_fails_instead_of_falling_back_to_hard_coded_content` | I | ✅ |
| R-03 | An edit to a template row shows on the very next send, with no restart | `EmailTemplateRenderer` reads per render | `An_edit_to_the_row_is_visible_on_the_very_next_render`, `Editing_the_template_changes_the_very_next_message_without_a_restart` | I | ✅ |
| R-04 | The same is true of the preview modal — it shares the renderer | `PreviewEmailTemplateQueryHandler` | `A_template_edit_shows_in_the_next_preview_without_a_restart` | E2E | ✅ |
| R-05 | The preview refuses exactly what the send refuses (missing/unknown variable, unknown code) | same handler, same renderer | `The_preview_refuses_exactly_what_the_send_refuses` | E2E | ✅ |
| R-06 | An `INACTIVE` template is refused by both send and preview | renderer status check | `An_inactive_template_is_refused`, `The_preview_refuses_an_inactive_template` | I, E2E | ✅ |
| R-07 | A half-translated template fails rather than silently serving Vietnamese | renderer language check | `A_half_translated_template_fails_rather_than_silently_serving_vietnamese` | I | ✅ |
| R-08 | No dead hard-coded subject/body survives anywhere in the Application layer | `DepartmentPersonnelEmails` reduced to its status vocabulary | static scan §8 of `05-final-verification-report.md`; compiler proves no caller | BUILD | ✅ |

## 2. The catalog and the code agree

| # | Requirement | Implementation | Test | Layer | State |
|---|---|---|---|---|---|
| R-09 | Every registry code has an `ACTIVE` row | `SystemEmailTemplates` ↔ seed | `SystemEmailTemplateContractTests` (set comparison) | I | ✅ |
| R-10 | Every `ACTIVE` row has a registry code — no orphan template | same, reverse direction | `SystemEmailTemplateContractTests` | I | ✅ |
| R-11 | `variables_text` equals the placeholders actually used | seed | contract test + `03_verify.sql` checks E1/E2 | I, SQL | ✅ |
| R-12 | `variables_text` equals the registry's `DeclaredVariables` | registry | contract test | I | ✅ |
| R-13 | No PascalCase placeholder | seed | contract test + `03_verify.sql` check E3 | I, SQL | ✅ |
| R-14 | No action URL or token is an editable template variable | seed | contract test + `03_verify.sql` check E4 | I, SQL | ✅ |
| R-15 | Nothing depends on a numeric `email_template_id` | lookup is by `TemplateCode` | `Sync_script_never_writes_a_numeric_template_id`, `Sync_matches_on_code_so_existing_rows_keep_their_id` | I | ✅ |

## 3. Recipients, and what BCC promises

| # | Requirement | Implementation | Test | Layer | State |
|---|---|---|---|---|---|
| R-16 | One action produces exactly one MIME message for the whole envelope | `ManualEmailSender` | `Manual_compose_sends_one_message_for_all_three_groups`, `One_message_carries_every_TO_CC_and_BCC` | I | ✅ |
| R-17 | TO lands in `To`, CC in `Cc` | `EmailService.BuildMessage` | `TO_addresses_land_in_the_To_header`, `CC_addresses_land_in_the_Cc_header_where_recipients_can_see_them` | I | ✅ |
| R-18 | BCC appears in **no** header any recipient can read | `EmailService` uses the Bcc collection, never a header | `BCC_appears_in_no_header_the_other_recipients_can_read` | I | ✅ |
| R-19 | DB recipient rows match the message that was sent | `ManualEmailSender` | `One_message_carries_every_TO_CC_and_BCC`, `Every_addressee_shares_the_one_provider_identity` | I | ✅ |
| R-20 | The sender sees every blind copy | `SentEmailAccess.FilterRecipients` | `The_sender_sees_both_blind_copies` | I | ✅ |
| R-21 | A TO recipient sees no blind copy | same | `The_primary_recipient_sees_the_message_but_no_blind_copy` | I | ✅ |
| R-22 | A CC recipient sees no blind copy | same | `The_carbon_copy_sees_the_message_but_no_blind_copy` | I | ✅ |
| R-23 | Each BCC sees only their own entry, never another's | same | `A_blind_copy_sees_its_own_entry_and_not_the_other` | I | ✅ |
| R-24 | A linked-object viewer reads the message without its blind copies | same | `The_visit_host_reads_a_linked_message_without_its_blind_copies`, `Ho_reaching_a_linked_message_through_the_visit_still_gets_no_blind_copies` | I | ✅ |
| R-25 | HO / Staff Leader with no object scope are refused | `SentEmailAccess.Resolve` | `Ho_cannot_read_a_manual_message_it_was_not_party_to`, `A_staff_leader_cannot_read_a_manual_message_it_was_not_party_to` | I | ✅ |
| R-26 | An outsider is refused | same | `An_unrelated_colleague_is_refused` | I | ✅ |
| R-27 | Search by a blind address never surfaces the message to anyone else | list query | `Searching_by_a_blind_address_never_surfaces_the_message_to_anyone_else` | I | ✅ |
| R-28 | No count or flag betrays that a blind copy exists | `ViewEmailDto` | `The_detail_payload_carries_no_count_that_would_betray_a_blind_copy` | I | ✅ |
| R-29 | List and detail agree about who may read | both surfaces call one rule | `The_list_and_the_detail_agree_about_who_may_read_the_message` | I | ✅ |
| R-30 | Attachment download follows exactly the rule that governs the message | `FileDownloadAuthorization` | `Email_attachment_follows_exactly_the_rule_that_governs_the_message`, `Object_scope_grants_the_attachment_exactly_when_it_grants_the_message` | I | ✅ |
| R-31 | Total recipients across TO+CC+BCC are capped, on the real send path | `EmailRecipientValidator` inside `TrySendAsync` | `A_message_over_the_recipient_ceiling_is_refused_and_nothing_is_written_or_sent`, `A_message_exactly_at_the_recipient_ceiling_is_allowed` | E2E | ✅ |
| R-32 | Duplicate addresses within a group and across groups are refused | same | `Refuses_the_same_address_in_TO_and_BCC_and_sends_nothing`, `EmailRecipientValidatorTests` | I, U | ✅ |

## 4. Sender identity and headers

| # | Requirement | Implementation | Test | Layer | State |
|---|---|---|---|---|---|
| R-33 | The From address comes from configuration, never from a caller | `EmailService.SendCoreAsync` | `The_sender_address_always_comes_from_configuration`, `The_sender_identity_still_comes_from_configuration` | I | ✅ |
| R-34 | A caller cannot set From/Sender/Reply-To/Return-Path through the header bag | `EmailRecipientValidator.ReservedHeaderNames` | `A_header_the_pipeline_owns_is_refused_from_the_bag` (8 cases) | I | ✅ |
| R-35 | Nor To/Cc/Bcc/Message-Id — the envelope is the typed lists, not a header | same | same theory + `Header_names_are_matched_regardless_of_case` | I | ✅ |
| R-36 | Threading headers still work: `In-Reply-To` / `References` pass | denylist excludes them | `Thread_headers_are_still_allowed_through_the_headers_bag`, `Thread_headers_are_preserved` | I | ✅ |
| R-37 | `Message-Id` reaches the wire from the typed field and matches the history row | `OutboundEmail.MessageId` | `The_message_id_comes_from_the_typed_field_and_reaches_the_wire` | I | ✅ |
| R-38 | CR/LF injection is impossible in address, display name and subject | `AssertNoHeaderBreak` | `Refuses_a_subject_carrying_a_header_break_and_sends_nothing`, `EmailRecipientValidatorTests` | I, U | ✅ |
| R-39 | A Vietnamese display name is RFC 2047-encoded, not emitted raw | `ToMailAddress` | `A_display_name_with_vietnamese_diacritics_is_encoded_not_emitted_raw` | I | ✅ |

## 5. Security templates and one-time links

| # | Requirement | Implementation | Test | Layer | State |
|---|---|---|---|---|---|
| R-40 | A security template refuses CC | `EmailRecipientPolicyEnforcer` | `Refuses_CC_on_a_security_template_and_sends_nothing` | I | ✅ |
| R-41 | …and refuses BCC | same | `Refuses_BCC_on_a_security_template_and_sends_nothing` | I | ✅ |
| R-42 | …and refuses two recipients on a token-bearing invitation | same | `Refuses_two_recipients_on_an_invitation_that_carries_a_personal_token` | I | ✅ |
| R-43 | Two invitations are two messages, one addressee each, no copies | dispatcher per-recipient policy | `Two_invitations_produce_two_separate_messages_each_addressed_to_one_person` | E2E | ✅ |
| R-44 | Each invitation carries only its own accept/decline link | same | same test (asserts the other's link is absent from the decoded body) | E2E | ✅ |
| R-45 | A token belongs to one recipient row and does not open another invitation | `email_action_tokens` FKs | `An_invitation_token_belongs_to_one_recipient_and_does_not_open_another_invitation` | E2E | ✅ |
| R-46 | Token and message are written in one transaction; a rollback leaves neither | `InviteVisitParticipant` | `The_message_and_its_tokens_are_written_in_one_transaction_and_sent_after_it_commits`, `A_rollback_leaves_neither_the_message_nor_the_tokens_nor_a_sent_email` | I | ✅ |
| R-47 | No raw token, OTP or action URL is stored in the history body | `EmailComposition` strip | `The_history_row_keeps_the_metadata_but_not_the_one_time_link`, `The_delivered_message_carries_a_working_link_and_the_stored_one_does_not` | I | ✅ |
| R-48 | An author may not interpolate a credential, the action block, or a bare token into a subject | `AuthoredEmailContent` | `An_authored_subject_may_not_interpolate_a_credential`, `…the_action_block`, `…carry_the_bare_token_either` | I | ✅ |
| R-49 | No OTP/token is written to the application log | `EmailService` logging | `EmailServiceSensitiveLoggingTests` | I | ✅ |
| R-50 | A new account is `PENDING_EMAIL_CONFIRMATION` and cannot log in before confirming | account slice | `PendingAccountLoginBlockTests` | U | ✅ |
| R-51 | A confirmation token works exactly once | `ConfirmAccountEmailCommandHandler` | `ConfirmAccountEmailCommandHandlerTests`, `AccountEmailConfirmationPersistenceTests` | U | ✅ |
| R-52 | The confirmation mail goes to one person with no copies | account slice + policy | `The_confirmation_mail_goes_to_one_person_with_no_copies` | I | ✅ |

## 6. Truthful history

| # | Requirement | Implementation | Test | Layer | State |
|---|---|---|---|---|---|
| R-53 | Provider acceptance is recorded `SENT`, never `DELIVERED` | `ManualEmailSender`, `SystemEmailDispatcher` | `Provider_acceptance_is_recorded_as_SENT_and_never_as_DELIVERED`, `Provider_acceptance_reports_Sent_and_never_Delivered` | I | ✅ |
| R-54 | A provider failure is recorded `FAILED` with a safe message, and no false `SENT` | same | `A_provider_failure_is_recorded_as_FAILED_with_a_safe_message`, `A_provider_failure_fails_the_command_and_is_recorded_as_FAILED` | I | ✅ |
| R-55 | One send writes exactly one history row | `ManualEmailSender` | `Sending_a_draft_produces_one_message_and_one_history_row` | I | ✅ |
| R-56 | Two simultaneous sends of one draft produce one message | draft state machine | `Two_simultaneous_sends_of_one_draft_produce_exactly_one_message` | I | ✅ |
| R-57 | A sent draft cannot be sent again | same | `A_sent_draft_cannot_be_sent_again` | I | ✅ |
| R-58 | The stored snapshot is the content that was sent | `ManualEmailSender` | `The_stored_snapshot_is_the_content_that_was_sent` | I | ✅ |
| R-59 | A refused send writes no history, no recipient row, and sends nothing | validation before persistence | `A_refused_edit_writes_no_history_no_recipient_and_sends_nothing`, `Manual_compose_refuses_an_envelope_with_no_TO_and_records_nothing` | I | ✅ |
| R-60 | An inactive template stops the send with a stable code and no history | dispatcher | `An_inactive_template_stops_the_send_with_a_stable_error_and_no_history` | I | ✅ |

## 7. Drafts and reply

| # | Requirement | Implementation | Test | Layer | State |
|---|---|---|---|---|---|
| R-61 | A draft round-trips every group exactly as entered | `CreateEmailDraft` / `GetEmailDraft` | `A_draft_round_trip_keeps_every_group_exactly_as_entered` | I | ✅ |
| R-62 | An update replaces the envelope without leaving the old one behind | `UpdateEmailDraft` | `An_update_replaces_the_envelope_without_leaving_the_old_one_behind` | I | ✅ |
| R-63 | A rejected update leaves the saved envelope untouched | same | `A_rejected_update_leaves_the_saved_envelope_untouched` | I | ✅ |
| R-64 | A draft belongs to its author alone | `ListEmailDrafts` / `GetEmailDraft` | `A_draft_belongs_to_its_author_alone`, `The_list_contains_only_the_callers_own_drafts`, `Reading_another_users_draft_by_id_is_refused` | I | ✅ |
| R-65 | The drafts list orders stably before paging | `ListEmailDrafts` | `Drafts_edited_at_the_same_instant_keep_a_stable_order_across_pages`, `Paging_returns_the_requested_slice_and_the_full_total` | I | ✅ |
| R-66 | The drafts summary counts recipients without returning addresses | same | `The_summary_counts_recipients_without_returning_their_addresses` | I | ✅ |
| R-67 | Only someone who can read the original may reply | `ReplytoEmailCommandHandler` | `Someone_who_was_never_on_the_message_cannot_reply_to_it` | I | ✅ |
| R-68 | The reply's TO is resolved by the backend; the client cannot supply it | command has no TO field | `A_reply_carries_its_own_copies_and_never_the_originals_blind_ones` | I | ✅ |
| R-69 | A reply never carries the original's BCC — not in UI, state, or payload | `ReplyComposer` + handler | `A_reply_carries_its_own_copies_and_never_the_originals_blind_ones` (I) + `ReplyComposer.test.tsx` (FE) | I, FE | ✅ |
| R-70 | A reply does not copy the original's attachments | handler passes an empty list | `A_reply_does_not_carry_the_attachments_of_the_message_it_answers` | E2E | ✅ |
| R-71 | `In-Reply-To` / `References` / thread id point at the real parent | `ManualEmailSender` | `A_reply_points_at_its_parent_and_leaves_it_untouched` | I | ✅ |
| R-72 | Replying does not mark the parent delivered or completed | same | same test | I | ✅ |
| R-73 | The reply button appears exactly when the command would accept | `SentEmailAccess.CanOfferReply` | `Offering_a_reply_never_exceeds_what_the_reply_command_accepts` + 7 relation tests | U, I | ✅ |
| R-74 | "Đánh dấu đã xử lý" appears exactly when the command would accept | `SentEmailAccess.CanMarkComplete`, called by both sides | `Offering_completion_matches_what_the_command_accepts_exactly` + `An_addressee_is_told_they_may_close_the_message`, `A_linked_object_reader_is_not_told_they_may_close_the_message`, `A_message_already_closed_is_not_offered_for_closing_again` | U, I | ✅ |
| R-75 | Double-clicking send does not send twice within a UI session | `useGuardedSend`, compose/reply guards | `reportSendGuard.test.tsx` (8), `ReplyComposer.test.tsx` double-submit | FE | ✅ |

## 8. Report and invoice mail

| # | Requirement | Implementation | Test | Layer | State |
|---|---|---|---|---|---|
| R-76 | Each of the six report actions uses its declared template | six handlers | `C24_…` `C25_…` `C26_…` `C27_…` `C28_…` `C29_…` | I | ✅ |
| R-77 | The attachment is generated server-side and validated as a PDF | `ReportEmailSender` | `C24_campus_report_uses_the_campus_template_and_attaches_the_document`, `ReportEmailSenderTests` | I, U | ✅ |
| R-78 | The attachment gets a `files` row and is downloadable only under the message's own rule | `ReportEmailSender` + `FileDownloadAuthorization` | `The_test_sink_records_the_attachment_metadata`, `Email_attachment_follows_exactly_the_rule_that_governs_the_message` | I | ✅ |
| R-79 | A report goes to one visible recipient, with no copies and no token | caller-controlled policy + handlers | `A_report_goes_to_one_visible_recipient_with_no_copies_and_mints_no_token` | I | ✅ |
| R-80 | A recipient is resolved from scope; a leader cannot report outside their own | handler scope queries | `A_leader_cannot_report_on_somebody_outside_their_own_scope`, `A_personnel_report_is_addressed_to_the_person_it_is_about` | I | ✅ |
| R-81 | A department with no leader to write to is refused before anything is generated | handler | `A_department_with_no_leader_to_write_to_is_refused_before_anything_is_generated` | I | ✅ |
| R-82 | `file_id` / `report_id` cannot be swapped to reach another scope | scope-first re-read from DB | `Sync`-independent: `ReportInvoiceRouteTests`, `Walking_the_ids_returns_neither_bytes_nor_metadata` | I | ✅ |
| R-83 | `unitPrice` negative / too large / wrong scale / overflow is a business error, not a 500 | `InvoiceMoney` | `InvoiceMoneyTests` (18) | U | ✅ |
| R-84 | All three invoice paths validate price identically, after scope | `InvoiceMoney.ValidateUnitPrice` per item in all three | `InvoiceMoneyTests` + `ReportInvoiceRouteTests` | U, I | ✅ |
| R-85 | The three scaffold endpoints cannot be called anonymously | class-level `[Authorize]` | `ReportInvoiceRouteTests` anonymous-challenge theory | I | ✅ |
| R-86 | `SendHoCampusReport` checks the role in the handler, not only in the attribute | `HoReportV2Guard.RequireHo` at `SendHoCampusReportCommand.cs:58` | `ReportEmailEndToEndTests` C24 path | I | ✅ |

## 9. Frontend rendering safety

| # | Requirement | Implementation | Test | Layer | State |
|---|---|---|---|---|---|
| R-87 | Every email HTML sink is sanitised at its source | `sanitizeHtml` at 6 call sites | `emailHtmlSanitization.test.tsx` (12, caller-level) | FE | ✅ |
| R-88 | No print/report path builds HTML by string interpolation | `features/reports/print/` DOM builders | `reportPrintDocuments.test.ts` (9) | FE | ✅ |
| R-89 | Recipient chips enforce group rules and the limit in the UI | `RecipientChipInput`, `useRecipientLimit` | `features/emails/__tests__` | FE | ✅ |
| R-90 | `tsc --noEmit` stays an independent gate, not replaced by vitest | CI/local gate | §6 of `05-final-verification-report.md` | BUILD | ✅ |

## 10. Database sync (G7)

| # | Requirement | Implementation | Test | Layer | State |
|---|---|---|---|---|---|
| R-91 | Preflight is read-only | `01_preflight.sql` | `Preflight_changes_nothing`, `Preflight_contains_no_mutating_statement` | SQL | ✅ |
| R-92 | The sync refuses to run against an unnamed target | guard procedure | `Sync_refuses_to_run_without_an_explicitly_named_target`, `Sync_refuses_when_the_named_target_is_a_different_database` | SQL | ✅ |
| R-93 | One confirmation authorises exactly one run | guard clears itself | `Sync_spends_the_confirmation_so_the_same_session_cannot_reuse_it` | SQL | ✅ |
| R-94 | Upsert is by `template_code`; existing rows keep their id | `02_sync_templates.sql` | `Sync_matches_on_code_so_existing_rows_keep_their_id` | SQL | ✅ |
| R-95 | All 30 canonical templates end up present and `ACTIVE` | same | `Sync_makes_every_registered_template_present_and_active` | SQL | ✅ |
| R-96 | The 9 legacy codes end `INACTIVE`, never deleted | same | `Sync_retires_every_legacy_code_without_deleting_it` | SQL | ✅ |
| R-97 | Operator-authored templates are untouched, including active ones | explicit legacy list | `Sync_leaves_operator_authored_templates_alone` | SQL | ✅ |
| R-98 | History, drafts, recipients, tokens and out-of-scope tables are untouched | scope of the script | `Sync_leaves_history_drafts_and_everything_outside_email_templates_untouched`, `Sync_preserves_the_body_snapshot_of_a_history_row_on_a_retired_template` | SQL | ✅ |
| R-99 | A second run changes nothing, including `updated_at` | difference predicate | `Second_sync_run_changes_nothing_at_all`, `Second_sync_run_reports_zero_inserted_updated_and_deactivated` | SQL | ✅ |
| R-100 | Verify is a real gate — it fails the run when an invariant breaks | `SIGNAL` in `03_verify.sql` | `Verify_fails_when_a_canonical_template_is_deactivated_behind_its_back`, `…when_a_retired_template_is_reactivated`, `…when_variables_text_stops_matching_the_body` | SQL | ✅ |
| R-101 | The sync script cannot drift from the seed it converges on | generated from the canonical block | `Sync_script_carries_exactly_the_registered_codes` | SQL | ✅ |
| R-102 | The sync deletes nothing and touches no table but `email_templates` | script text | `Sync_script_deletes_nothing_anywhere`, `Sync_script_never_touches_history_drafts_or_tokens` | SQL | ✅ |

## 11. Send idempotency (G11 / R-103)

Every row below fails if the requirement breaks — the tests count rows, files and `.eml` messages rather
than asserting that a fake was called.

| # | Requirement | Implementation | Test | Layer | State |
|---|---|---|---|---|---|
| R-107 | A send with no `Idempotency-Key` is refused, and sends nothing | `IdempotencyKey.RequireHash` | `A_missing_key_is_refused_with_a_stable_code`, `A_send_with_no_idempotency_key_is_refused_and_sends_nothing`, `An_invoice_send_with_no_idempotency_key_is_refused` | U, I, HTTP | ✅ |
| R-108 | A key containing CR/LF or a control character is refused | printable-ASCII rule | `A_malformed_key_is_refused_with_a_stable_code` (6 cases), `A_send_with_a_header_injecting_key_is_refused_and_sends_nothing` | U, I | ✅ |
| R-109 | The key is opaque and case-sensitive; only its hash is stored | `IdempotencyKey.Hash` | `The_key_is_opaque_and_case_sensitive`, `Only_the_hash_ever_leaves_this_class`, `A_replay_is_recorded_as_one_reservation_that_ran_once` | U, I | ✅ |
| R-110 | The fingerprint is business content, not serialised JSON | `EmailSendFingerprintBuilder` | `Line_order_does_not_change_the_request`, `Decimal_formatting_does_not_change_the_request`, `The_time_of_day_is_not_part_of_the_request`, + 7 more | U | ✅ |
| R-111 | A replay returns the first result and creates no PDF, file, history row, attachment or MIME | behaviour short-circuits before the handler | `A_replay_returns_the_first_result_and_produces_nothing_new` (footprint equality), `An_invoice_send_repeated_with_the_same_key_produces_one_message` | I, HTTP | ✅ |
| R-112 | The same key with a different request is refused, and sends nothing | fingerprint comparison under the row lock | `The_same_key_with_a_different_request_is_refused_and_sends_nothing`, `An_invoice_send_reusing_a_key_for_a_different_request_is_refused`, `Editing_the_request_after_a_clean_failure_needs_a_new_key` | I, HTTP | ✅ |
| R-113 | Two concurrent requests with one key produce one message; a unique-key collision is not a 500 | `INSERT IGNORE` + `SELECT … FOR UPDATE` | `Two_concurrent_requests_with_one_key_produce_one_message` | I | ✅ |
| R-114 | A failure decided before the outbound call may be retried under the same key | `FAILED_BEFORE_DISPATCH` | `A_failure_before_dispatch_can_be_retried_with_the_same_key` | I | ✅ |
| R-115 | A failure after the outbound call started is `OUTCOME_UNKNOWN` and is never auto-retried | `EmailSendAttempt.DispatchStarted` | `A_provider_failure_after_dispatch_started_is_recorded_as_an_unknown_outcome`, `An_unknown_outcome_is_never_retried_under_the_same_key` | I | ✅ |
| R-116 | A configuration refusal is classified as clean, not unknown | `EmailDeliveryCodes.ProvesNothingWasSent` | `A_configuration_refusal_proves_nothing_was_sent`, `An_smtp_exception_proves_nothing_at_all`, `An_unclassified_failure_code_reads_as_unknown` | U | ✅ |
| R-117 | Provider rejection still never writes `SENT` to the history | unchanged dispatcher | `A_provider_failure_after_dispatch_started_is_recorded_as_an_unknown_outcome` (status assertion) | I | ✅ |
| R-118 | One actor's key cannot reach another actor's reservation | actor in the unique key, read from the JWT | `A_second_actor_using_the_same_key_gets_their_own_send` | I | ✅ |
| R-119 | A new key with the same payload is a valid new send | no payload-based dedup predicate | `A_new_key_with_an_identical_payload_sends_again`, `A_new_key_after_an_unknown_outcome_is_a_new_send` | I | ✅ |
| R-120 | All six send routes are covered; none has a keyless path | `IIdempotentEmailSend` marker | `All_six_send_commands_declare_themselves_idempotent`, `Every_send_action_replays_instead_of_sending_twice` (6 cases) | U, I | ✅ |
| R-121 | The reservation stores no note, no amount and no raw key | hashes only | `The_reservation_stores_hashes_and_a_result_never_a_copy_of_the_request` | I | ✅ |
| R-122 | The frontend reuses the key across a retry and mints a new one only when the attempt ends | `useIdempotentSend`, `attemptIsOver` | `idempotentSend.test.tsx` (23) | FE | ✅ |
| R-123 | A timed-out send is not reported as "failed" | `sendFailureMessage` | `does not say "failed" when the connection dropped`, `…when the outcome is unknown` | FE | ✅ |
| R-124 | The key survives a reload within the session and does not outlive the tab | `sessionStorage` | `survives a remount…`, `keeps the key out of localStorage…` | FE | ✅ |
| R-125 | The migration is additive, guarded, idempotent and verified by a real gate | `email_dispatch_idempotency/` | `The_migration_touches_nothing_but_its_own_table`, `The_migration_guards_its_target_and_spends_the_confirmation`, `The_verify_script_is_a_gate` + measured double run | SQL | ✅ |
| R-126 | Every SQL script declares its connection character set | `SET NAMES utf8mb4` | `Every_script_sets_its_connection_character_set` (3 cases), `The_migration_sets_its_connection_character_set` | SQL | ✅ |

## 12. Preview coverage (G11 / R-106)

| # | Requirement | Implementation | Test | Layer | State |
|---|---|---|---|---|---|
| R-127 | All 30 active templates preview in VI and EN | neutral disabled block for unregistered action templates | `Every_active_template_previews_in_both_languages` (60 cases) | I | ✅ |
| R-128 | An unregistered action template is shown as one, with an inert block and no invented labels | `DisabledUnspecifiedActionBlock` | `Templates_with_an_unregistered_action_block_preview_as_action_templates` | I | ✅ |
| R-129 | No preview contains a token, a clickable link, a script or an event handler | `<span>`-only blocks, no `href` | `No_preview_contains_a_clickable_link_a_token_or_a_script` (60 cases) | I | ✅ |
| R-130 | Send stays fail-closed for action templates, registered or not | preview-only fallback | `Send_still_refuses_an_action_template_with_no_action_data`, `Send_still_refuses_an_unregistered_action_template_with_no_action_data` | I | ✅ |
| R-131 | A template edited in the database previews from the edit, with no restart | no renderer cache | `A_hot_edit_shows_up_in_the_next_preview` | I | ✅ |

---

## Requirements NOT closed

These are carried, not claimed. Each names what would have to be decided or built.

| # | Requirement | Why it is open | Needs |
|---|---|---|---|
| R-104 | The three dashboard scaffold endpoints must carry a role gate | Two canonical documents disagree on both the role set and the UC ids, and the metric semantics are unspecified. | Owner picks the role contract and the metrics — see `08-open-product-decisions.md`. |
| R-105 | `sendStaffLeaderDeptInvoice` / `sendDeptLeaderInvoiceToStaffLeader` need a UI entry point | No product flow specifies where the button lives. Routes are proven by API test; nothing calls them from the app. | A UX decision — see `08-open-product-decisions.md`. Both routes are already under the G11 idempotency contract, so a UI added later inherits it. |

### Closed in G11

| # | Was | Now |
|---|---|---|
| R-103 | A report-email retry after a network timeout could send twice. | **CLOSED** — persistent reservations (R-107…R-126). Note the limit that is *not* claimed: SMTP has no exactly-once delivery, and an outcome the provider never confirmed is reported as unknown rather than guessed. |
| R-106 | 9 of the 30 templates could not be previewed at all. | **CLOSED** — 30/30 in both languages (R-127…R-131), with send security unchanged. |

### Noted, not actioned

| Observation | Why it is only noted |
|---|---|
| `IdempotencyFilter.cs`, `IIdempotencyService.cs` and `Infrastructure/Idempotency/IdempotencyService.cs` are empty stubs in the wrong namespace with zero references, and `tests/…/Api/IdempotencyBehaviourTests.cs` is a zero-byte file. | They are now actively misleading — a reader looking for the idempotency contract will find `IIdempotencyService` first. Removing them is the obvious follow-up, but it would take the branch's deletion count off zero, which the G11 brief pinned as a baseline to preserve. Recommended as a one-line follow-up commit. |
| `USE_CASE_LIST.md` line 32 states `ReportsController` has no authorization and is callable anonymously. | No longer true of the code — the class carries `[Authorize]`. Left as-is because that paragraph is settled by the R-104 decision, and correcting documentation to match code is not this workstream's call to make unilaterally. |

---

## G12 + G11-H/I/J — 2026-07-30

Chi tiết đầy đủ: `09-g12-contact-guard-and-template-contract.md`. Bảng dưới là truy vết
requirement → test.

### G12 — contact guard

| # | Requirement | Test / bằng chứng |
|---|---|---|
| R-132 | Sai quan hệ đầu mối chính bị DB chặn bằng `SQLSTATE 45000` **và** stable code | `ContactGuardTests.Every_guard_refusal_carries_sqlstate_45000` — đọc SQLSTATE **phía server** qua `GET DIAGNOSTICS`, không dùng error-number 1644 của client làm bằng chứng thay thế |
| R-133 | `contact_guard_negative_failures = 0` | Fresh import canonical: 18 NEG pass. Nguyên nhân của "14" cũ là **thứ tự trong handler self-test**, không phải trigger |
| R-134 | `contact_guard_positive_failures = 0` | 8 POS pass, gồm POS-08 mới |
| R-135 | VISITOR chưa xác nhận email bị từ chối bằng **business code**, không phải `22001` | `Unconfirmed_visitor_is_refused_with_the_business_code_not_a_storage_error` + NEG-16 + NEG-18 |
| R-136 | Role không đọc được không được lọt qua bằng NULL semantics | `Users_guard_counts_the_role_it_looked_up`, `Visitor_guard_reads_status_into_a_wide_enough_variable` |
| R-137 | Đường UPDATE chỉ ghi `visitor_user_id` vẫn bị canh | `Updating_visitor_user_id_alone_is_still_guarded` + NEG-17 |
| R-138 | Quan hệ hợp lệ **không** bị chặn oan | 4 test positive, gồm `A_visitor_linked_only_to_a_cancelled_request_may_be_deactivated` (có assert fixture thật sự cancelled) |
| R-139 | Migration idempotent, không drift, khớp fresh canonical | run ×2 snapshot MD5 giống hệt; 0/32 trigger body khác; verify 34 PASS / 0 FAIL; đối chứng âm exit 1 |

### G11-J — variable contract

| # | Requirement | Test |
|---|---|---|
| R-140 | Catalog phủ đúng registry — không thiếu, không thừa | `Catalog_describes_every_variable_the_registry_declares` + `Catalog_describes_nothing_the_registry_does_not_declare` |
| R-141 | Mẫu canonical mở lần đầu **0** false warning | `Every_template_accepts_content_built_from_its_own_contract`; FE `opens a canonical template with no warnings at all` |
| R-142 | Sidebar không lẫn biến module khác | `Account_email_confirmation_offers_no_logistics_variables`; FE `offers only this template's variables` + 6 case `refuses %s on an account template` |
| R-143 | Preview toàn catalog VI **và** EN | `Every_template_previews_from_samples_alone` ×2 — 0 unresolved placeholder |
| R-144 | Preview không chứa token/link thật | `No_preview_contains_a_real_token_or_a_clickable_link` ×2; `No_preview_sample_looks_like_a_real_credential` ×2 |
| R-145 | Biến lạ / sai casing / malformed bị chặn lưu | `EmailTemplateContentValidatorTests` (19 test) |
| R-146 | Xóa required / `actionBlock` bị chặn; xóa optional được phép | 3 test unit + 2 integration |
| R-147 | Send thiếu runtime value vẫn fail-closed | `Without_sample_mode_a_missing_caller_variable_still_fails` |
| R-148 | Issue có field + code + variable + cả hai ngôn ngữ | `Issues_are_addressed_to_the_field_that_carries_them`, `Every_issue_carries_both_languages_and_a_stable_code`, `All_problems_are_reported_together` |
| R-149 | Caller không inject được trusted block qua context | `A_caller_cannot_inject_the_action_block_through_the_context` |
| R-150 | `actionBlock` hợp lệ trên **mọi** template (14 dùng, 5 đăng ký) | `The_action_block_is_a_legal_placeholder_on_every_template` |

### G11-I — fixed catalog

| # | Requirement | Test |
|---|---|---|
| R-151 | Create bị chặn ở handler, **0 dòng được ghi** | `Create_is_refused_with_a_stable_code` |
| R-152 | Toggle status bị chặn | `Toggle_status_is_refused_with_a_stable_code` |
| R-153 | Không tồn tại command delete/clone | `No_delete_command_exists_for_email_templates` |
| R-154 | Field registry-owned **không có trên command** | `Update_command_does_not_expose_a_registry_owned_field` (6 field) |
| R-155 | Update không đổi được code/module/status/format | `An_update_cannot_move_the_registry_owned_fields` |
| R-156 | `variables_text` ghi lại từ registry | `An_update_rewrites_variables_text_from_the_registry` |
| R-157 | Count/code set không đổi; DB khớp registry | `A_content_update_leaves_the_count_and_code_set_untouched`, `The_database_code_set_matches_the_registry_exactly` |
| R-158 | Concurrent update không overwrite im lặng | 3 test concurrency (stale token, thiếu token, token vừa trả về) |
| R-159 | Mẫu lịch sử giữ, không sửa được | `A_historical_template_is_kept_but_not_editable` |
| R-160 | Không ghi nội dung VI lên bản EN | FE `does not overwrite the English content with the Vietnamese one` |

### G11-H — TO/CC/BCC

| # | Requirement | Test |
|---|---|---|
| R-161 | Sink evidence áp **đúng** policy như provider thật | `FileSinkPolicyParityTests` (10 test; 4 refusal + assert **0 dòng ghi**) |
| R-162 | Không mẫu mang bí mật nào cho phép copies | `No_secret_bearing_template_permits_copies` (quét toàn registry) |
| R-163 | Capability do backend trả, FE không suy từ tên mẫu | `GET contract/{code}` → `allowCc`/`allowBcc`; FE `warns that a secret-bearing template cannot be copied` |
| R-164 | Evidence ghi envelope đã normalize | `The_recorded_envelope_is_the_normalised_one` |

### Vẫn mở

| # | Nội dung | Tại sao |
|---|---|---|
| R-165 | Restore default **từng mẫu** chưa có | Nội dung gốc chỉ tồn tại ở canonical seed + sync script; không cột nào giữ bản gốc. Làm đúng cần bảng additive `email_template_defaults`. Đường vòng: chạy lại `02_sync_templates.sql`. |
| R-166 | Concurrency ở độ phân giải giây | Hai lần lưu trong cùng một giây không phân biệt được; cần cột row-version đơn điệu |
| R-167 | Một lần đỏ không tất định ở reminder race test | Chưa giải thích xong — xem `09-…` Mục 6.2. Không dán nhãn flaky, không sửa test |

---

## G11 final closure — R-165 đến R-167 đóng lại, R-168 đến R-186 mới

> Lượt này đóng đúng ba khoản còn mở ở trên và mở rộng G11-H sang các đường chưa có bằng chứng.
> Chi tiết thiết kế: `10-g11-final-closure.md`.

### Ba khoản cũ

| # | Trạng thái | Bằng chứng |
|---|---|---|
| R-165 | **ĐÓNG** | Restore Default là chức năng thật: `POST /api/email-templates/{id}/restore-default`. Nguồn mặc định là embedded resource **sinh từ canonical seed**, KHÔNG phải bảng mới, KHÔNG phải nội dung DB hiện tại, KHÔNG phải chạy SQL tay. `EmailTemplateRestoreDefaultTests` (13), `EmailTemplateDefaultsParityTests` (6) |
| R-166 | **ĐÓNG** | Token đổi từ `updated_at` sang cột `revision` đơn điệu; so sánh nằm **trong** câu UPDATE. `Two_saves_inside_the_same_second_are_still_distinguished` |
| R-167 | **ĐÓNG** | Nguyên nhân: test isolation, không phải lỗi sản phẩm — xem R-176. 20/20 lần race liên tiếp xanh |

### Restore Default (G11-I)

| # | Yêu cầu | Test |
|---|---|---|
| R-168 | Restore đủ 6 trường sửa được, cả VI và EN | `Restoring_an_edited_template_returns_all_six_fields_to_the_shipped_default`, `Restoring_returns_both_the_vietnamese_and_the_english_content` |
| R-169 | Mặc định KHÔNG lấy từ nội dung DB hiện tại | `The_default_does_not_come_from_the_current_database_content` |
| R-170 | Mẫu nhạy cảm restore được | `A_sensitive_template_restores_to_its_shipped_content` |
| R-171 | Không đụng field registry-owned | `Restoring_does_not_move_any_registry_owned_field` |
| R-172 | Mã lạ / mẫu không tồn tại bị từ chối | `An_unknown_template_is_refused_with_a_stable_code`, `Restoring_a_template_that_does_not_exist_is_a_not_found` |
| R-173 | Có audit kèm nội dung bị thay | `A_restore_writes_an_audit_row_carrying_the_replaced_content` |
| R-174 | 30/30 mẫu có default hợp lệ theo contract | `Every_system_template_has_a_shipped_default`, `Every_shipped_default_satisfies_its_own_contract` |
| R-175 | Registry ↔ canonical SQL ↔ catalog ACTIVE không drift | `Every_shipped_default_matches_the_freshly_imported_canonical_catalog`, `The_registry_the_defaults_and_the_active_catalog_hold_the_same_codes` |
| R-176 | Default không chứa token/URL thật, không mojibake | `No_shipped_default_contains_a_baked_in_token_or_action_url`, `The_vietnamese_defaults_are_not_mojibake` |
| R-177 | FE: chỉ hiện khi có default + có revision; xác nhận trước; hiện nội dung mới | `TemplateManagement.test.tsx` — 8 test mục "restore to default" |

### Optimistic concurrency

| # | Yêu cầu | Test |
|---|---|---|
| R-178 | Update/restore đúng revision → tăng đúng 1 | `An_update_with_the_current_revision_succeeds_and_bumps_it_by_one`, `A_restore_with_the_current_revision_succeeds_and_bumps_it_by_one` |
| R-179 | Revision cũ → 409, **không ghi gì** | `A_stale_revision_is_refused_and_changes_nothing`, `A_restore_carrying_a_stale_revision_is_refused` |
| R-180 | Hai update / update-vs-restore cạnh tranh → đúng 1 thắng | `Two_updates_racing_the_same_revision_produce_exactly_one_winner`, `An_update_and_a_restore_racing_the_same_revision_produce_exactly_one_winner` |
| R-181 | Validation/thiếu token thất bại → revision KHÔNG tăng | `A_content_validation_failure_leaves_the_revision_untouched`, `An_update_without_a_revision_is_refused_and_writes_nothing` |
| R-182 | SQL: điều kiện version nằm trong cùng câu lệnh | `03_verify.sql` — `conditional_update_matches_current_revision` / `..._refuses_a_stale_revision` |

### G11-H mở rộng

| # | Yêu cầu | Test |
|---|---|---|
| R-183 | **Đường vòng qua phân quyền đã đóng**: `POST /api/Emails/updateemailtemplate` từng chỉ có gate 5-role cấp class → Staff/Department sửa được mẫu hệ thống | `EmailsController` per-action `[RoleAuthorize(Ho)]`; PERMISSION_MATRIX §5.5 |
| R-184 | Reply All: gửi sender + người nhận **hiện**, loại current user, dedupe, TO>CC>BCC | `ReplyRecipientPlannerTests` (15), `ReplyAllJourneyTests` (6) |
| R-185 | Reply All **không bao giờ** mang BCC gốc — kể cả người gửi vốn là BCC | `Reply_all_never_carries_a_blind_copy_from_the_original`, `A_real_reply_all_reaches_the_visible_recipients_and_never_the_blind_one`, `A_blind_copy_recipient_replying_all_does_not_reveal_themselves` |
| R-186 | Recipient set đã normalize tham gia fingerprint; đổi TO/CC/BCC → khác request, đổi hoa-thường/thứ tự → cùng request | `EmailSendRecipientFingerprintTests` (18) |

---

## G11-H evidence closure — R-187 đến R-193 (2026-07-30)

Lượt này **không thêm production code**. Nó bổ sung thứ báo cáo trước còn thiếu: ma trận truy vết 16 đường
(`10-g11-final-closure.md` Mục 8) và **ba journey real-stack** — trình duyệt thật → React thật → API thật →
MySQL disposable → dispatcher thật → history thật.

Vì sao vẫn cần dù suite API đã xanh: mọi tính chất dưới đây là tính chất của một lần **trao tay**. Một
integration test post payload đã đúng dạng thì chứng minh nửa phía server và giả định nửa còn lại — chính là
nửa mà lỗi hay xảy ra. Một CC bị UI gộp thành TO trên đường ra là vô hình với nó.

| # | Yêu cầu | Bằng chứng |
|---|---|---|
| R-187 | Compose giữ TO/CC/BCC thành **ba nhóm rời** từ màn hình tới dispatcher; sai định dạng / trùng trong nhóm / trùng chéo nhóm bị chặn tại field; bắt buộc ≥1 TO | `email-envelope.realstack.spec.ts` — Journey A |
| R-188 | Draft lưu đúng `recipient_type`; **reload cả trang** rồi mở lại vẫn về đúng nhóm; sửa rồi gửi vẫn đúng | Journey A (đọc thẳng `email_draft_recipients` + `sent_email_recipients`) |
| R-189 | Hoa-thường: **giữ nguyên** khi lưu, **gấp về chữ thường** khi trao cho transport; so sánh trùng lặp không phân biệt hoa thường | Journey A |
| R-190 | History theo người xem: sender thấy cả BCC; TO/CC không thấy dấu vết nào (kể cả đếm/cờ/tổng); người bị BCC thấy hàng của chính mình; người ngoài **403** | Journey A2 |
| R-191 | Reply: TO do server suy, không có field TO để nhập; **không** kế thừa CC/BCC bản gốc; đúng 1 hàng người nhận; có `provider_thread_id` | Journey B |
| R-192 | Reply All: client gửi **mode qua URL**, body **không có** to/cc/bcc; server suy sender + người nhận hiện, loại chính mình; BCC gốc được gửi **đúng một lần** trên bản gốc và không đâu khác | Journey C |
| R-193 | MIME thật: **không có header `Bcc:`** trong bất kỳ thư nào; địa chỉ BCC không nằm trong header/body/attachment của thư người khác nhận | Journey A + test `No message produced by this run carries a Bcc header`, chế độ SMTP pickup |

**Hai chế độ dispatcher, vì "đã được gửi tới" và "không ai thấy" là hai tính chất ngược nhau** — xem
`10-g11-final-closure.md` Mục 10, gồm cả lý do `X-Receiver` trong file pickup **không** phải rò rỉ.

**Defect tìm được (hạ tầng test, không phải production):** `queryDb` cắt dòng bằng `'\n'` nên cột cuối của
mọi hàng trừ hàng cuối còn dính `\r` → helper đúng với 1 hàng và âm thầm sai với nhiều hàng. Đã sửa ở
`tests-realstack/departmentRealstackHelpers.ts` và `tests-realstack/emailRealstackHelpers.ts`. Chi tiết:
`10-g11-final-closure.md` Mục 11.

---

## G11-I mở lại — R-194 (2026-07-30)

| # | Yêu cầu | Test |
|---|---|---|
| R-194 | Màn quản lý mẫu phải tải **toàn bộ** danh mục, không phải 10 mẫu đầu; tìm kiếm phải thấy mẫu ở "trang sau"; tải thiếu phải hiện cảnh báo thay vì render tập con trông như đầy đủ | `TemplateManagement.test.tsx` — mục *"the whole catalog is loaded"* (5 test) |

Bug: `getEmailTemplateList()` gọi không tham số → `PageSize` mặc định 10 trên danh mục 30. Vì màn lọc phía
client và không có phân trang, 20 mẫu còn lại **không tới được** — mà đây là surface duy nhất có "Chỉnh sửa"
và "Phục hồi mặc định", cả hai chỉ HO. Chi tiết + vì sao vòng kiểm chứng trước không bắt được:
`10-g11-final-closure.md` Mục 12.

**Bằng chứng test bắt được lỗi:** tạm hoàn nguyên đúng dòng sửa → **4/5 đỏ**; khôi phục → **31/31 xanh**.
Mock của list được viết lại để mô phỏng đúng server (gồm cả mặc định 10) — mock cũ trả đủ 30 bất kể tham số,
nên test viết trên nó sẽ xanh ngay trên chính con bug.
