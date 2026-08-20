# PEMS — Master Remediation Plan: Field Contract, Validation, History, and Pending-Edit Integrity

**Repository:** `quangthoai04/PEMS`  
**Branch reviewed:** `Dev`  
**HEAD verified:** `2c1d55cf2b64ffca8d13936a9fe82a00da5e92c7` (`fix view history changes`)  
**Plan date:** 2026-08-20  
**Primary scope:** Visit Request V2 + shared validation contracts + partner-link resolver + history integrity + authorization side-effects  
**Execution rule:** Fix in phases. Do not skip merge gates. Do not perform unrelated refactors.

---

# 1. Objective

The current system has multiple write paths for the same business fields, but not all paths use the same validation, normalization, history, and relationship rules.

The target is:

```text
ONE BUSINESS FIELD
    ↓
ONE CANONICAL CONTRACT
    ↓
all create/edit/safe-edit/contact/partner/OCR/API paths obey it
    ↓
frontend provides UX validation
backend remains authoritative
database is not used as a user-facing validator
history preserves exact before/after facts
multi-campus data never cross-wires
```

The remediation must solve the confirmed runtime bugs while preventing the same class of drift from reappearing elsewhere.

---

# 2. Confirmed issues

## ISSUE A — Critical 500 on pending edit

### Runtime evidence

Observed endpoint:

```http
PUT /api/v2/visit-requests/47003/pending-edit
```

Response:

```json
{
  "success": false,
  "errorCode": "INTERNAL_SERVER_ERROR",
  "message": "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.",
  "error": "An item with the same key has already been added. Key: 47203"
}
```

Relevant stack:

```text
GuestPartnerLinkResolver.ResolveForRequestAsync
→ VisitRequestV2EditService.ResolvePartnerLinksAsync
→ VisitRequestV2EditService.ApplyPendingEditAsync
→ UpdatePendingVisitRequestV2CommandHandler.Handle
```

### Confirmed code fault

Current resolver contains:

```csharp
var orgByGuestId = members.ToDictionary(
    m => m.GuestMemberId,
    m => m.Organization);
```

The `members` sequence is produced from:

```text
VisitInstanceGuestMembers
JOIN VisitGuestMembers
```

and contains both:

```text
VisitInstanceId
GuestMemberId
```

A single guest-member row may be linked by more than one visit instance/campus. Therefore `GuestMemberId` is not guaranteed to be unique in this query result.

`ToDictionary(GuestMemberId)` throws when the same member is linked to multiple instances.

### Severity

**CRITICAL.**

This blocks Full Edit and converts a legitimate data shape into HTTP 500.

---

## ISSUE B — Request history revision 1 → 2 can show “Không có dữ liệu lịch sử”

### Symptom

History detail can show:

```text
Phiên bản: 1 → 2

Họ tên người đăng ký:
Không có dữ liệu lịch sử → value

Quốc tịch:
Không có dữ liệu lịch sử → value
```

### Existing reader behavior

The reader correctly treats:

```text
missing predecessor field
```

as:

```text
BeforeUnknown = true
```

rather than inventing an empty old value.

That behavior must be preserved.

### Confirmed writer inconsistency

A canonical builder exists:

```text
VisitFormRevisionSnapshotBuilder.Request(request)
```

and includes:

```text
RegistrantFullName
RegistrantOrganization
RegistrantJobTitle
RegistrantNationality
RegistrantPhone
RegistrantEmail
```

However the CREATE request-revision path still has a hand-written serialization path that does not fully match the canonical builder.

This violates the intended invariant that all request revision snapshots share one shape.

### Additional gap

Safe Edit stores immutable `AuditLogChange` old/new values for changed fields, but request revision detail currently relies primarily on snapshot-to-snapshot diff.

Therefore, when a legacy predecessor snapshot is partial but the exact immutable old value exists in the correlated audit, the UI may still show:

```text
Không có dữ liệu lịch sử
```

even though the system has reliable evidence.

---

## ISSUE C — Safe Edit accepts invalid phone values

Confirmed behavior:

```text
+821012340001123213sd
```

can pass through Safe Edit.

### Root cause

Frontend Safe Edit uses `PhoneField`, but `PhoneField` is an input/hint component, not a validator.

