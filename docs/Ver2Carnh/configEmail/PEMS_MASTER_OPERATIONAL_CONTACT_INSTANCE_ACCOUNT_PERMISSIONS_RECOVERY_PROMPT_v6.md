# PEMS — Master Operational Contact & Visit Instance Ownership Closure Prompt v6
## Contact Management, Account Provisioning, Instance-Scoped Visitor Rights, Profile Sync, Email Recovery, Reject Events, Transfer, and 72H Boundaries

### Target
- Repository: PEMS
- Working branch: `Cảnh-Iter1`
- Continue from the CURRENT working tree.
- Current `Cảnh-Iter1` / `Dev` baseline may be treated as equivalent for this task, but implement on `Cảnh-Iter1`.
- Preserve ALL existing modified/untracked files and all previous fixes.
- Do NOT reset, discard, overwrite, checkout-over, drop stashes, or rewrite completed work from scratch.

---

# 0. Non-negotiable working rule — DO NOT GUESS

This task contains product/business decisions that must not be invented.

For every point where:
- current code has two plausible behaviors;
- existing documentation conflicts;
- the requested behavior is not precise enough to determine authorization/state transitions;
- implementing one option would change business semantics;
- a new schema/table/column appears necessary;
- a role/status eligibility rule is unclear;
- an instance-scoped action can affect request-level or sibling-campus data;
- an email retry outcome is not provably safe;
- profile fields owned by the account are unclear;

you MUST:

```text
STOP that subtask
→ show exact code/database evidence
→ explain the competing options
→ ask the user for the decision
→ do NOT implement that decision until answered
```

You may continue other independent, unblocked work.

Do not “choose the most reasonable behavior”.
Do not silently infer from naming.
Do not turn an ambiguity into a new business rule.

---

# 1. Confirmed product model

The following distinctions are CONFIRMED and must be implemented consistently:

```text
VISIT REQUEST OWNERSHIP
≠
VISIT INSTANCE OPERATIONAL-CONTACT OWNERSHIP
≠
ACCOUNT PROFILE
≠
PER-INSTANCE OPERATIONAL-CONTACT SNAPSHOT
```

## 1.1 Registrant

The registrant owns the overall request according to the existing Visitor workflow.

## 1.2 Operational Contact

After a person confirms the operational-contact role and is linked/provisioned as a PEMS account, they are NOT merely “an email confirmation recipient”.

They become an authenticated actor for the specific Visit Instance(s) where:

```text
visit_request_campuses.operational_contact_user_id
==
currentUser.UserId
```

Their authority is INSTANCE-SCOPED.

## 1.3 Account profile

The account represents the actual PEMS user identity.

Primary identity key:

```text
normalized verified email
```

## 1.4 Operational-contact snapshot

The snapshot represents that person's contextual contact information for ONE request/campus/instance.

Different instances may legitimately store different:
- displayed name;
- organization;
- job title;
- phone;

while still linking to the SAME account/user by verified email/UserId.

---

# 2. Preflight

Before coding:

1. Confirm branch = `Cảnh-Iter1`.
2. Record:
   - HEAD SHA;
   - `git status`;
   - modified files;
   - untracked files.
3. Preserve prior WIP.
4. Do not touch existing stashes.
5. Audit the real working tree, not only committed HEAD.

Locate and report exact current implementations of at least:

```text
EditVisitRequestV2Page
CampusVisitCard
VisitRequestV2DetailView
CampusVisitDetailCard
visitV2Actions / VisitFormActions
contact management modal/page
SaveOperationalContactCommand
UpdateOperationalContactProfileCommand
ReplaceOperationalContactCommandHandler
InitiateOperationalContactTransferCommandHandler
AcceptOperationalContactConfirmationCommandHandler
Decline/Cancel/Resend contact handlers
OperationalContactInvitationService
OperationalContactMaintenanceService
OperationalContactGuards
VisitRequestV2EditService
Resubmit service/handler
VisitRequestV2CreateService
VisitMutationPolicy
RejectCampusInstanceCommandHandler
RecoverableVisitEmailSender
IVisitNotificationRecoveryService
recovery hosted service
SystemEmailDispatcher
email_send_idempotency / durable email states
account provisioning / SSO provisioning service
Visitor authorization / request-history / feedback / amendment handlers
```

---

# 3. Preserve all completed fixes

