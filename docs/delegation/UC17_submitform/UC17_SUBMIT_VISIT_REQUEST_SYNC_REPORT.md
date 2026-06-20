# UC-17 Submit Visit Request Sync Report

## Summary
- **SQL source of truth:** `database/scripts/pems_full(3).sql` (v8.3 — 42 tables)
- **UC-17 doc source:** `docs/delegation/PROMPT_AUDIT_SYNC_UC17_WITH_SQL_FULL.md` + `docs/GUIDE CLAUDE/PEMS_CLAUDE_PROJECT_INSTRUCTIONS.md`
- **Result:** **UPDATED** — entities/config/DbContext already matched SQL; the create flow was hardened to satisfy the UC-17 rules (full re-validation, campus ACTIVE check, transaction, duplicate guard, error codes).

The real UC-17 flow lives in `VisitRequestsController` (public, no auth) as a 2-step flow:

```
POST /api/visit-requests/initiate    → validate full form + send OTP (nothing persisted except otp_tokens)
POST /api/visit-requests/verify      → verify OTP + create visit_requests/+campuses/+guests (atomic)
POST /api/visit-requests/resend-otp  → resend OTP (per-email hourly limit)
```

> **Decision on routes:** The audit lists ideal routes `send-verification-code` / `verify-code` / `submit` with a separate short-lived `verificationToken`. The running React frontend uses `initiate` / `verify` and resubmits the full form together with the OTP code (the OTP itself is the proof-of-verification, single-use). Per the audit's rule *"không phá frontend đang chạy nếu chưa có migration rõ ràng"*, the 2-step flow was **kept** and hardened instead of being restructured. It is functionally equivalent and secure: the request is created **only after** the OTP is verified, and the full form is re-validated server-side at the create boundary.

---

## Mismatches Found & Fixed

| Area | Before | Expected (UC-17 / SQL) | Action |
|---|---|---|---|
| Re-validation at create step | `verify` validated only email + name + OTP | Full form must be re-validated server-side at submit | **Fixed** — shared rule set runs at both steps |
| Scope ↔ campus count | not enforced | SINGLE=1 campus, MULTI≥2, no duplicate campus | **Fixed** — in shared validator |
| Campus existence / state | raw `InvalidOperationException` (→500) on unknown code; ACTIVE never checked | exists + `status=ACTIVE` with clean error | **Fixed** — `CAMPUS_NOT_FOUND` / `CAMPUS_INACTIVE` (422) |
| Planned time | only end>start at the OTP step | end>start **and** not in the past | **Fixed** — `INVALID_VISIT_TIME` (422), 1-day TZ grace |
| Atomicity | OTP-verify, user-provision, request-insert ran as 3 separate `SaveChanges` (no transaction) | one transaction | **Fixed** — `BeginTransactionAsync` wraps the whole submit |
| Duplicate / idempotency | none | reject recent duplicate (409) | **Fixed** — `DUPLICATE_VISIT_REQUEST` (email+delegation+scope within 10 min, not rejected/cancelled) |
| Error codes | only auth errors carried `errorCode` | machine-readable codes per UC-17 §8 | **Fixed** — `errorCode` added to `ConflictException`/`BusinessRuleException` + middleware |
| `visit_request_campuses.status` | string literal `"WAITING_REQUEST_APPROVAL"` | constant; host fields NULL | **Fixed** — `VisitInstanceStatuses` constant; host fields explicitly NULL |
| `working_language` mapping | only VI/EN handled | VI/EN/OTHER | **Fixed** |

### Already correct (verified, no change needed)
- No `pending_visit_requests` table or DbSet; `initiate` persists **only** the OTP (`otp_tokens`) — no unverified form stored in DB.
- OTP stored **hashed** (`SecureTokenGenerator.Hash`, `token_hash`); never returned/logged in plain text.
- `visit_requests.status` only ever set to `PENDING_APPROVAL` at submit; operational statuses (`IN_PROGRESS/COMPLETED/…`) are **not** written here.
- Entities / column mappings match SQL v8.3 PKs (`visit_request_id`, `visit_instance_id`, `guest_member_id`, `agenda_id`, `otp_token_id` — all `BIGINT UNSIGNED`/`ulong`), column names, nullability and enums.
- Host assignment is **not** performed in UC-17 (`current_host_user_id`, `host_assigned_*`, `host_assignment_source` left NULL).
- Cancel/approve/reject are **not** in UC-17 — they live in `DelegationsController` (`ProcessVisitRequest`, `CancelVisitRequest` / UC-136).
- `visit_guest_members` uses `is_representative` (SQL name), no non-existent columns; `visit_agendas` is **not** inserted at submit (form has no agenda — correctly not faked).

---

## Files Changed