Backend Safe Edit currently has paths where phone is only bounded by length instead of using the shared phone rule.

The service can then clean/store the string instead of rejecting it as invalid.

---

## ISSUE D — Operational Contact phone validation is also inconsistent

Several Operational Contact mutation validators use only length checks.

Some persistence code uses a normalization helper whose fallback behavior preserves the original input when it cannot normalize.

If invalid input is not rejected before that point, invalid text may be persisted.

The Manage Contact frontend also has dedicated field validation for several fields but does not consistently apply the canonical shared phone validator.

---

## ISSUE E — Phone contract adoption is incomplete system-wide

The backend already contains a canonical shared rule:

```text
PhoneNumber.IsValid(...)
MustBeAPhoneNumber(...)
```

but not all user-write validators use it.

Known areas requiring audit include at minimum:

```text
Visit Safe Edit
Operational Contact management
Partner Contact create/update
Business Card OCR confirmation
legacy registrant/update paths
other DTOs/commands accepting phone
```

This is a system-wide contract-adoption problem, not just one broken screen.

---

## ISSUE F — Nationality is not enforced as a real country

Current backend validation is effectively:

```text
required/non-empty
max length
```

rather than a canonical country validation.

Frontend also differs by screen:

```text
some screens → CountrySelect
some screens → plain input
```

`CountrySelect` is currently creatable/free-solo, meaning arbitrary user-entered values can be accepted.

If the business requirement is “nationality must be a valid country”, current implementation does not enforce it.

---

## ISSUE G — Email validation uses multiple independent implementations

Email is better protected than phone, but rules are still distributed across:

```text
Zod email validation
FluentValidation EmailAddress()
custom frontend regex
identity/business eligibility validators
```

The risk is future drift between screens and APIs.

This phase is primarily standardization and regression protection, unless further concrete corruption is discovered.

---

## ISSUE H — `/api/campuses/filter-options` returns 403 on the Full Edit screen

Observed independently of the pending-edit 500:

```http
GET /api/campuses/filter-options
→ 403 Forbidden
```

This is not the cause of the `GuestPartnerLinkResolver` crash.

It is a separate authorization/API-contract issue and must be investigated independently.

---

# 3. Non-goals

Do **not** turn this remediation into a broad rewrite.

Do not:

```text
rewrite Visit Request architecture
rewrite history architecture
convert every audit log into user-facing history
change FormRevision semantics
change contact ownership semantics
change approval semantics
change campus lifecycle rules
silently “repair” legacy data by guessing
replace business errors with generic success
hide real DB defects behind GroupBy().First()
```

---

# 4. Global invariants

These must remain true after every phase.

## 4.1 Backend is authoritative

Frontend validation is UX.

A direct API request with invalid data must still fail correctly.

## 4.2 Invalid data must fail before persistence

Expected invalid user input should produce:

```text
400 validation
or
422 business rule
or
409 conflict
```

depending on existing API conventions.

It should not normally reach MySQL and become an unhandled persistence exception.

## 4.3 Unknown history must remain unknown

Never convert:

```text
unknown old value
```

into:

```text
blank old value
```

without evidence.

Preferred rule:

```text
reliable immutable evidence exists → recover
otherwise → keep unknown
```

## 4.4 Multi-campus isolation

A request may contain multiple campuses.

Fixes must never:

```text
attach Campus A's guest to Campus B's partner relationship
copy Campus A's history into Campus B
dedupe data by throwing away VisitInstanceId
```

## 4.5 One canonical field contract

Where practical:

```text
phone
email
nationality
```

should each have one backend definition and one frontend mirror.

---

# 5. Phase 0 — Freeze evidence and map every affected path

Before modifying production code:

1. Verify current HEAD.
2. Record current full test totals.
3. Reproduce the `47003` pending-edit crash in an integration fixture.
4. Repo-wide search all writers/readers for:
   - `GuestPartnerLinkResolver.ResolveForRequestAsync`
   - `VisitGuestPartnerLinks`
   - `VisitInstanceGuestMembers`
   - `ToDictionary`
   - `RegistrantPhone`
   - `OperationalContactPhone`
   - `PhoneNumber`
   - `MustBeAPhoneNumber`
   - `MaximumLength(50)`
   - `Nationality`
   - `CountrySelect`
   - `EmailAddress`
   - custom email regex
   - `VisitRequestRevisionHistory`
   - `SnapshotJson`
   - `VisitFormRevisionSnapshotBuilder`
   - `/campuses/filter-options`