Do not regress:

```text
- Dynamic campus count/limit
- Create and Edit use same canonical ACTIVE-campus ceiling
- Controlled campus select
- Correct campusId state/payload
- Exactly one Edit success toast
- Exactly one Resubmit success toast
- Create registration lead time = 72h
- PRE-APPROVAL Edit submission lead time = 72h
- Resubmit-after-Reject lead time = 72h
- Approved Amendment is NOT subject to registration 72h
- Passive passage below 72h does NOT auto-expire/reject/email
- Reject email sent to registrant with correct per-campus semantics
- Contact invitation expiry notifies registrant
- Existing contact removed from Visit Request Edit
- Detail View owns contact management
- Same-email metadata update creates no identity change/token/mail
- Changed-email path uses INITIAL_CONFIRMATION / TRANSFER
- TRANSFER keeps current contact until acceptance
- contactFullName comes from actual pending person's name, not email
- Edit/Resubmit backend cannot mutate existing operational-contact profile
```

---

# 4. Visit Edit vs Contact Management

## 4.1 Existing campus in Visit Request Edit

For:

```text
instanceId != null
```

Visit Request Edit must NOT edit:
- operational contact full name;
- organization;
- job title;
- phone;
- email.

Do not show contact-change action inside Visit Request Edit.

A compact read-only summary may remain if useful.

## 4.2 Newly added campus during Edit

A newly added campus has no existing operational-contact relation yet.

It MAY require initial contact information inside the edit/add-campus flow.

Do not accidentally apply the “existing contact read-only” rule to a truly new campus.

## 4.3 Backend defense

Frontend separation is insufficient.

PRE-APPROVAL Edit and Resubmit endpoints must refuse mutation of existing contact fields.

Keep:
- `IMMUTABLE_CONTACT_IDENTITY`;
- `IMMUTABLE_CONTACT_PROFILE` or equivalent stable business error.

Do not silently accept contact metadata mutation through Visit Edit.

---

# 5. Detail View becomes the home of Operational Contact management

Each campus/instance detail should expose its own operational-contact card.

The card should show, where applicable:

```text
Họ và tên
Đơn vị công tác
Chức vụ
Số điện thoại
Email
Trạng thái xác nhận
Nguồn xác nhận
Xác nhận lúc
Pending identity-change state
```

Authorized users get:

```text
Chỉnh sửa đầu mối
```

or the existing equivalent wording.

The action must operate on:

```text
visitRequestId + visitInstanceId
```

Never route contact management back into the whole-request Edit page.

Multi-campus isolation is mandatory.

---

# 6. Tooltip / explanatory copy

Remove the long persistent sentence:

> Thông tin đầu mối vận hành của cơ sở đã có được quản lý ở màn hình chi tiết đơn (mục "Quản lý đầu mối"), không sửa trong biểu mẫu đăng ký.

Place guidance in the `?` tooltip instead.

Recommended concise VI copy:

> **Đầu mối dùng để phối hợp tại cơ sở này, không nhất thiết là tài khoản đang đăng nhập. Với đầu mối đã có, hãy chỉnh sửa tại Chi tiết đơn → Quản lý đầu mối. Email mới cần được chính đầu mối xác nhận trước khi được liên kết hoặc tạo tài khoản.**

Provide equivalent EN translation.

Do not duplicate the same long guidance as permanent text and tooltip.

---

# 7. Operational Contact edit — two semantic branches only

Dedicated contact-management form fields:

```text
Full name
Organization
Job title
Phone
Email
```

Normalize and compare email on Save.

## 7.1 SAME normalized email

This is a metadata/snapshot update only.

Allowed:
- full name;
- organization;
- job title;
- phone.

Must NOT:
- create identity-change row;
- create token;
- send confirmation email;
- create transfer;
- change `OperationalContactUserId`;
- change confirmed account identity;
- clear confirmation;
- reset confirmation source;
- change campus/request status;
- reopen gate;
- bump Visit Form revision;
- create Amendment;
- invoke 72h registration validation;
- overwrite existing account profile automatically.

## 7.2 DIFFERENT normalized email

This is an identity change.

Only this path may:
- create INITIAL_CONFIRMATION / replacement flow;
- create TRANSFER;
- mint token;
- send contact confirmation email.

---

# 8. Account creation/provisioning — only AFTER proof of email ownership

