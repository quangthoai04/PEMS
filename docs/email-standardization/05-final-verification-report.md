---
type: verification-report
status: approved
updated: 2026-07-29
links:
  - docs/email-standardization/00-preflight-baseline.md
  - docs/email-standardization/01-email-caller-template-audit.md
  - docs/email-standardization/02-decisions-and-contracts.md
  - docs/email-standardization/03-system-template-catalog.md
  - docs/email-standardization/04-requirement-test-traceability.md
  - docs/email-standardization/06-deployment-readiness-runbook.md
  - docs/database/scripts/email_template_cc_bcc_sync/04_rollback_guidance.md
---

# Final verification report — G7, G8, G9

Measured 2026-07-29 on branch `Canh-Iter1` at `c39e6f0404978a5a05b0c52681e01c8837fc4b29`, with the
G6 working tree intact. Nothing here was committed, pushed or deployed.

> **Superseded in part — 2026-07-30.** G11 (`07-g11-residual-technical-closure.md`), then
> G12 + G11-H/I/J (`09-g12-contact-guard-and-template-contract.md`), then G11 final closure
> (`10-g11-final-closure.md`) moved several numbers this report pins. Where they disagree, the later
> document is current:
>
> | | This report (G9) | Current (G11 final closure) |
> |---|---|---|
> | Canonical SQL SHA-256 | `18e97d4d…e286b8` | **`16010f54…b854f2f0`** |
> | Base tables | 82 | **83** (`email_send_idempotency`, G11) |
> | Unit / Architecture / Integration | 1730 / 14 / 1020 | **1853 / 14 / 1277** |
> | Frontend | 891 tests, 68 files | **972 tests, 71 files** |
> | `contact_guard_negative_failures` | 14 (reported) | **0** — the 14 was a broken self-test handler, not a broken trigger |
> | Template concurrency token | — | `revision` column (was `updated_at`, which could not tell two saves in one second apart) |
>
> Backend build stayed 0 errors / 208 warnings throughout. Nothing in this report was retracted; it was
> extended.

---

## 1. Preflight

| Check | Expected | Measured | |
|---|---|---|---|
| Branch | `Canh-Iter1` | `Canh-Iter1` | ✅ |
| HEAD | `c39e6f04…` | `c39e6f0404978a5a05b0c52681e01c8837fc4b29` | ✅ |
| `origin/Cảnh-Iter1`, `origin/Dev` | `c39e6f04` | both `c39e6f04` | ✅ |
| Local upstream | not configured | not configured | ✅ |
| Stashes | 9 | 9 | ✅ |
| WIP at start | 21 modified + 14 untracked | 21 + 14 | ✅ |
| Deletions | 0 | 0 | ✅ |
| `git diff --check` | clean | exit 0 (CRLF warnings only) | ✅ |
| Conflict markers | 0 | 0 | ✅ |

### 1.1 Two baseline values in the brief did not match HEAD

Both are stale documentation, not drift introduced by this work. Neither required an owner decision: the
repository is internally consistent and its own contract tests assert it in both directions.

| Value | Brief | Measured at HEAD | Explanation |
|---|---|---|---|
| Canonical SQL SHA-256 | `51e178bb…aae8a1` | **`18e97d4d…e286b8`** | `51e178bb` is the pre-merge hash recorded in `02-decisions-and-contracts.md:69` as "after 7a-fix". Merge `c39e6f04` (2026-07-28) renamed the script to `PEMS_FULL_V2_NO_SEED_DATA_GALLERY_DOCUMENT_AI_FIXED.sql` and merged Dev's schema into it. `CanonicalSqlScript.ExpectedSha256` reads `18e97d4d…`, and the file on disk hashes to exactly that — raw *and* LF-normalised, because the worktree holds it with LF. The file is unmodified; the doc is old. |
| Active catalog size | 26 templates | **30 templates** | Same merge added four `DEPT_*` templates. Seed and registry are identical sets, measured: 30 = 30, `diff` empty. |