5. Produce a write-path inventory before coding.

### Required Phase-0 output

For each field:

```text
Field
Frontend create
Frontend full edit
Frontend safe edit
Frontend contact management
Backend create validator
Backend edit validator
Backend safe-edit validator
Backend contact validator
Normalization
Persistence
History source
```

---

# 6. Phase 1 — Fix the critical GuestPartnerLinkResolver 500

## 6.1 First prove the data model

Before changing `ToDictionary`, answer from schema/domain/tests:

```text
Can one GuestMemberId legally be linked to multiple VisitInstanceIds?
```

Then determine the real partner-link identity.

Possible models:

### Model A — relationship is member-global

```text
GuestMemberId → Partner
```

In this case duplicated instance links are legitimate query duplication and the resolver must collapse only when all member-global facts are identical.

### Model B — relationship is instance-specific

```text
(VisitInstanceId, GuestMemberId) → Partner
```

In this case all lookup/dedupe state must preserve the composite identity.

Do not decide by convenience.

Prove it from:

```text
entity relationships
DB unique constraints
partner-link schema
minutes integration
existing integration tests
business documentation/comments
```

## 6.2 Forbidden quick fix

Do not simply write:

```csharp
members
    .GroupBy(x => x.GuestMemberId)
    .Select(g => g.First())
```

unless the implementation first proves:

```text
all rows of one GuestMemberId are semantically equivalent
```

and has a test that rejects/handles inconsistent duplicates.

`First()` may stop the exception while silently cross-wiring campuses.

## 6.3 Required resolver behavior

The final resolver must:

```text
accept legitimate shared-member shapes
remain idempotent
not create duplicate partner links
not overwrite existing confirmed decisions
not propagate ambiguous organization→partner decisions
not lose VisitInstanceId context when it matters
remove true orphan links exactly as before
```

## 6.4 Add explicit ambiguity handling

If the same logical member resolves to conflicting organization/partner facts:

```text
do not arbitrarily pick one
```

Depending on proven domain semantics:

```text
skip propagation
or
throw a typed data-integrity/business exception
```

but do not crash with raw `ArgumentException`.

## 6.5 Tests for Phase 1

### GP-1 — exact reproduction

A request has:

```text
GuestMemberId = X
linked to Campus A
linked to Campus B
```

Run the real pending-edit handler.

Assert:

```text
no 500
transaction commits correctly
```

### GP-2 — no cross-campus link

Assert created/retained partner links belong to the correct intended instance(s).

### GP-3 — idempotency

Run resolver twice.

Assert second run:

```text
0 new links
0 duplicate links
```

### GP-4 — conflicting partner decisions

Same normalized organization has two confirmed partner decisions.

Assert:

```text
no automatic propagation to a third member
```

### GP-5 — orphan cleanup

Existing expected orphan behavior remains.

### GP-6 — pending full edit regression

Real `UpdatePendingVisitRequestV2CommandHandler`.

### GP-7 — create flow regression

Real create path that invokes partner resolution.

### GP-8 — multi-campus shared member with explicit partner selection

Ensure correct propagation semantics according to the proven domain model.

## 6.6 Merge gate Phase 1

```text
[ ] Runtime duplicate-key class reproduced by test
[ ] Root data-model semantics documented
[ ] No GroupBy().First() blind patch
[ ] Shared member no longer crashes
[ ] No cross-campus link
[ ] Resolver remains idempotent
[ ] Create path remains green
[ ] Pending edit remains green
[ ] Full VisitRequests suite green
```

Do not proceed until Phase 1 is green.

---

# 7. Phase 2 — Repair request revision snapshot consistency

## 7.1 Enforce canonical snapshot writer

Every request-level revision writer must serialize through:

```text
VisitFormRevisionSnapshotBuilder.Request(...)
```

Search and eliminate hand-written equivalent anonymous-object serializers.

At minimum inspect:

```text
VisitRequestV2CreateService
VisitRequestV2EditService
VisitSafeEditService
resubmit flows
migration/baseline helpers
tests/fixtures
```