Do NOT create a PEMS account merely because a registrant typed an email.

Required lifecycle:

```text
new operational-contact email entered
→ pending invitation
→ confirmation/SSO proves control of that email
→ lookup account by normalized verified email
→ reuse eligible existing account OR provision a new eligible account
→ bind UserId to the instance
```

If invitation:
- expires;
- is declined;
- is cancelled;

then no account should exist solely because that email was entered.

---

# 9. Existing account reuse

If an eligible account already exists for the confirmed normalized email:

```text
reuse existing UserId
```

Do NOT create a duplicate account.

Do NOT automatically overwrite the existing account profile using registrant-entered snapshot data.

The per-instance snapshot may still use different contextual:
- name;
- organization;
- title;
- phone.

---

# 10. New account provisioning

If NO eligible account exists after successful confirmation:

use the project's canonical account/SSO provisioning mechanism.

Expected external operational-contact role:

```text
VISITOR
```

ONLY if this matches the current canonical role model.

### MUST ASK RULE

If current code indicates that operational contacts may legitimately be:
- VISITOR;
- internal staff;
- multiple roles;
- role-independent;

and the repository does not already contain one authoritative eligibility rule:

STOP and ask the user.

Do NOT silently force an existing internal account to VISITOR.

Do NOT silently create a second VISITOR account for the same email.

---

# 11. New-account initial profile

For a genuinely new account, after confirmation, pending snapshot may seed fields that the account schema canonically owns.

Likely candidates include:
- email;
- full name;
- phone.

But DO NOT ASSUME the full field set.

### MUST ASK RULE

Audit account entity/profile schema first.

If it is unclear whether:
- organization;
- job title;
- nationality;
- phone;
- other fields;

belong to the global account profile or only the visit-instance snapshot:

show the schema/current usage and ask the user before mapping those fields into the account.

Do not invent profile ownership.

---

# 12. Optional self-service account profile synchronization — REQUIRED feature

This feature is now IN SCOPE.

When an EXISTING account holder confirms or opens an assigned contact instance and the current instance/contact snapshot differs from their own account profile, the system must NOT auto-overwrite their account.

Instead, only the authenticated account holder may be offered:

> **Thông tin liên hệ trong yêu cầu này khác hồ sơ PEMS của bạn. Bạn có muốn cập nhật hồ sơ cá nhân không?**

Actions:

```text
Giữ nguyên hồ sơ
Cập nhật hồ sơ cá nhân
```

Rules:

```text
- Registrant cannot choose this for another person.
- Staff cannot silently choose this for the contact.
- Only current authenticated account holder can update their own account profile.
- Declining profile sync does not affect the instance contact snapshot.
- Updating the account profile does not rewrite historical snapshots in previous requests/instances.
```

### MUST ASK RULE — profile-sync fields

Before implementing the copy operation:

audit which fields are owned by the account profile.

If the exact set is not already canonical, ask the user:

```text
Which instance-contact fields may be copied into the account profile?
```

Do not infer that organization/job title must be global profile fields.

---

# 13. Operational Contact becomes a real instance-scoped actor

After successful confirmation and account binding, the operational contact must be able to use the system for the instance(s) assigned to them.

Authorization identity:

```text
currentUser.UserId == instance.OperationalContactUserId
```

Role alone is NOT sufficient.

A random VISITOR must not gain access merely because they have role VISITOR.

---

# 14. Confirmed high-level permission intent

The operational contact should have visitor-like operational capabilities, but ONLY for the instance(s) assigned to them.

Confirmed intent includes at least:

```text
- view the assigned instance;
- see its relevant status/history;
- see rejection information for that instance;
- edit allowed information of that instance;
- submit/send back/resubmit allowed information for that instance;
- provide feedback/respond where the Visitor workflow supports it;
- perform other Visitor-like instance-local actions that are meaningful for that assigned instance;
- manage/transfer their operational-contact role if the existing policy allows it.
```

They must NOT automatically gain ownership over the whole multi-campus request.

---

# 15. Request-level vs Instance-level authorization boundary

Use this conceptual rule:

```text
REQUEST-LEVEL ACTION
→ remains registrant/request-owner scoped unless explicitly decided otherwise

INSTANCE-LOCAL ACTION
→ may allow:
   registrant
   OR current assigned OperationalContactUserId
```