A third, minor one: `PEMS_EMAIL_TEMPLATE_CC_BCC_IMPLEMENTATION_PLAN.md`, `04-automated-caller-g4-closure.md`
and `05-manual-email-draft-reply-history-g5.md` are not in `docs/email-standardization/` — they live in
`docs/Ver2Carnh/canh/email/`. They were read from there.

---

## 2. G7 — canonical SQL and database sync

### 2.1 G7a re-confirmed (not rewritten)

Fresh import of the canonical script into a disposable database, then measured:

| Property | Measured |
|---|---|
| Canonical file resolved | `docs/database/scripts/PEMS_FULL_V2_NO_SEED_DATA_GALLERY_DOCUMENT_AI_FIXED.sql`, the only `PEMS_FULL_*.sql` at top level |
| Normalised SHA-256 | `18e97d4dce754353f5d19decc304c46f4d8f8dab3364d24ebdec9ba907e286b8` — matches the pinned constant |
| Base tables / triggers / foreign keys | 82 / 32 / 252 — identical to the pinned baseline |
| Templates total / active / inactive | 30 / 30 / 0 |
| Duplicate `template_code` | 0 |
| Missing VI or EN content | 0 |
| Templates scoped to a campus | 0 |
| Seed ↔ registry code sets | identical in both directions (`diff` empty) |
| Placeholders vs `variables_text` vs registry `DeclaredVariables` | agree for all 30, `{{actionBlock}}` excluded by design (present in 14 bodies) |
| Numeric `email_template_id` dependency in code | none — lookup is `t.TemplateCode == code` |
| Historical `sent_emails` | 22 rows, **all** with `email_template_id IS NULL`, subject and `body_snapshot` intact |

The brief said 25 historical rows; the measured count is 22, again a pre-merge figure. What matters is the
property, and it holds: every historical row is detached and none had its snapshot rewritten.

Gallery and other out-of-scope seed: the canonical script is byte-identical to HEAD (`git status` reports
it unmodified), so nothing outside scope changed.

### 2.2 G7b — the four scripts

`docs/database/scripts/email_template_cc_bcc_sync/`

| File | Lines | What it is |
|---|---|---|
| `01_preflight.sql` | 208 | Read-only survey: connection, required schema (fail-closed), protected-database advisory, catalog classification, reference counts, six preservation checksums |
| `02_sync_templates.sql` | 930 | Guarded upsert by `template_code`. **Generated**, not hand-written: the 30 `VALUES` rows are lifted verbatim from the canonical script's own `INSERT INTO email_templates` block, so the two cannot drift |
| `03_verify.sql` | 358 | 17 checks with an explicit PASS/FAIL verdict, ending in a `SIGNAL` so a failure is a non-zero exit status rather than something to scroll past |
| `04_rollback_guidance.md` | — | Four different meanings of "roll back", what must survive any of them, and when not to roll back at all |

Design decisions worth naming:

* **Legacy codes are enumerated, not inferred.** The nine DL-03 codes are listed by name. "Everything not
  in the canonical set" would also sweep up templates an operator authored in the admin UI, and no column
  distinguishes the two after the fact.
* **Deactivate, never delete.** `sent_emails.email_template_id` and `email_drafts.email_template_id` are
  `ON DELETE SET NULL`; deleting a legacy row would silently unlink history from its template.
* **The update has a difference predicate** using `<=>`, so a converged database is not rewritten and
  `updated_at` does not churn.

### 2.3 G7 evidence

Fresh disposable database, then an "existing deployment" fixture: 3 canonical templates absent, 4 stale or
wrongly deactivated, all 9 legacy codes `ACTIVE`, history and drafts holding foreign keys into them, and 2
operator-authored templates.