## 7.2 CREATE revision must include the complete canonical registrant snapshot

Revision 1 should contain exactly the canonical request snapshot fields required by current History:

```text
RegistrantFullName
RegistrantOrganization
RegistrantJobTitle
RegistrantNationality
RegistrantPhone
RegistrantEmail
```

Do not add unrelated fields merely for convenience.

## 7.3 Do not rewrite historical revision rows

Runtime fix is forward-only.

Do not mutate old immutable snapshots in normal request flow.

Legacy repair belongs to an explicit backfill/recovery mechanism if deterministic.

## 7.4 Safe recovery for partial predecessor snapshots

Improve request revision detail with the following precedence:

```text
A. previous snapshot contains field
   → use previous snapshot value

B. previous snapshot does not contain field
   AND current revision has a uniquely-correlated immutable audit change
   → use AuditLogChange.OldValueText

C. no reliable immutable evidence
   → BeforeUnknown remains true
```

### Correlation must be deterministic

Use existing correlation fields/reason only if they uniquely identify the audit belonging to that revision.

Do not correlate using:

```text
nearest timestamp
same actor only
latest audit
message text
```

## 7.5 Current revision must remain authoritative for “after”

Recovery is only for the unknown predecessor value.

Do not replace canonical revision values with mutable current request data.

## 7.6 Tests Phase 2

### HR-1 — create then first safe edit

Create request using real handler.

Safe edit:

```text
name A → B
phone A → B
nationality A → B
```

History `1 → 2` must show exact old/new.

### HR-2 — CREATE snapshot contains nationality

Direct DB assertion.

### HR-3 — no manual request snapshot writer remains

Architecture/source guard where practical.

### HR-4 — legacy partial snapshot + uniquely correlated audit

Old snapshot missing phone.

Audit has:

```text
OldValueText = old phone
NewValueText = new phone
```

Assert History recovers old phone.

### HR-5 — legacy partial snapshot without evidence

Assert:

```text
BeforeUnknown = true
```

### HR-6 — ambiguous audit correlation

Assert no recovery.

### HR-7 — no fake blank

Missing key must never be displayed as a proven empty string.

## 7.7 Merge gate Phase 2

```text
[ ] All new request revisions use canonical builder
[ ] Revision 1 includes nationality
[ ] Create→Edit first diff exact
[ ] Legacy unknown remains unknown without evidence
[ ] Correlated immutable audit can recover exact before
[ ] No historical snapshot rewritten by runtime path
[ ] Existing history permission rules unchanged
```

---

# 8. Phase 3 — System-wide phone contract

## 8.1 Canonical backend rule

The backend rule should be the authority:

```text
PhoneNumber.IsValid
MustBeAPhoneNumber
```

Required/optional semantics remain field-specific.

Example:

```text
optional phone:
blank → allowed
nonblank invalid → rejected

required phone:
blank → rejected
nonblank invalid → rejected
```

## 8.2 Canonical persistence rule

After successful validation:

```text
store normalized representation
```

preferably E.164 where current system already expects it.

Do not use:

```text
NormalizeOrOriginal
```

as a substitute for validation on user-write paths.

If a legacy replay/read path intentionally preserves historical raw input, document that separately.

## 8.3 Audit every phone write path

At minimum:

```text
Create Visit V2
Pending Edit V2
Resubmit V2
Safe Edit
Operational Contact profile update
Operational Contact replace
Operational Contact transfer
Partner Contact create
Partner Contact update
Business Card OCR confirmation
legacy registrant edit
other commands accepting user phone input
```

For each path, classify:

```text
USER WRITE
LEGACY REPLAY
READ ONLY
SYSTEM IMPORT
```

Strict phone validation applies to user writes.

Do not apply new strict validation blindly to read-only replay payloads.

## 8.4 Frontend phone validation

Create a single shared frontend helper/schema around:

```text
isValidPhone(...)
```

Reuse it in:

```text
Create
Full Edit
Safe Edit
Manage Contact
Partner screens
OCR confirmation UI
```

`PhoneField` may remain the shared visual component, but validation must not be assumed to happen inside it.

## 8.5 Safe Edit fix

Safe Edit must:

```text
validate locally before request
show field-level phone error
backend reject direct invalid API call
normalize valid phone consistently
```