Operational contact on HN must not automatically act on DN/HCM sibling instances.

Example:

```text
Request VR-2003

HN instance 3103 → OperationalContactUserId = 501
DN instance 3104 → OperationalContactUserId = 700
```

User 501:
- may act on HN according to allowed instance actions;
- must not gain DN mutation rights merely because both belong to VR-2003.

---

# 16. CRITICAL — action-by-action authorization audit

Do NOT globally copy “all registrant permissions” to operational contact.

Audit each Visitor action and classify it:

```text
A. INSTANCE_LOCAL
B. REQUEST_LEVEL
C. CROSS_INSTANCE
D. IDENTITY/OWNER_ONLY
E. UNCLEAR
```

At minimum audit:

```text
View detail
View history
View rejection reason
Edit before approval
Resubmit after rejection
Feedback / response
Amendment creation
Amendment view
Amendment cancel
Guest list changes
Support personnel changes
Schedule changes
Additional requirements
Upload/download instance files
Contact management
Cancel visit/request
Add campus
Remove campus
Registrant identity changes
Request-level notes/data
Any existing Visitor CTA/action in detail/history pages
```

### MUST ASK RULE

For every action classified `E. UNCLEAR`, or where an instance-local edit necessarily changes shared request-level state:

STOP and ask the user.

Do NOT guess.

---

# 17. Suggested default authorization shape — DO NOT IMPLEMENT IF CODE PROVES AMBIGUOUS

This section is a working classification to verify, not permission to invent.

Likely instance-local:
- view assigned instance;
- view instance status/history;
- view instance rejection;
- edit instance-local editable data;
- resubmit that instance if workflow truly supports per-instance resubmit;
- feedback/respond for that instance;
- instance-specific Amendment if canonical Amendment model is target-instance scoped;
- manage their own contact handover.

Likely request-owner-only:
- add/remove campus;
- change registrant identity;
- change request-wide ownership;
- mutate sibling campuses;
- cancel whole multi-campus request;
- request-level operations affecting all campuses.

If code/business docs do not prove these boundaries, ASK.

---

# 18. Resubmit and Edit by operational contact

The user has confirmed that operational contact should be able to "edit / gửi lại" for their assigned instance.

But exact behavior must follow current per-campus workflow.

### Required audit

Determine whether current backend Resubmit/Edit is:
- whole-request;
- target-campus only;
- mixed.

If a command currently resubmits the entire request or mutates sibling campus state, DO NOT simply authorize operational contact to call it.

Instead:

```text
show the exact current semantics
→ ask the user whether to:
   A. create/enable target-instance resubmit for contact;
   B. keep whole-request resubmit registrant-only;
   C. another desired behavior.
```

This decision MUST NOT be guessed.

---

# 19. Feedback / response rights

Audit every current Visitor feedback/response mechanism.

If feedback is attached to:
- one instance/campus;
- one rejection/decision;
- one instance-local workflow;

operational contact may be a candidate actor.

If feedback is request-wide or visible across campuses, ask before granting it.

Authorization must be enforced backend-side by `OperationalContactUserId`, not just button visibility.

---

# 20. Amendment rights

Registration 72h remains unrelated.

Operational contact may potentially create/view an Amendment for their assigned instance ONLY if the current Amendment model supports instance-targeted ownership.

### MUST ASK RULE

If Amendment creation:
- changes the whole request;
- permits cross-campus edits;
- has registrant-only ownership assumptions;
- or target scope is unclear;

STOP and ask before granting operational-contact Amendment permissions.

Do not infer.

---

# 21. Pending invitation metadata

If a pending invitation exists for the SAME pending email and metadata changes:

```text
do not mint a new token
do not resend automatically
do not extend expires_at
do not increment token_version
do not reset resend_count
```

Update pending snapshot only if supported.

Expired invitation stays expired.

Resend is explicit.

---

# 22. Pending TRANSFER — A current vs B pending

For:

```text
A = current confirmed contact
B = pending transfer target
```

keep identities visually and logically separate.

Preferred behavior already agreed for lowest risk:

```text
Current A
→ shown as current contact
→ may edit A's metadata if authorized

Pending B
→ separate pending-transfer block
→ Resend / Cancel
→ no ordinary current-contact edit action
```

Do not combine:

```text
UserId = A
Name/Email = B
```

in one edit model.

Do not create a second transfer while B is pending.