### Backend
- `PEMS.Application/Common/Exceptions/ConflictException.cs` — optional `ErrorCode`.
- `PEMS.Application/Common/Exceptions/BusinessRuleException.cs` — optional `ErrorCode`.
- `PEMS.Api/Middleware/ExceptionHandlingMiddleware.cs` — surface `errorCode` for 409/422.
- `PEMS.Domain/Constants/VisitRequestConstants.cs` — added `VisitInstanceStatuses` + `VisitRequestErrorCodes`.
- `PEMS.Application/Delegations/Commands/IVisitRequestFormCommand.cs` — **new** shared form contract.
- `PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs` — **new** shared FluentValidation rule set.
- `PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommand.cs` — implements `IVisitRequestFormCommand`.
- `PEMS.Application/Delegations/Commands/InitiateVisitRequest/InitiateVisitRequestCommandValidator.cs` — uses shared rules.
- `PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommand.cs` — implements `IVisitRequestFormCommand`.
- `PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandValidator.cs` — full re-validation + OTP rule.
- `PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/VerifyAndCreateVisitRequestCommandHandler.cs` — transaction + duplicate guard + commit/rollback; email after commit.
- `PEMS.Application/Common/Interfaces/IApplicationDbContext.cs` — `BeginTransactionAsync`.
- `PEMS.Infrastructure/Persistence/ApplicationDbContext.cs` — implements `BeginTransactionAsync`.
- `PEMS.Infrastructure/Services/VisitRequestService.cs` — campus existence/ACTIVE + planned-time business validation, constants, language mapping.

### Frontend
- None. The public flow already matches the security model (no server draft, full-form resubmit with OTP, no `IN_PROGRESS/COMPLETED` shown from request status).

### Database
- None. Code conformed to `pems_full(3).sql`; no schema change required.

### Docs
- `docs/delegation/UC17_SUBMIT_VISIT_REQUEST_SYNC_REPORT.md` (this report).
- `docs/architecture/REFACTOR_CHANGELOG.md` — entry appended.

---

## Insert Rules Verified
- [x] `visit_requests.status = PENDING_APPROVAL` at submit
- [x] `visit_request_campuses.status = WAITING_REQUEST_APPROVAL` at submit
- [x] `current_host_user_id = NULL` (and `host_assigned_by/at`, `host_assignment_source` NULL) before approval
- [x] `email_verified_at = now`, `visitor_user_id` linked
- [x] No `pending_visit_requests` table
- [x] No unverified form stored in DB (only OTP hash in `otp_tokens`)
- [x] No cancel/approve/host-assignment logic in UC-17

## Commands Run
```bash
# Build (dev server can lock bin → temp output path)
dotnet build backend/PEMS.Api/PEMS.Api.csproj -p:BaseOutputPath=./.tmp-build/
# Build succeeded. 0 Warning(s) 0 Error(s)

dotnet test tests/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj -p:BaseOutputPath=./.tmp-build/
# Passed! Failed: 0, Passed: 14, Skipped: 0
```
> Frontend not built — no frontend changes. `PEMS.ApplicationTests` has no `.csproj` (files not compiled), so unit tests for this UC could not be run; only `PEMS.ArchitectureTests` is a buildable test project.

## Manual / Runtime Tests (to run on a test DB)
OTP: send→generic success, hash-only in DB, resend>limit→error, wrong code→`attempt_count++`, expired→fail, correct→verify.
Submit: missing/invalid OTP→422; email≠verified handled by OTP single-use; SINGLE with 0/2 campuses→400; MULTI with 1→400; inactive campus→`CAMPUS_INACTIVE`; end≤start→`INVALID_VISIT_TIME`; valid→`visit_requests=PENDING_APPROVAL` + `visit_request_campuses=WAITING_REQUEST_APPROVAL` + host NULL; double-submit ≤10 min→`DUPLICATE_VISIT_REQUEST` (409).

```sql
SELECT visit_request_id, status, visitor_user_id, registrant_email, email_verified_at
FROM visit_requests ORDER BY visit_request_id DESC LIMIT 5;          -- expect PENDING_APPROVAL, email_verified_at + visitor_user_id NOT NULL

SELECT visit_instance_id, status, current_host_user_id, host_assigned_by, host_assignment_source
FROM visit_request_campuses WHERE visit_request_id = <ID>;            -- expect WAITING_REQUEST_APPROVAL, all host cols NULL
```

## Remaining TODO / Risks
- **Idempotency key:** not sent by the frontend. Double-submits are covered by (a) single-use OTP and (b) the 10-minute duplicate guard. A true `idempotencyKey` would need a frontend + behaviour change — recommended but out of this scope.
- **Duplicate guard scope:** matches email+delegation+scope within 10 min (not the full campus-set + first planned_start_at). Sufficient for accidental resubmits; documented simplification.
- **Past-date check** uses a 1-day grace to absorb client/server timezone skew (frontend already enforces a 72h advance window), so it only rejects clearly-past dates.
- ~~**Dead scaffold:** `DelegationsController.submitvisitrequest` → `SubmitVisitRequestCommand`/Handler throw `NotImplementedException`.~~ **DONE (2026-06-20)** — route + command/handler/validator/response + tests removed (see changelog). UC-17 stays `initiate`/`verify`/`resend-otp`.
- ~~**Empty behaviour stubs:** `Common/Behaviours/TransactionBehaviour.cs` / `IdempotencyBehaviour.cs` (empty `PEMS.Shared` classes).~~ **DONE (2026-06-20)** — removed. The UC-17 transaction remains explicit inside `VerifyAndCreateVisitRequestCommandHandler`.
- **OTP rate limiting:** per-email hourly cap exists; per-IP limit / 60s server-side resend cooldown (currently client-side only) are recommended hardening, not yet added.