Test the exact previously accepted bad value:

```text
+821012340001123213sd
```

Expected:

```text
frontend refuses
backend direct request returns validation error
database unchanged
history unchanged
```

## 8.6 Operational Contact fix

Update:

```text
profile update
replace
transfer
save/combined endpoint
```

with the same backend phone rule where phone is a user-write field.

Maintain existing optionality.

Do not change identity semantics.

## 8.7 Partner and OCR paths

Bring these paths to the same backend phone contract.

Do not change:

```text
partner matching
OCR extraction
contact ownership
approval workflow
```

Only validation/normalization unless a directly related bug is proven.

## 8.8 Architecture guard

Add a test/static guard where practical so a new user-write validator cannot regress to:

```text
MaximumLength(50)
```

without also applying the canonical phone rule.

A behavior-based validator test is preferred.

## 8.9 Phone test matrix

For every important write path test:

```text
blank optional
valid VN national
valid +84
valid non-VN international
letters
extension
too short
impossible number
spaces/punctuation according to canonical parser behavior
over max length
```

Also assert exact DB normalization.

---

# 9. Phase 4 — Canonical nationality contract

## 9.1 First decide the business representation

Do not code before deciding one canonical stored representation.

Preferred choices:

```text
ISO 3166-1 alpha-2
```

or, if schema compatibility requires names:

```text
canonical English country name
```

Document why.

## 9.2 Backend authority

Create a shared backend nationality validator/normalizer.

It must reject arbitrary strings such as:

```text
abcxyzcountry
FPTU123
```

unless business requirements explicitly allow free text.

## 9.3 Frontend control

If nationality is a strict country:

```text
CountrySelect must not remain free-creatable for Visit nationality writes
```

Possible implementation:

```text
non-creatable select/search
```

or:

```text
creatable disabled in strict-country mode
```

Do not break other modules that intentionally use free text.

## 9.4 Full Edit consistency

Replace the plain nationality input in Full Edit with the same canonical country control/contract used by Create/Safe Edit, while preserving current stored value.

## 9.5 Legacy compatibility

Existing legacy values may not match the new canonical vocabulary.

Required behavior:

```text
existing legacy value can be displayed/replayed
unrelated edit must not become impossible
newly changed nationality must satisfy canonical contract
```

Do not force users to repair unrelated legacy data in a read-only/unrelated edit flow.

## 9.6 Tests nationality

```text
valid country
invalid arbitrary text
create
full edit
safe edit
direct API bypass
legacy display
legacy unrelated edit
canonical normalization
history before/after
```

---

# 10. Phase 5 — Email rule consolidation

## 10.1 Inventory all email validators

Classify:

```text
format validation
identity eligibility
account existence/status
visitor-vs-internal account restriction
immutable email rules
```

Do not conflate syntax with business identity policy.

## 10.2 Shared frontend syntax rule

Replace isolated regex implementations where practical with one shared email validation helper/schema.

Backend remains final authority.

## 10.3 Preserve identity-specific errors

Operational Contact may return specific errors such as:

```text
account inactive
email cannot be used for visitor account
identity change not allowed
```

Do not collapse these into “Email không hợp lệ”.

## 10.4 Tests

```text
bad syntax
valid syntax
internal forbidden account
inactive account
immutable registrant email
contact identity change
direct API bypass
```

---

# 11. Phase 6 — Investigate `/campuses/filter-options` 403

This phase must remain separate from the 500 fix.

## 11.1 Trace request ownership

Determine:

```text
which component calls /api/campuses/filter-options
which roles load EditVisitRequestV2Page
which roles are authorized by controller/policy
whether the endpoint is actually required on existing-campus edit
```

## 11.2 Possible outcomes

### Outcome A — frontend should not call endpoint

If existing request campus set is immutable and the page does not need selectable campus options:

```text
stop unnecessary request
```

### Outcome B — caller should be authorized

If the page legitimately requires the endpoint:

```text
fix authorization policy
```

with least privilege.

### Outcome C — wrong endpoint for role

Use the correct scoped/read endpoint instead.

Do not simply remove authorization or broaden permissions globally.

## 11.3 Tests

```text
Visitor edit page
Staff Leader edit page
HO edit page
unrelated role
no repeated 403 spam
campus set still immutable
no hidden-campus disclosure
```