### MUST ASK RULE

If current implementation already exposes editing of B's pending metadata and removing it would change a relied-upon business flow, show evidence and ask before deleting it.

---

# 23. `contactFullName` must be actual name

Keep the fix:

```text
PendingSnapshotJson.fullName
→ legacy-compatible key
→ safe neutral fallback
```

Never:

```text
contactFullName = email
```

For TRANSFER, do not use outgoing A's name as incoming B's name.

---

# 24. Verified registrant self-match

Preserve existing proven self-match semantics if current code requires:

```text
newEmail == RegistrantEmail
AND RegistrantUserId != null
AND EmailVerifiedAt != null
```

No extra confirmation needed if that identity is already proven under canonical code.

If current code contains competing self-match rules, ask before changing.

---

# 25. Contact confirmation expiry

Keep invitation lifecycle independent from visit scheduling.

Example existing policy may be:

```text
INITIAL_CONFIRMATION = 72h
TRANSFER = 24h
```

These are invitation-expiry policies, not visit-registration lead time.

On expiry:
- invitation → EXPIRED;
- token invalid;
- no unintended account creation;
- pending target not bound;
- TRANSFER keeps current A;
- registrant/initiator receives the canonical expiry notification.

---

# 26. Reject notification recovery — exact business event

Do NOT identify a rejection notification only by:

```text
(templateCode, visitInstanceId)
```

A campus can be rejected multiple times.

Required identity:

```text
Reject #1 → rejection business event E100
Resubmit
Reject #2 → rejection business event E205
```

Email/recovery must distinguish E100 vs E205 even for the same instance.

Prefer an existing immutable:
- event ID;
- audit ID;
- decision revision;
- equivalent persisted event identity.

Do not create schema until proving existing identity insufficient.

---

# 27. Reject recovery regression case

Mandatory:

```text
Reject #1
→ email SENT

Registrant/contact resubmits according to allowed workflow

Reject #2
→ email attempt FAILS

Recovery:
→ sees Reject #2 is missing
→ old SENT for Reject #1 does NOT suppress it
→ retries only Reject #2 if safe
```

After success:
- later sweeps do not duplicate Reject #2 email.

---

# 28. Ambiguous SMTP outcome

Never auto-retry a message when provider acceptance is uncertain.

Classify:

```text
PROVEN_NOT_DISPATCHED
→ auto retry allowed

CONFIG/RENDER FAILURE BEFORE OUTBOUND
→ controlled retry allowed

SENT / PROVIDER_ACCEPTED
→ complete

OUTCOME_UNKNOWN
→ NO automatic retry
→ manual/operator decision
```

Do not treat all FAILED/QUEUED as retryable.

Do not claim exactly-once SMTP delivery.

---

# 29. Retry cap / scan window

If current policy is:

```text
max automatic attempts = 5
scan window = 7 days
```

unresolved notifications must not silently disappear.

When exhausted/aged/ambiguous, expose an operator-visible condition such as:

```text
RETRY_EXHAUSTED
NEEDS_ATTENTION
OUTCOME_UNKNOWN
FAILED_PERMANENT
```

or a safely derived equivalent from existing durable email records.

Document manual recovery.

### MUST ASK RULE

If implementing operator recovery requires choosing between:
- new admin UI;
- admin endpoint;
- CLI/runbook;
- database/manual operation;

and there is no existing canonical operations pattern, present the options and ask the user before building a new management surface.

---

# 30. Retry backoff / concurrency

Do not retry every worker tick.

Reuse current attempt timestamps if possible.

Multiple workers/app instances must not dispatch the same event concurrently.

Use DB-backed locking/idempotency, not only in-memory locks.

---

# 31. Post-commit API consistency

Business action success must remain success after commit.

Example:

```text
Reject DB commit succeeds
email render/delivery fails
```

API must not falsely report that Reject itself failed.

Notification failure must:
- be logged/recorded;
- remain recoverable according to safe outcome rules.

Same principle for contact expiry and other post-commit notification flows.

---

# 32. 72-hour registration boundary

72h applies ONLY:

```text
Create
PRE-APPROVAL Edit submission
Resubmit after rejection
```

It does NOT apply:
- Approved Amendment;
- contact metadata update;
- contact account provisioning;
- profile sync;
- INITIAL_CONFIRMATION;
- TRANSFER;
- accept/decline/resend/cancel;
- contact expiry;
- email recovery;
- passive time passage.