| Step | Result |
|---|---|
| Fresh import → verify | 16 PASS / 0 FAIL / 1 INFO |
| Existing fixture → preflight | exit 0, read-only confirmed |
| Sync run #1 | 3 inserted, 4 updated, 9 deactivated |
| Verify after #1 | 16 PASS / 0 FAIL / 1 INFO |
| Sync run #2 | **0 / 0 / 0** |
| Verify after #2 | 16 PASS / 0 FAIL / 1 INFO |
| Snapshot after #1 vs after #2 | **byte-identical**, `updated_at` included |
| Existing rows' ids | preserved (e.g. `ACCOUNT_EMAIL_CONFIRMATION` id 1, `AUTH_PASSWORD_RESET_OTP` id 13, legacy ids 31–39) |
| Legacy content hashes | unchanged — only `status` moved `ACTIVE → INACTIVE` |
| Operator-authored templates | untouched, including the `ACTIVE` one |
| Non-template rows (history, recipients, drafts, draft recipients, tokens, attachments, and 6 out-of-scope tables) | 71 = 71, identical |
| Canonical SQL after all runs | hash unchanged |

**A measurement error found and corrected mid-run.** The first snapshot script queried a table named
`gallery_files`, which does not exist. The MySQL client aborted the script at that statement, so all six
out-of-scope rows were silently missing and the first "nothing outside `email_templates` changed" claim
covered only history/drafts/tokens. Corrected to `gallery_items`, the fixture rebuilt and the whole
sequence re-run; the numbers above are from the corrected run.

**A real weakness found in the guard.** The confirmation is a session variable, and the automated tests
exposed that a pooled connection carried it forward: after one sync, a second run on the same connection
proceeded with no confirmation at all. Fixed by having the script clear the variable as its last
statement, so one confirmation authorises exactly one run, and pinned by
`Sync_spends_the_confirmation_so_the_same_session_cannot_reuse_it`.