---

# 12. Phase 7 — Error taxonomy and 500 hardening

## 12.1 Expected errors should be typed

Review mutation paths for expected failures currently capable of escaping as:

```text
ArgumentException
InvalidOperationException
DbUpdateException
```

If the failure represents a predictable user/business/data-integrity condition, map it to the project’s existing typed exception model.

## 12.2 Do not over-catch

Do **not** convert all `DbUpdateException` into validation.

Real infrastructure/data corruption must remain 500 and be logged.

Only deterministic known constraint/business cases should be mapped.

## 12.3 Logging

For real 500s preserve:

```text
traceId
structured server log
exception type
request id / visit request id where safe
```

Production response must remain safe.

Development diagnostics can retain stack trace according to current middleware behavior.

---

# 13. Cross-phase History requirements

Any field fix that changes a persisted business field must preserve correct history.

For:

```text
registrant name
organization
job title
nationality
phone
```

assert:

```text
before value
after value
actor
timestamp
revision number
```

are correct.

Do not create extra FormRevision for request-level Safe Edit.

Do not create duplicate AuditLog and Revision timeline events unless current design intentionally surfaces both as different business meanings.

---

# 14. Cross-phase security requirements

All current history visibility guarantees must remain.

Test at least:

```text
Registrant
Current Operational Contact
HO
Staff Leader campus A
Host campus A
unrelated user
```

and multi-campus:

```text
Campus A viewer cannot retrieve Campus B hidden history EventId
```

Expected anti-discovery behavior remains:

```text
NotFound
```

where current API uses that convention.

---

# 15. Regression groups that must remain green

The previous History work must not regress.

At minimum rerun:

```text
Commit 1 — Revision Integrity
Commit 2 — Decision History Integrity
Commit 3 — Contact History Integrity
Commit 4 — Lifecycle History Integrity
Commit 5 — legacy/history backfill tests if present on current branch
```

Plus:

```text
UpdatePendingVisitRequestV2ServiceTests
UpdatePendingVisitInstanceV2ServiceTests
VisitSafeEditV2Tests
ResubmitRejectedVisitRequestV2ServiceTests
CampusApprovalDecisionV2Tests
OperationalContactManagementTests
CompleteVisitStageV2Tests
VisitRequestHistoryV2Tests
Partner-link related tests
```

---

# 16. Mandatory end-to-end scenarios

## E2E-1 — Pending edit with shared member

```text
multi-campus request
same GuestMemberId linked by two instances
edit one request
save succeeds
partner links remain correct
no cross-campus corruption
```

## E2E-2 — Create → Safe Edit → History

```text
create request with valid name/nationality/phone
safe edit all three
history 1 → 2 shows exact old/new
```

## E2E-3 — invalid Safe Edit phone

```text
bad phone
frontend field error
direct backend call rejected
DB unchanged
history unchanged
```

## E2E-4 — Manage Contact invalid phone

```text
bad phone
rejected
contact identity unchanged
invitation state unchanged
history unchanged
```

## E2E-5 — valid international phone

```text
valid international number
accepted
normalized consistently
reload shows correct representation
```

## E2E-6 — nationality invalid

```text
arbitrary text
frontend refuses
direct API refuses
DB unchanged
```

## E2E-7 — nationality legacy

```text
old noncanonical nationality exists
user edits an unrelated field
unrelated edit remains possible
```

## E2E-8 — campus options authorization

```text
open Full Edit as actual allowed roles
no unnecessary 403
no hidden campus data
```

---

# 17. Database/data-integrity investigation for the reported records

## 17.1 Request 47003

Before/after resolver fix, inspect:

```sql
SELECT
    visit_request_id,
    visit_instance_id,
    guest_member_id,
    display_order
FROM visit_instance_guest_members
WHERE visit_request_id = 47003
ORDER BY guest_member_id, visit_instance_id;
```

Inspect member:

```sql
SELECT
    guest_member_id,
    visit_request_id,
    full_name,
    organization,
    organization_partner_id
FROM visit_guest_members
WHERE guest_member_id = 47203;
```

Inspect partner links:

```sql
SELECT
    visit_guest_partner_link_id,
    visit_request_id,
    visit_instance_id,
    guest_member_id,
    minute_participant_id,
    partner_id,
    match_status,
    match_source
FROM visit_guest_partner_links
WHERE visit_request_id = 47003
ORDER BY guest_member_id, visit_instance_id;
```

The agent must use actual schema column names if they differ.

Do not modify production data manually just to make the failing request pass.

## 17.2 Request 41012 history

Inspect:

```sql
SELECT
    request_revision_history_id,
    visit_request_id,
    request_revision,
    source_type,
    reason,
    snapshot_json,
    applied_by,
    applied_at
FROM visit_request_revision_histories
WHERE visit_request_id = 41012
ORDER BY request_revision;
```

Then inspect Safe Edit audit rows correlated to the revision, using actual schema.

Goal:

```text
determine whether old values truly do not exist
or exist in immutable AuditLogChange but are currently ignored by History detail
```

Do not guess.

---

# 18. Recommended implementation order

Use separate patches.

```text
Patch 1
Critical GuestPartnerLinkResolver multi-campus fix

Patch 2
Request revision canonical snapshot + safe legacy before-value recovery

Patch 3
System-wide phone contract

Patch 4
Nationality contract

Patch 5
Email consolidation

Patch 6
Campus filter-options authorization/request cleanup

Patch 7
Error taxonomy + architecture guards + final regression
```

Do not combine all fixes into one giant unreviewable diff.

---

# 19. Required report after each patch

For every patch report:

```text
1. Files changed
2. Root cause
3. Exact invariant fixed
4. Before behavior
5. After behavior
6. Data-model assumptions verified
7. Tests added
8. Focused test results
9. Full regression results
10. Any new edge cases discovered
11. Anything intentionally not fixed
12. Confirm no unrelated refactor
```

Stop after each patch if a newly discovered issue changes the assumed data model.

---

# 20. Final merge gate

Do not call the remediation complete until all are true:

```text
[ ] Request 47003 class no longer crashes
[ ] Shared GuestMember multi-campus semantics are explicitly tested
[ ] Partner links do not cross-wire campuses
[ ] Resolver remains idempotent

[ ] Request CREATE revisions use canonical snapshot builder
[ ] First edit has correct 1 → 2 before/after
[ ] Legacy partial history recovers only from deterministic immutable evidence
[ ] Unknown remains unknown when evidence is absent

[ ] Safe Edit invalid phone rejected
[ ] Manage Contact invalid phone rejected
[ ] Partner/OCR/legacy user-write phone paths audited and fixed
[ ] All valid phone writes normalize consistently
[ ] Direct API bypass cannot store invalid phone

[ ] Nationality contract documented
[ ] Backend rejects invalid nationality if strict-country requirement is confirmed
[ ] Create/Edit/Safe Edit use compatible country contract
[ ] Legacy values do not block unrelated edits

[ ] Email syntax validation no longer drifts unnecessarily
[ ] Identity/business email errors remain specific

[ ] /campuses/filter-options 403 root cause resolved
[ ] No authorization broadening leaks campus data

[ ] Expected user/business failures do not surface as accidental 500s
[ ] Real unexpected failures remain logged and 500

[ ] Existing revision/decision/contact/lifecycle history tests green
[ ] Multi-campus permission tests green
[ ] Full VisitRequests suite green
[ ] Architecture tests green
[ ] Full solution build green
```

---

# 21. Final agent instruction

Work from current source, not from this plan alone.

If actual code proves any assumption here wrong:

```text
stop
document the contradiction
show the code/schema/test evidence
revise the implementation approach
```

Do not “make the code fit the plan”.

The governing principle is:

```text
Same business field → same contract.
Same historical fact → immutable, evidence-backed history.
Same shared member → relationship semantics must preserve campus context.
Expected invalid input → typed rejection, never accidental 500.
Unknown data → remain unknown rather than fabricated.
```

After all phases are complete, provide a final system-wide matrix:

```text
Field / Mutation
Frontend validator
Backend validator
Normalizer
Persistence target
History source
Allowed roles
Legacy behavior
Tests
```

for at least:

```text
Registrant full name
Registrant organization
Registrant job title
Registrant nationality
Registrant phone
Registrant email
Operational Contact phone/email
Guest/support nationality
Partner Contact phone/email
OCR Contact phone/email
```