Do not change Amendment's own cutoff/policy.

---

# 33. Mandatory account tests

## ACCOUNT-01 — typed email does not create account

Before confirmation:
- no account created solely because email was entered.

## ACCOUNT-02 — successful confirmation, no account exists

After proof:
- canonical provision;
- eligible VISITOR account if confirmed by business rule;
- bind UserId to instance.

## ACCOUNT-03 — existing eligible account

- reuse same UserId;
- no duplicate account.

## ACCOUNT-04 — same email, different metadata

- instance snapshot uses contextual values;
- existing account profile unchanged automatically.

## ACCOUNT-05 — incompatible/inactive account

- enforce existing canonical eligibility;
- no silent role conversion/reactivation;
- if canonical behavior is unclear, test remains blocked pending user decision.

## ACCOUNT-06 — decline/expiry

- no new account created solely from pending email.

---

# 34. Mandatory profile-sync tests

For existing account with differing instance snapshot:

```text
authenticated holder opens confirmation/assigned instance
→ sees profile-difference prompt
```

`Giữ nguyên hồ sơ`:
- account unchanged;
- instance snapshot unchanged.

`Cập nhật hồ sơ cá nhân`:
- only authenticated holder may execute;
- only approved account-owned fields copied;
- current instance snapshot remains valid;
- historical instance snapshots not rewritten.

Unauthorized registrant/other user:
- cannot synchronize another person's account profile.

---

# 35. Mandatory instance-scope authorization tests

For request:

```text
HN instance → contact A
DN instance → contact B
```

A must:
- access allowed HN actions;
- be denied DN mutation;
- not become whole-request owner merely due to VISITOR role.

B gets symmetrical DN scope.

A random Visitor C:
- no access solely from role.

Registrant:
- keeps current request-owner permissions.

Tests must assert backend authorization, not only frontend buttons.

---

# 36. Mandatory action-matrix report BEFORE broad authorization changes

Before implementing new operational-contact rights beyond simple view:

produce:

| Action | Current registrant behavior | Data scope | Proposed contact behavior | Evidence | Decision |
|---|---|---|---|---|---|
| View | ... | instance/request | ... | file/function | CONFIRMED / ASK |
| Edit | ... | ... | ... | ... | ... |
| Resubmit | ... | ... | ... | ... | ... |
| Feedback | ... | ... | ... | ... | ... |
| Amendment | ... | ... | ... | ... | ... |
| Cancel | ... | ... | ... | ... | ... |
| Files | ... | ... | ... | ... | ... |
| ... | ... | ... | ... | ... | ... |

For every row marked `ASK`, stop that permission change and ask the user.

This is mandatory to prevent accidental cross-instance authority.

---

# 37. Do NOT do these

```text
- guess unclear permissions
- give all VISITOR accounts contact rights
- authorize by role alone
- let contact A mutate sibling campus B
- grant whole-request ownership automatically
- auto-overwrite existing account profile from registrant snapshot
- create account before email proof
- create duplicate account for same normalized email
- silently convert an internal/incompatible account to VISITOR
- sync profile without account-holder action
- rewrite historical snapshots after profile sync
- send confirmation for metadata-only change
- put existing contact editing back into Visit Edit
- apply 72h to contact/profile/account actions
- apply registration 72h to Approved Amendment
- remove current contact when TRANSFER merely starts
- confuse current A with pending B
- dedupe Reject by instance only
- auto-retry OUTCOME_UNKNOWN
- silently lose exhausted notifications
- return false business failure after post-commit mail failure
- create new table/column without proving it is necessary
```

---

# 38. Required stop-and-ask decision points

The agent MUST ask the user, with code evidence, if any of these are not already unambiguously defined:

1. Which account roles are eligible to become operational contact?
2. Which contact snapshot fields may sync into account profile?
3. Is organization/job title global account profile data or instance-only?
4. Can operational contact Resubmit only their own instance, or is current Resubmit whole-request?
5. Can operational contact create/cancel Amendments for their instance?
6. Can operational contact cancel a visit instance, or only registrant can cancel request?
7. Which feedback actions are instance-local?
8. Which file/document actions should operational contact have?
9. Can pending TRANSFER target B edit their pending metadata?
10. What operator surface should handle `OUTCOME_UNKNOWN` / `RETRY_EXHAUSTED` if no canonical admin mechanism exists?
11. Any action whose current backend changes sibling campuses/request aggregate beyond the target instance.
12. Any schema change.