**Automated:** `EmailTemplateSyncScriptTests` — 23 tests, own imported database (it mutates
`email_templates`, which `SystemEmailTemplateContractTests` asserts is pristine; sharing one database
would make both classes' verdicts depend on execution order). Run three consecutive times: 23/23 each.

**G7: ĐẠT.**

---

## 3. G8 — real-stack journeys

### 3.1 Environment

| | |
|---|---|
| Backend | the repository's real handlers, renderer, dispatcher and `EmailService` |
| Database | disposable MySQL 8.0.46 per run, imported from the canonical script and dropped afterwards |
| Mail sink | `Smtp:PickupDirectory` — .NET serialises real `.eml` files to disk instead of connecting to a server |
| Evidence | parsed MIME on disk (headers and decoded body), plus the rows in `sent_emails` / `sent_email_recipients` |
| Real email sent | **none** |
| `pems_db` | never connected to |
| Cleanup | each suite deletes only the rows carrying its own marker address; disposable databases dropped |

The pickup directory is what makes these journeys real rather than mocked: the assertions read the bytes
that would have gone to a provider, not an intention recorded in a fake.

### 3.2 Journey coverage

Most of the seven journeys were already covered by the G4/G5/G6 suites. Rather than duplicating them for
test count, coverage was inventoried and only the genuinely unproven parts were added.

| Journey | Already proven by | Added in G8 |
|---|---|---|
| E2E-01 manual compose TO/CC/BCC | `ManualEmailPipelineTests`, `EmailMimeEnvelopeTests` — draft round-trip, one message per action, To/Cc correct, no Bcc header, DB rows match, `SENT` not `DELIVERED`, one history row | — |
| E2E-02 BCC privacy | `SentEmailHistoryAuthorizationTests` (7 viewer types), `FileDownloadAuthorizationTests` (attachment surface) | — |
| E2E-03 security email | `AccountEmailEndToEndTests`, `EmailMimeEnvelopeTests` (CC/BCC refused), `PendingAccountLoginBlockTests`, `ConfirmAccountEmailCommandHandlerTests` (one-time token), `EmailServiceSensitiveLoggingTests` | — |
| E2E-04 DB content hot change | `EmailTemplateRendererTests`, `AccountEmailEndToEndTests`, `ReportEmailEndToEndTests` | **4 tests** proving the preview shares the renderer |
| E2E-05 invitation token isolation | `ParticipantInvitationLinkageTests` (transaction/rollback) | **2 tests** proving two messages, one addressee each, distinct links, per-recipient token binding |
| E2E-06 reply | `ManualEmailPipelineTests` — reader-only, backend-resolved TO, no BCC hydration, threading headers, parent untouched | **1 test** proving attachments are not copied |
| E2E-07 report/invoice | `ReportEmailEndToEndTests` (6 actions C24–C29), `ReportInvoiceRouteTests`, `InvoiceMoneyTests` | — |
| Negative/failure | provider reject, template missing/inactive, CR/LF subject, duplicate recipients, security CC/BCC, outsider attachment, path traversal — all covered | **2 tests** proving the recipient ceiling on the real send path |

`EmailG8JourneyTests` — 9 tests, all green.

### 3.3 The two routes with no UI

`sendStaffLeaderDeptInvoice` and `sendDeptLeaderInvoiceToStaffLeader` have **no `.tsx` caller** — measured:
2 definitions in `reportsApi.ts`, 0 references anywhere else. They were exercised at the route/API level
(`ReportInvoiceRouteTests`); no UI was invented for them. Carried as product-integration debt (R-105).

**G8: ĐẠT.**

---

## 4. G9-A — ledger disposition

| # | Item | Disposition |
|---|---|---|
| 10.1 | Three dashboard scaffold endpoints | **Re-verified on HEAD.** Class-level `[Authorize]` blocks anonymous; the three have no `[RoleAuthorize]`; all three handlers still `throw new NotImplementedException`. No role chosen, no handler implemented. Doc drift corrected (§V11.2 of `PROJECT_OVERVIEW`). Blocking condition preserved: the PERMISSION_MATRIX (HO+StaffLeader+DeptLead, UC-69/70/71) vs FE-08 (HO+StaffLeader, UC-66/67/68) conflict — both role set *and* UC ids — must be resolved by the owner before `NotImplementedException` is removed. **Carried (R-104).** |
| 10.2 | Report-email retry idempotency | Re-checked all six actions: no idempotency key, no deduplication predicate anywhere in `backend/PEMS.Application/Reports/`. No predicate invented — a `(sender, target, period)` guess would block legitimate re-sends. **Carried as residual risk (R-103):** a retry after a network timeout can send a second email. |
| 10.3 | Two invoice endpoints without a UI caller | Confirmed by measurement. Proven at the API level in G8. **Carried (R-105).** |
| 10.4 | Header identity hardening | **Fixed.** Central `EmailRecipientValidator.ReservedHeaderNames` refuses `From`, `Sender`, `Reply-To`, `Return-Path`, `To`, `Cc`, `Bcc`, `Message-Id` from `OutboundEmail.Headers`. `In-Reply-To`/`References` still pass. `Message-Id` moved to a typed `OutboundEmail.MessageId` so identity is a field, not a bag entry. 11 tests. |
| 10.5 | `SendHoCampusReport` in-handler role check | **Not a gap — the G6 note was wrong.** `SendHoCampusReportCommand.cs:58` already calls `HoReportV2Guard.RequireHo(_currentUser)`, which is an in-handler check independent of the controller attribute. Removed from the ledger. |
| 10.6 | Doc drift | **Fixed.** `PROJECT_OVERVIEW` §V11.2 rewritten against measured state: all four named controllers now carry class-level `[Authorize]` and none is anonymously callable. The `DashboardController` `[AllowAnonymous]` warning was **not** re-verified this round and is left standing, marked as such. |
| 10.7 | SSR sanitizer fallback | **Confirmed unreachable.** Zero references to `react-dom/server`, `renderToString`, `renderToStaticMarkup`, prerender or SSR anywhere in `src/`, the Vite config or `package.json`. The regex branch is **not** described as fail-closed; caller-level sanitisation tests retained. No regex sanitizer was written. |
| 10.8 | `FileDownloadAuthorizationTests` | Two further isolated runs, 14/14 each, plus two unfiltered full-suite runs. Not reproduced — **7 clean executions** since the single G6.5 failure. Still not labelled flaky. Temp storage: 47 `pems-*` directories exist, all dated 2026-07-22; **none created by this run**, so none deleted. |
| 10.9 | TypeScript gate | Kept independent. `npx tsc --noEmit` exit 0, run separately from vitest. |
| 10.10 | `SentEmailDetail.canMarkComplete` | **Fixed at the source of authority.** `ViewEmailDto` never carried the field, so the button was invisible to everyone while `MarkEmailCompletedCommandHandler` stood ready to accept the call. Added `SentEmailAccess.CanMarkComplete(relation, deliveredAt)` and made **both** the query and the command consult it. Not hard-coded `true`; not inferred from role. 12 tests, including an equality assertion over every relation × completion state. The old command also compared recipient addresses with a case-sensitive `==`, which is now gone. |
| 10.11 | Redundant negative-price pre-check | **Fixed.** Removed from `ExportDeptLeaderInvoiceCommandHandler`. All three invoice paths now validate through `InvoiceMoney.ValidateUnitPrice` per item, **after** the item is matched to a row the caller owns — the pre-pass rejected prices before scope was established, so it could tell a caller their price was invalid on an item that was not in their department. |

---

## 5. G9-B — full regression, unfiltered

| Gate | Result | Baseline |
|---|---|---|
| Backend solution build, `--no-incremental` | **0 errors, 208 warnings** | 208 |
| Backend unit tests | **1730 / 1730** | 1721 |
| Architecture tests | **14 / 14** | 14 |
| Integration tests, no filter | **1020 / 1020** | 975 |
| Frontend unit tests (project config, default timeouts) | **891 / 891** across **68 files** | 891 / 68 |
| `tsc --noEmit` | **exit 0** | exit 0 |
| Vite production build | **exit 0** | exit 0 |
| SQL fresh import + verify | pass | — |
| SQL sync ×2 + verify ×2 | pass, second run a no-op | — |
| File-sink / fake-SMTP journeys | pass | — |

0 failed, 0 skipped, everywhere. No test was deleted, skipped, retried, timeout-extended or loosened to
get here.

The warning count is worth one sentence: the first non-incremental build after the new tests reported
**209**. The extra one was mine — an EF1002 on an interpolated `ExecuteSqlRawAsync` in a test I had just
written. Parameterised it; back to 208, which is the baseline exactly.

---

## 6. Test accounting

| Suite | Baseline | Now | Δ |
|---|---|---|---|
| Backend unit | 1721 | 1730 | **+9** |
| Architecture | 14 | 14 | **0** |
| Integration | 975 | 1020 | **+45** |
| Frontend | 891 | 891 | **0** |
| Frontend files | 68 | 68 | **0** |

Every added test accounted for:

| Where | Δ | What |
|---|---|---|
| `SentEmailAccessTests` (unit, tracked) | +9 | `CanMarkComplete`: 8 relation/state cases + 1 equality assertion over the full cross-product |
| `EmailTemplateSyncScriptTests` (integration, **new file**) | +23 | G7 sync scripts |
| `EmailG8JourneyTests` (integration, **new file**) | +9 | G8 journeys |
| `SentEmailHistoryAuthorizationTests` (integration, tracked) | +3 | `CanMarkComplete` at the API surface |
| `EmailMimeEnvelopeTests` (integration, tracked) | +10 | 8-case denylist theory + 3 facts, minus 1 replaced |
| | **+45** | matches the measured integration delta exactly |

**Tracked vs untracked:** 2 new untracked test files (32 tests); 4 tracked test files appended to.

**Removed or replaced:** one test — `A_header_bag_entry_cannot_take_over_the_From_the_Sender_or_the_Reply_To`.
It asserted what .NET *happened* to do with identity headers (From overwritten, Sender and Reply-To
dropped) and said nothing about `Return-Path`, which did survive into the file, or about To/Cc/Bcc. It was
replaced by the denylist theory, which makes the refusal our rule instead of the library's. That test was
written during G6's uncommitted WIP and **does not exist at HEAD**, so `git diff --numstat` against HEAD
still reports **0 deleted lines** in all four tracked test files (`136 0`, `24 0`, `107 0`, `138 0`).

No frontend test changed, and no frontend source changed: `canMarkComplete` was fixed entirely on the
server, and `SentEmailDetail` reads the payload untyped, so the button now appears with no client edit.

---

## 7. Static scan

`rg` over the working tree including untracked files. Every hit classified by reading source **and** sink,
not by filtering one line.

| Pattern | Hits | Disposition |
|---|---|---|
| `DeliveryStatus = "DELIVERED"` | 0 | — |
| `delivered_at` | 6 | Schema + the completion path. `DELIVERED` is never written on provider acceptance. |
| `document.write` | 1 real call | `printDocument.ts:42` — `doc.write(PRINT_SKELETON)`, a static constant with no interpolation. The other 2 hits are comments describing why. **Production-safe.** |
| `.outerHTML`, `insertAdjacentHTML`, `srcDoc` | 0 | — |
| `dangerouslySetInnerHTML` / `.innerHTML` (email scope) | 6 sinks | All six traced source → sink: `TemplateManagement:309`, `EmailManagement:469`, `SentEmailDetail:173`, `SentEmailsModal:266`, `EmailPreviewModal:326`, `EmailComposeModal:645`. Each derives from `sanitizeHtml(...)` at its origin; `SentEmailsModal` additionally passes through `sanitizeSentEmailPreviewHtml` and `resolveCidImages`, both fed the already-sanitised string. **Production-safe.** |
| `javascript:`, `onerror` | 16 each | Sanitizer allow-list rules and their tests. **Test fixture / sanitizer internals.** |
| `Return-Path` | 7 | Denylist entry + its tests + comments. **Violation fixed.** |
| `From`, `Sender`, `Reply-To`, `To`, `Cc`, `Bcc`, `Message-Id` in the header bag | — | **Violation fixed** — refused at `EmailService.BuildMessage`. |
| `otpCode`, `rawToken`, `actionBlock` | 41 / 35 / 20 | Template variables, trusted-block plumbing, and the tests asserting they never reach history or logs. **Production-safe.** |
| `email_template_id` | 25 | Schema, foreign keys, and the tests proving nothing depends on the *numeric* value. No numeric literal assignment anywhere. **Production-safe.** |
| template IDs `1..16` | 0 | — |
| Old template codes | 9, all in the sync script's explicit legacy list and its tests | **Accepted by design.** |
| `NotImplementedException` | 45 | 3 are the dashboard scaffolds (R-104); the rest belong to other modules, outside email scope. |
| `canReply` / `canMarkComplete` | 6 / 2 | DTO field + FE read + tests. Both now server-decided. |
| `unitPrice` | 40 | `InvoiceMoney` and the three invoice paths. **Production-safe.** |
| Hard-coded email subject/body | **1 file** | **Violation found and fixed** — see below. |

### 7.1 The one violation the scan found

`DepartmentPersonnelEmails.cs` still held six email subjects and six HTML body builders. Measured: **zero
references** across `backend/` and `tests/` — the six handlers had moved onto `ISystemEmailDispatcher` and
render from `email_templates`, leaving this behind.

Dead, but still exactly what G2 set out to eliminate: email wording living in code, uneditable by an
operator and free to disagree with the catalog. Two copies of an email's text is one too many even when
only one is reachable — the next person needing a notice here would have found a working-looking builder.
Removed (114 lines); the five `Status*` constants, which are live, stay. The compiler confirms nothing
referenced them: 0 errors, and the warning count is unchanged at 208.

---

## 8. Files changed in G7–G9

**Production (10):**

| File | Δ | Why |
|---|---|---|
| `Emails/Common/EmailRecipientValidator.cs` | +42 | `ReservedHeaderNames` + `AssertHeaderNameAllowed` |
| `Emails/Common/SentEmailAccess.cs` | +43 | `CanMarkComplete` |
| `Emails/Common/ManualEmailSender.cs` | +13 −6 | `Message-Id` moved to the typed field |
| `Common/Interfaces/IEmailService.cs` | +18 | `OutboundEmail.MessageId` + denylist contract note |
| `Infrastructure/Email/EmailService.cs` | +10 | Applies the denylist; sets `Message-Id` from the typed field |
| `Infrastructure/Email/FileSinkEmailService.cs` | +1 | Records `messageId` in the sink |
| `Emails/Queries/ViewEmail/ViewEmailDto.cs` | +15 | `CanMarkComplete` |
| `Emails/Queries/ViewEmail/ViewEmailQueryHandler.cs` | +7 −1 | Populates it from the shared predicate |
| `Emails/Commands/MarkEmailCompleted/…Handler.cs` | +12 −9 | Consults the same predicate; drops the case-sensitive address compare |
| `Reports/Commands/ExportDeptLeaderInvoice/…Handler.cs` | +14 −4 | Redundant pre-check removed |
| `DepartmentLeaderPersonnel/Common/DepartmentPersonnelEmails.cs` | +16 −114 | Dead hard-coded email content removed |

**SQL (new folder, 4 files):** `docs/database/scripts/email_template_cc_bcc_sync/`

**Tests:** 2 new files (`EmailTemplateSyncScriptTests`, `EmailG8JourneyTests`); 4 tracked files appended.

**Docs:** `PROJECT_OVERVIEW` §V11.2 corrected; `04-requirement-test-traceability.md`,
`05-final-verification-report.md`, `06-deployment-readiness-runbook.md` added.

---

## 9. Repository integrity

| Check | Result |
|---|---|
| Branch / HEAD | `Canh-Iter1` / `c39e6f0404978a5a05b0c52681e01c8837fc4b29` — unchanged |
| Stashes | **9** — unchanged, none dropped or modified |
| `git diff --check` | exit 0 |
| Conflict markers | 0 |
| Deleted paths | 0 |
| WIP | 29 modified + 17 untracked (from 21 + 14) |
| Canonical SQL | `18e97d4d…e286b8` — unchanged |
| Artifacts in the tree (`.eml`, `.pdf`, `.trx`, logs, coverage, TestResults, `dist/`) | none |
| Disposable credentials or tokens in the WIP | none — the only credentials are the pre-existing local `root/123456` in committed `appsettings*.json`, untouched |
| Commits / pushes / PRs / merges / deploys | **none** |

---

## 10. Residual risks and owner decisions

> **Superseded in part by G11 (2026-07-29).** R-103 and R-106 are closed; see
> `07-g11-residual-technical-closure.md`. R-104 and R-105 remain open, with decision evidence prepared in
> `08-open-product-decisions.md`. The rows below are the state as of G9 and are kept as the record of what
> was carried at that gate.

| # | Risk | Severity | What is needed |
|---|---|---|---|
| ~~R-103~~ | A report-email retry after a network timeout can send a second email | Medium | **CLOSED in G11** — persistent reservations on all six send actions. Exactly-once delivery is still not claimed. |
| R-104 | Three dashboard scaffold endpoints have no role gate | Low (anonymous blocked; handlers throw; no data disclosed) | Owner resolves the PERMISSION_MATRIX vs FE-08 conflict — both role set and UC ids — then `[RoleAuthorize]` before `NotImplementedException` goes. |
| R-105 | Two invoice-send routes have no UI entry point | Low (routes work and are authorised) | A UX/use-case decision on where the action belongs. Both routes are now under the G11 idempotency contract, so a UI added later inherits it. |
| ~~R-106~~ | 9 of 30 templates cannot be rendered by the preview modal | Low (unreachable: the modal is wired only to the 5 registered action templates) | **CLOSED in G11** — 30/30 preview in VI and EN, with send security unchanged. |
| — | `FileDownloadAuthorizationTests` failed once during G6.5 | Unknown | Not reproduced in 7 subsequent executions. Deliberately not labelled flaky. |
| — | `DashboardController` still has `[AllowAnonymous]` | Not assessed | Outside email scope; the original V11.2 warning is preserved rather than silently dropped. |

---

## 11. Conclusion

| Gate | Verdict |
|---|---|
| G7 — canonical SQL and database sync | **ĐẠT** |
| G8 — file-sink / real-stack E2E | **ĐẠT** |
| G9 — ledger, traceability, full regression, static scan | **ĐẠT** |
| G10 readiness | **READY** |
| G10 execution | **NOT STARTED** — awaiting explicit owner approval |

The email-standardisation workstream is complete and verified. That is not the same as the product being
finished: four items above are carried deliberately, one of them (R-103) a real behaviour a user could hit.
They are recorded rather than closed because closing them needs decisions that are not the implementer's
to make.

### Update after G11 (2026-07-29)

| Gate | Verdict |
|---|---|
| G11 — residual technical closure (R-103, R-106) | **ĐẠT** |

R-103 and R-106 are closed; R-104 and R-105 remain open because both still need an owner decision, and
both now have the evidence needed to make one written up in `08-open-product-decisions.md`. G7–G9 were
not reopened, with one exception that qualified as a real regression: the G7 sync script did not set its
connection character set, and a run through the mysql CLI corrupted all thirty templates. Fixed,
re-measured, and asserted by test — see §3.4 of `07-g11-residual-technical-closure.md`.

### Update after G11-H evidence closure (2026-07-30)

The G11 final-closure report stated the G11-H conclusions but did not exhibit two things it claimed:
the **16-path traceability matrix** and **end-to-end evidence through a real browser**. Neither existed.
What existed was API-level integration coverage plus jsdom component tests — each real, neither one a
real-stack journey. This update supplies both.

| Gate | Verdict |
|---|---|
| G11-H — 16-path traceability matrix | **ĐẠT** — `10-g11-final-closure.md` §8, rebuilt from code |
| G11-H — Journey A: compose → preview → draft → reopen → send | **ĐẠT** — `email-envelope.realstack.spec.ts` |
| G11-H — Journey B: Reply | **ĐẠT** |
| G11-H — Journey C: Reply All | **ĐẠT** |
| G11-H — real MIME, no `Bcc:` header | **ĐẠT** — SMTP pickup mode |

**Chain exercised** (no network mocking, no real SMTP, no real mail):
`real Chromium → real React (Vite) → real .NET API → disposable MySQL from canonical SQL → real dispatcher
→ real persistence/history`.

**Two dispatcher modes, deliberately.** "The blind copy was addressed" and "the blind copy is invisible"
are opposite properties; one artefact showing both would prove neither. The default file-sink records the
three envelope groups separately and answers the first. `PEMS_E2E_SMTP_PICKUP=1` swaps in the real
`EmailService` writing real `.eml` files through `SpecifiedPickupDirectory` — no connection is ever
opened — and answers the second. See `10-g11-final-closure.md` §10, including why the `X-Sender` /
`X-Receiver` preamble in a pickup file is the transport envelope rather than a leak.

**Runs.** Email journeys: 5/5 in sink mode, 5/5 in pickup mode. Full real-stack suite, unfiltered:
green, then one red, then three consecutive green — the red is reported in full in the closure report and
was measured, not assumed, to be host starvation (two failures in a spec that imports none of the changed
helpers, 1.5h wall time against 2.7m for the runs either side, and 15/15 green on a focused repeat).

**No production code changed in this round.** The changes are one added spec, one added helper, a fix to
a shared test helper, and an opt-in mode in the E2E orchestrator. The .NET and vitest baselines are
therefore carried forward and re-verified rather than re-derived.