Do not batch these as speculative questions upfront if code can answer them.
Audit first; ask only when a real ambiguity remains.

---

# 39. Implementation order

```text
Phase A — Preflight + preserve WIP

Phase B — Audit account provisioning and account-role eligibility

Phase C — Finalize Detail/contact tooltip UX

Phase D — Keep same-email metadata update isolated

Phase E — Confirm/provision/reuse account only after verified contact acceptance

Phase F — Add self-service account-profile comparison/sync

Phase G — Build Visitor-action scope matrix

Phase H — Implement only CONFIRMED instance-scoped contact permissions

Phase I — Stop and ask for unresolved permission rows

Phase J — Lock current A vs pending B TRANSFER semantics

Phase K — Finish Reject event-centric recovery

Phase L — Finish safe email outcome/retry exhaustion behavior

Phase M — Verify post-commit API semantics

Phase N — Run full regression gates
```

---

# 40. Final report format

## 1. Preflight

```text
Branch:
Start HEAD:
End HEAD:
WIP preserved:
```

## 2. Confirmed vs Asked decisions

Two sections:

```text
Confirmed from code/business rule:
...

Asked user and answer:
...
```

Do not hide inferred decisions.

## 3. Account provisioning

```text
When account is created:
Lookup key:
Reuse behavior:
New-account role:
Incompatible account behavior:
Fields seeded:
```

## 4. Profile sync

```text
Difference detection:
Prompt:
Allowed fields:
Authorization:
Historical snapshot behavior:
```

## 5. Operational-contact permissions

Provide the final action matrix with:
- registrant;
- current operational contact;
- random Visitor;
- sibling-campus operational contact;
- read-only roles if relevant.

## 6. Contact identity flow

```text
Same email:
Changed email pre-decision:
Changed email decided:
Pending transfer:
Expiry:
```

## 7. Recovery

```text
Reject event identity:
Expiry event identity:
Retryable outcomes:
OUTCOME_UNKNOWN:
Retry cap:
Backoff:
Operator recovery:
```

## 8. Changed files

File + reason.

## 9. Tests/gates

```text
dotnet build
unit
architecture
VisitRequests integration
Emails integration
frontend typecheck
frontend unit
frontend build
```

## 10. Remaining debt / BLOCKED decisions

Only real unresolved items.

---

# 41. Definition of Done

- [ ] Existing contact is not edited in Visit Request Edit.
- [ ] Detail View owns contact management.
- [ ] Guidance lives in `?` tooltip rather than persistent long text.
- [ ] Same email updates snapshot only.
- [ ] Metadata-only change sends no confirmation.
- [ ] Account is not created before email ownership proof.
- [ ] Existing eligible account is reused by normalized verified email.
- [ ] No duplicate account for same email.
- [ ] Existing account profile is never silently overwritten by registrant snapshot.
- [ ] Existing account holder can explicitly choose whether to sync allowed profile fields.
- [ ] Only account holder may execute profile sync.
- [ ] Profile sync does not rewrite historical snapshots.
- [ ] Confirmed operational contact becomes a real authenticated actor.
- [ ] Operational-contact authorization is bound to assigned `visitInstanceId`, not VISITOR role alone.
- [ ] Contact cannot mutate sibling instances without explicit business rule.
- [ ] Edit/Resubmit/Feedback/etc. are audited action-by-action before permission expansion.
- [ ] Ambiguous permission decisions are ASKED, not invented.
- [ ] INITIAL_CONFIRMATION / TRANSFER semantics remain correct.
- [ ] Transfer keeps A until B accepts.
- [ ] Pending A/B identities are never mixed.
- [ ] Reject recovery uses exact rejection business event.
- [ ] Old Reject mail cannot suppress later Reject mail.
- [ ] OUTCOME_UNKNOWN is not automatically resent.
- [ ] Retry exhaustion remains observable and recoverable.
- [ ] Business success is not falsely reported as failure after mail problem.
- [ ] Registration 72h remains limited to Create / PRE-APPROVAL Edit / Resubmit.
- [ ] Approved Amendment/contact/profile operations remain outside registration 72h.
