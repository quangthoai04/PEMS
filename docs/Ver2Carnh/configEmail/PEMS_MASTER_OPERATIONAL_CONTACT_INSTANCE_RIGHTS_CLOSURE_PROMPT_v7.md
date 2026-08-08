# PEMS — Master Closure Prompt v7
## Operational Contact Account + Instance-Scoped Visitor Rights + Contact Management + Recovery + 72H Boundaries

> **This prompt supersedes v6.**
> Continue from the CURRENT working tree on `Cảnh-Iter1`.
> Preserve every prior fix and every uncommitted file.
> Do not reset, discard, overwrite, or re-implement completed work.

---

# 0. NON-NEGOTIABLE RULE: DO NOT GUESS

You MUST NOT invent business rules.

If, after auditing the current code/schema/tests, a point is still genuinely ambiguous and implementing one option would alter business semantics, authorization, data ownership, status transitions, or schema:

```text
STOP only that subtask
→ show exact code/database evidence
→ explain the competing options
→ ask the user
→ do not implement that unresolved choice until answered
```

Continue other independent work that is not blocked.

Do NOT ask the user again about business decisions already CONFIRMED in this prompt.

---

# 1. CONFIRMED business model

The following distinctions are FINAL for this task:

```text
REQUEST OWNER / REGISTRANT
≠
OPERATIONAL CONTACT OF ONE VISIT INSTANCE
≠
GLOBAL USER ACCOUNT PROFILE
≠
PER-INSTANCE OPERATIONAL-CONTACT SNAPSHOT
```

## Registrant

The registrant remains the owner of the overall Visit Request according to the existing workflow.

## Operational Contact

After confirmation and account binding, the operational contact becomes a REAL authenticated actor for the exact Visit Instance(s) where:

```text
visit_request_campuses.operational_contact_user_id
==
currentUser.UserId
```

The contact is NOT the owner of sibling campuses merely because they are a `VISITOR`.

Authorization must be **instance-scoped**, not role-only.

---

# 2. CONFIRMED rights of an Operational Contact

Once the contact has confirmed the role and has/reuses a valid account, they MUST be allowed — for the assigned Visit Instance only — to:

```text
1. View the assigned instance and its relevant status/history
2. Edit allowed instance-local information
3. Resubmit / gửi lại after Reject
4. Send feedback / response
5. Create Amendment after approval
6. View / preview / download files belonging to the assigned instance
7. Initiate transfer of their operational-contact role
8. Resend a pending transfer invitation
9. Cancel a pending transfer
10. Manage/update their operational-contact metadata for that instance
```

These permissions are CONFIRMED.

Do NOT ask whether these permissions should exist.

What still requires code audit is HOW to make each action truly instance-scoped without leaking side effects into sibling campuses or the whole request.

---

# 3. Rights the Operational Contact does NOT gain automatically

The contact must NOT automatically gain:

```text
- whole-request ownership
- permission to add a new campus
- permission to remove a campus
- permission to edit sibling-campus data
- permission to change registrant identity
- permission to approve/reject as Staff Leader
- permission to manage unrelated accounts/roles
- permission to cancel the whole multi-campus request unless separately confirmed later
```

Do not authorize any action merely by:

```text
role == VISITOR
```

Required authorization shape:

```text
currentUser.UserId == targetInstance.OperationalContactUserId
```

plus whatever status/business guards that action already requires.

---

# 4. Preflight

Before editing:

1. Confirm branch = `Cảnh-Iter1`.
2. Record:
   - HEAD SHA;
   - `git status`;
   - modified files;
   - untracked files.
3. Preserve all existing WIP.
4. Do not touch existing stashes.
5. Audit the actual working tree, not only committed HEAD.

Locate exact implementations of:

```text
VisitRequestV2DetailView
CampusVisitDetailCard
EditVisitRequestV2Page
CampusVisitCard
VisitFormActions / visitV2Actions

VisitRequestV2EditService
Resubmit service/handler/controller
Amendment create/view/cancel handlers
Feedback/response handlers
file preview/download authorization
OperationalContactGuards

SaveOperationalContactCommand
UpdateOperationalContactProfileCommand
ReplaceOperationalContactCommandHandler
InitiateOperationalContactTransferCommandHandler
Resend/Cancel transfer handlers
Accept/Decline contact handlers

OperationalContactInvitationService
OperationalContactMaintenanceService

RejectCampusInstanceCommandHandler
RecoverableVisitEmailSender
IVisitNotificationRecoveryService
recovery hosted service
SystemEmailDispatcher

account / Google SSO / Visitor provisioning service
User entity/profile fields
```

---

# 5. Preserve previous fixes

Do NOT regress:

```text
- dynamic campus limit
- Create/Edit same canonical active-campus ceiling
- controlled campus select
- correct campusId state/payload
- one Edit success toast
- one Resubmit success toast
- Create lead time = 72h
- PRE-APPROVAL Edit lead time = 72h
- Resubmit after Reject lead time = 72h
- Approved Amendment is NOT subject to registration 72h
- passive time <72h does not auto-expire/reject/email
- per-campus Reject email
- contact-expiry email to registrant
- Visit Edit separated from existing-contact management
- same-email metadata update without token/mail
- changed-email INITIAL_CONFIRMATION / TRANSFER
- transfer preserves current contact until acceptance
- contactFullName uses actual name
- Edit/Resubmit cannot mutate existing contact profile through request-edit payload
```

---

# 6. Visit Edit vs Contact Management

## Existing campus

For an existing campus instance, Visit Request Edit must NOT edit:

```text
operational-contact full name
organization
job title
phone
email
```

No `Thay đổi đầu mối` action inside the Visit Request Edit form.

A read-only summary may remain.

## Newly added campus

A newly added campus has no existing contact relation yet and may still require initial contact fields.

Do not incorrectly lock a brand-new campus.

## Backend

PRE-APPROVAL Edit and Resubmit must refuse mutation of an already-existing contact through the request-edit payload.

Keep stable errors such as:

```text
IMMUTABLE_CONTACT_IDENTITY
IMMUTABLE_CONTACT_PROFILE
```

or the project's equivalent.

---

# 7. Detail View is the home of contact management

Each campus card in Detail should show:

```text
Họ và tên
Đơn vị công tác
Chức vụ
Số điện thoại
Email
Trạng thái xác nhận
Nguồn xác nhận
Xác nhận lúc
Pending transfer / invitation state if any
```

Authorized actor gets:

```text
Chỉnh sửa đầu mối
```

or the established equivalent.

Contact management must be scoped by:

```text
visitRequestId + visitInstanceId
```

Do not route back to whole-request Edit.

---

# 8. Tooltip copy

Remove the long always-visible sentence:

> Thông tin đầu mối vận hành của cơ sở đã có được quản lý ở màn hình chi tiết đơn (mục "Quản lý đầu mối"), không sửa trong biểu mẫu đăng ký.

Move the guidance to the `?` tooltip.

Recommended VI copy:

> **Đầu mối dùng để phối hợp tại cơ sở này, không nhất thiết là tài khoản đang đăng nhập. Với đầu mối đã có, hãy chỉnh sửa tại Chi tiết đơn → Quản lý đầu mối. Email mới cần được chính đầu mối xác nhận trước khi được liên kết hoặc tạo tài khoản.**

Add matching EN translation.

Do not show the same long guidance in two places.

---

# 9. Same email vs changed email

## SAME normalized email

This is metadata/snapshot update only.

Allowed:
- name;
- organization;
- job title;
- phone.

Must NOT:
- create identity-change row;
- create token;
- send confirmation email;
- create transfer;
- change `OperationalContactUserId`;
- overwrite account profile automatically;
- clear confirmation;
- change request/campus status;
- re-open contact gate;
- create Amendment;
- bump visit form revision;
- invoke registration 72h.

## DIFFERENT normalized email

This is identity change.

Only this path may:
- create INITIAL_CONFIRMATION / replace;
- create TRANSFER;
- mint token;
- send confirmation email.

---

# 10. Account identity and per-instance snapshot are separate

Core account identity:

```text
normalized verified email
```

Per-instance snapshot is contextual.

Example:

```text
Account:
UserId = 501
Email = kim@example.com
Global profile name = Kim Min Jae
```

HN instance snapshot:

```text
Name = Kim Min Jae
Organization = SeoulTech GEC
JobTitle = Partnerships Manager
Phone = ...
OperationalContactUserId = 501
```

Another request/campus may have different contextual metadata but still bind the same `UserId = 501`.

Do NOT treat name/title/org/phone differences as a new identity when verified email is the same.

---

# 11. Do not create an account just because somebody typed an email

Required lifecycle:

```text
new email entered
→ pending invitation
→ email ownership proven by confirmation/SSO
→ lookup existing account by normalized verified email
→ reuse eligible account OR provision a new account
→ bind UserId to the instance
```

Before successful confirmation:

```text
NO active account should be created solely from the typed email
```

If invitation expires/declines/cancels:

```text
no account is created solely because that email appeared in the form
```

---

# 12. Reuse existing eligible account

After successful confirmation:

```text
lookup by normalized verified email
```

If an eligible account exists:

```text
reuse existing UserId
do not create duplicate account
do not overwrite account profile automatically
bind existing UserId to the target instance
```

Do not silently:
- change role;
- reactivate disabled account;
- convert internal account to VISITOR.

If current repository has more than one conflicting rule for account eligibility, show evidence and ASK before changing that rule.

---

# 13. Provision new account only after successful confirmation

If no eligible account exists after proof of email ownership:

reuse the canonical account/Google SSO provisioning path.

Expected external contact role is:

```text
VISITOR
```

IF that is consistent with the current canonical role model.

Do not create a parallel account-creation pipeline inside contact handlers.

For a brand-new account, seed only fields the account schema canonically owns.

If ownership of a field is unclear, ASK before mapping it.

---

# 14. Self-service account profile sync — CONFIRMED

This feature is REQUIRED.

If an EXISTING account holder's account profile differs from the instance/contact snapshot, do NOT auto-update the account.

Offer only to the authenticated account holder:

> **Thông tin liên hệ trong yêu cầu này khác hồ sơ PEMS của bạn. Bạn có muốn cập nhật hồ sơ cá nhân không?**

Actions:

```text
Giữ nguyên hồ sơ
Cập nhật hồ sơ cá nhân
```

Rules:

```text
- Registrant cannot update the contact's account profile.
- Staff cannot silently update it.
- Only the authenticated account holder may perform profile sync.
- "Giữ nguyên hồ sơ" changes nothing in the account.
- Account profile update does not rewrite historical visit/contact snapshots.
- Current instance snapshot remains contextual data.
```

### Field ownership

Before implementing the copy:

audit which fields belong to the global account profile.

If it is not unambiguous whether:
- organization;
- job title;
- phone;
- other contact fields;

belong to the global account profile, STOP and ASK.

Do not infer.

---

# 15. Instance-scoped authorization helper

Create/reuse ONE canonical authorization helper/guard for:

```text
IsCurrentOperationalContact(targetInstance, currentUser)
```

or equivalent.

Do not scatter raw comparisons inconsistently.

The guard should establish:

```text
currentUser.UserId == targetInstance.OperationalContactUserId
```

and preserve any existing account/status constraints.

Use it for all newly granted operational-contact actions.

---

# 16. CONFIRMED permission: View assigned instance

Operational contact can view the assigned instance.

Backend must return only the data they are authorized to see for that target instance.

Do not broaden this into sibling-campus visibility.

If existing detail endpoint currently returns the whole request with all campuses, audit whether sensitive sibling data leaks.

If fixing this requires choosing between:
- filtered response;
- dedicated instance-detail route;
- another security model;

and current code does not already define the intended approach, ASK before redesigning the API.

---

# 17. CONFIRMED permission: Edit instance-local information

Operational contact may edit allowed information for their assigned instance.

Do not automatically grant mutation of request-level/shared fields.

Audit each editable field and classify:

```text
INSTANCE_LOCAL
REQUEST_SHARED
IDENTITY_ONLY
UNCLEAR
```

Implement contact edit permission only for `INSTANCE_LOCAL`.

If a field is `UNCLEAR`, ASK.

Do not let HN contact mutate DN/HCM or request-wide ownership data.

---

# 18. CONFIRMED permission: Resubmit after Reject

Operational contact MUST be able to resubmit the assigned instance after Reject.

Required outcome:

```text
HN instance rejected
HN contact edits HN
HN contact resubmits
→ only HN target instance re-enters the proper review workflow
→ sibling instances are not reset/resubmitted
```

Because this is Resubmit after Reject:

```text
plannedStart >= serverNow + 72h
```

must be revalidated for the target instance according to the canonical scheduling rule.

## Critical audit

If the existing Resubmit implementation is whole-request:

DO NOT merely authorize the contact to call it.

Refactor/add an instance-scoped execution path so the CONFIRMED permission can be implemented without sibling side effects.

If accomplishing this requires a new business state transition not representable by the current aggregate/status model, show exact evidence and ASK before inventing a new state.

---

# 19. CONFIRMED permission: Feedback / response

Operational contact MUST be able to send feedback/respond for the assigned instance.

Backend authorization must verify target instance ownership.

If current feedback storage is already instance-local:
- reuse it.

If current feedback is request-level and writing feedback necessarily affects all campuses:
- do not grant the whole request mutation accidentally;
- show the current model and ASK how the user wants request-wide feedback to behave.

The high-level permission itself is already confirmed; only unresolved data-scope semantics may require a question.

---

# 20. CONFIRMED permission: Create Amendment after approval

Operational contact MUST be able to create an Amendment for their assigned approved instance.

Registration lead time 72h MUST NOT be applied.

Use the canonical Amendment policy/cutoff.

Required:

```text
targetInstance = assigned instance
currentUser = its OperationalContactUserId
instance approved / amendment-eligible
→ may create Amendment targeted only to that instance
```

Do not allow:
- sibling-campus amendment;
- whole-request mutation merely because the user is contact for one instance.

If current Amendment model is request-wide and cannot target one instance without a business-model change:
- show exact current schema/handler behavior;
- ASK before inventing a new cross-scope model.

Do not revoke the confirmed permission; resolve the implementation gap with the user.

---

# 21. CONFIRMED permission: View / preview / download files of the assigned instance

Operational contact MUST be able to access files belonging to the assigned instance.

Authorization must resolve:

```text
file
→ owning business object
→ target VisitInstanceId
→ currentUser == OperationalContactUserId
```

Do NOT authorize by:
- role VISITOR alone;
- knowing `fileId`;
- belonging to the same whole request only.

Files of sibling campuses remain forbidden unless current business ownership explicitly makes them shared.

### Shared-file ambiguity

If a file is genuinely request-wide/shared across campuses and current ownership cannot map it to one instance:
- show evidence;
- ASK whether assigned contacts should access that shared file.

Do not guess.

---

# 22. CONFIRMED permission: Transfer operational-contact role

Current operational contact A MUST be able to initiate transfer of their own assigned instance to B.

During pending transfer:

```text
A remains current contact
A keeps current instance rights
B is pending
B does not gain operational-contact rights yet
```

After B accepts:

```text
reuse/provision B account
bind B as OperationalContactUserId
A loses rights that depended solely on being current contact
B gains the confirmed instance-scoped rights
```

If B declines/expires/cancels:
- A remains current;
- B gets no operational-contact rights.

---

# 23. CONFIRMED permission: Resend / Cancel transfer

Current contact A MUST be able to:

```text
Resend pending transfer invitation
Cancel pending transfer
```

only for the target instance where A is still current contact.

Resend must preserve canonical:
- cooldown;
- max resend;
- token version;
- expiry behavior.

Cancel must:

```text
pending B → CANCELLED
A remains current contact
```

No sibling effect.

---

# 24. Pending TRANSFER UI: current A vs pending B

Never mix:

```text
A = current confirmed
B = pending transfer target
```

Preferred confirmed UI model:

```text
Current contact
A
[Chỉnh sửa thông tin] [Chuyển giao]

Pending transfer
B
expiry...
[Resend] [Cancel]
```

Do not show a merged model where:
- `UserId = A`
- name/email = B.

Do not create a second transfer while one is pending.

If current product already exposes editing pending B metadata and removing it would break a relied-upon workflow, show evidence and ASK before changing that behavior.

---

# 25. contactFullName

Keep the fix:

```text
PendingSnapshotJson.fullName
→ compatible legacy key if needed
→ safe neutral fallback
```

Never use email as `contactFullName`.

For TRANSFER, do not use outgoing A's name for incoming B.

---

# 26. Verified registrant self-match

Preserve existing canonical self-match if current code requires:

```text
newEmail == RegistrantEmail
RegistrantUserId != null
EmailVerifiedAt != null
```

No extra confirmation is required if identity is already proven by the current canonical flow.

If repository code contains competing rules, ASK before changing.

---

# 27. Contact invitation expiry

Keep invitation expiry separate from visit-registration lead time.

Example canonical policy:

```text
INITIAL_CONFIRMATION = 72h
TRANSFER = 24h
```

On expiry:
- invitation becomes EXPIRED;
- token invalid;
- no account created solely from pending email;
- pending target not bound;
- transfer preserves A;
- registrant/initiator receives expiry notification.

---

# 28. Reject recovery must use exact rejection business event

Do NOT dedupe using:

```text
(templateCode, visitInstanceId)
```

Same campus can be rejected more than once.

Required semantics:

```text
Reject #1 → business event E100 → email E100
Resubmit
Reject #2 → business event E205 → email E205
```

Old SENT for E100 must not suppress recovery for E205.

Prefer existing immutable:
- event ID;
- audit ID;
- decision revision;
- equivalent persisted event identity.

Do not create schema unless necessary.

---

# 29. Mandatory Reject recovery test

```text
Reject #1 → email SENT
Resubmit target instance
Reject #2 → first email attempt FAILS
Recovery → finds Reject #2 independently
Recovery → retries only Reject #2 when safe
Later sweep → no duplicate after success
```

Also test sibling campuses have independent event identities.

---

# 30. Ambiguous SMTP outcome

Never automatically retry an email when provider acceptance is uncertain.

Classification:

```text
PROVEN_NOT_DISPATCHED
→ auto retry allowed

CONFIG/RENDER FAILURE BEFORE OUTBOUND
→ controlled retry allowed

SENT / PROVIDER_ACCEPTED
→ complete

OUTCOME_UNKNOWN
→ NO auto retry
→ operator/manual decision
```

Do not classify all `FAILED` or all `QUEUED` as retryable.

Do not claim exactly-once SMTP delivery.

---

# 31. Retry cap / scan window

If policy remains:

```text
automatic attempts = 5
scan window = 7 days
```

unresolved notifications must not disappear silently.

Expose a durable/derived operator-visible condition such as:

```text
RETRY_EXHAUSTED
NEEDS_ATTENTION
OUTCOME_UNKNOWN
FAILED_PERMANENT
```

or equivalent.

Document manual/operator recovery.

If no canonical operations surface exists and implementing one requires choosing admin UI vs endpoint vs runbook, ASK before creating a new management surface.

---

# 32. Retry backoff / concurrency

Do not retry every worker tick.

Use durable timestamps/backoff.

Multiple app instances/workers/manual retries must not concurrently send the same business event.

Use DB-backed locking/idempotency.

---

# 33. Post-commit API consistency

If business transaction commits:

```text
Reject / Expiry / Contact mutation
```

and notification later fails:

```text
business result remains success
notification failure is recorded separately
```

Do not return misleading mutation failure after business state already committed.

---

# 34. 72-hour boundary

72h applies ONLY to:

```text
Create
PRE-APPROVAL Edit submission
Resubmit after Reject
```

It does NOT apply to:

```text
Approved Amendment
contact metadata update
account provisioning
profile sync
INITIAL_CONFIRMATION
TRANSFER
Resend/Cancel/Accept/Decline
contact expiry
email recovery
passive time
```

Operational-contact Resubmit after Reject DOES use the 72h rule because it is a Resubmit event.

Operational-contact Amendment after approval DOES NOT.

---

# 35. Mandatory permission matrix

Before finalizing, produce and verify:

| Action | Registrant | Assigned Operational Contact | Sibling Contact | Random VISITOR |
|---|---:|---:|---:|---:|
| View assigned instance | existing policy | ALLOW | DENY | DENY |
| Edit instance-local data | existing policy | ALLOW | DENY | DENY |
| Resubmit rejected instance | existing policy | ALLOW | DENY | DENY |
| Feedback/respond | existing policy | ALLOW | DENY | DENY |
| Create Amendment after approval | existing policy | ALLOW | DENY | DENY |
| View/preview/download instance files | existing policy | ALLOW | DENY | DENY |
| Transfer contact | existing policy | ALLOW current instance | DENY | DENY |
| Resend transfer | existing policy | ALLOW current instance | DENY | DENY |
| Cancel transfer | existing policy | ALLOW current instance | DENY | DENY |
| Add/remove campus | existing policy | DENY | DENY | DENY |
| Edit sibling instance | existing policy | DENY | DENY | DENY |
| Approve/Reject as Staff Leader | role policy | DENY | DENY | DENY |

If an implementation row cannot be made instance-scoped with the current model, mark it BLOCKED and ASK with evidence.

---

# 36. Mandatory tests

Add/keep tests for:

## Account
- typed email does not create account;
- successful confirmation provisions new account only when needed;
- existing eligible account reused;
- no duplicate email account;
- same email/different snapshot does not overwrite account;
- incompatible account follows canonical eligibility;
- decline/expiry creates no account solely from pending email.

## Profile sync
- only account holder sees/can execute sync;
- Keep profile leaves account unchanged;
- Update profile changes only approved account-owned fields;
- historical snapshots not rewritten;
- registrant cannot sync another person's profile.

## Instance scope
- assigned contact can view own instance;
- sibling contact cannot;
- random VISITOR cannot;
- assigned contact can edit own instance-local data;
- assigned contact can Resubmit own rejected instance;
- sibling state untouched by Resubmit;
- 72h enforced on that Resubmit;
- assigned contact can feedback/respond;
- assigned contact can create target-instance Amendment;
- Amendment not blocked by registration 72h;
- assigned contact can access own instance files;
- cannot access sibling files;
- assigned current contact can transfer;
- can Resend/Cancel own pending transfer;
- cannot manage sibling transfer.

## Recovery
- repeated Reject events independent;
- ambiguous email outcome not auto-retried;
- proven pre-dispatch failure retryable;
- retry exhaustion observable;
- no duplicate after successful recovery.

---

# 37. Stop-and-ask points that REMAIN

Do NOT ask again whether the six confirmed rights are allowed.

Ask only if code audit leaves one of these unresolved:

```text
1. Exact global-account fields allowed in self-service profile sync.
2. Eligibility of existing non-VISITOR/internal/inactive accounts as operational contact.
3. A specific editable field is unclear whether instance-local or request-shared.
4. Current Feedback model is request-wide and cannot be scoped without changing business semantics.
5. Current Amendment model is request-wide and cannot target one instance without a model change.
6. A file is truly request-wide/shared and ownership cannot be resolved to one instance.
7. Implementing instance-scoped Resubmit requires a new state not representable by the current model.
8. Pending B metadata editing has an existing relied-upon workflow that conflicts with the preferred UI.
9. Operator recovery needs a new management surface and the project has no canonical pattern.
10. Any schema/table/column change.
```

For each:
- provide exact evidence;
- show options;
- ask the user;
- do not guess.

---

# 38. Suggested implementation order

```text
A. Preflight + preserve WIP
B. Audit existing account provisioning/eligibility
C. Finalize Detail tooltip/contact UX
D. Finalize same-email metadata path
E. Confirm/reuse/provision account after verified contact acceptance
F. Add self-service profile comparison/sync
G. Create/reuse canonical instance-contact authorization guard
H. Implement instance-scoped View/Edit
I. Implement instance-scoped Resubmit after Reject
J. Implement instance-scoped Feedback
K. Implement instance-scoped Amendment creation
L. Implement instance-scoped file access
M. Lock Transfer / Resend / Cancel authorization
N. Lock A-current vs B-pending UI
O. Finish rejection-event recovery
P. Finish safe SMTP outcome/retry exhaustion
Q. Verify post-commit API semantics
R. Run full gates
```

---

# 39. Final report format

## 1. Preflight

```text
Branch:
Start HEAD:
End HEAD:
WIP preserved:
```

## 2. Decisions

```text
Confirmed from this prompt:
...

Asked user because code was ambiguous:
...
```

## 3. Account provisioning

```text
When account is created:
Lookup key:
Reuse behavior:
New account role:
Eligibility behavior:
Fields seeded:
```

## 4. Profile sync

```text
Difference detection:
Prompt:
Authorized actor:
Fields copied:
Historical snapshot behavior:
```

## 5. Instance authorization

Provide final matrix for:
- registrant;
- assigned operational contact;
- sibling contact;
- random VISITOR.

## 6. Resubmit

```text
Target instance:
Sibling behavior:
72h:
Status transition:
```

## 7. Amendment

```text
Target instance:
Authorization:
72h exclusion:
Canonical cutoff/policy:
```

## 8. File access

```text
File ownership resolution:
Authorized instance:
Sibling denial:
Shared-file decisions:
```

## 9. Transfer

```text
Current A:
Pending B:
Resend:
Cancel:
Accept:
Rights handover:
```

## 10. Recovery

```text
Reject business-event key:
Retryable outcomes:
OUTCOME_UNKNOWN behavior:
Retry cap:
Backoff:
Operator recovery:
```

## 11. Changed files

File + reason.

## 12. Tests / gates

```text
dotnet build
backend unit
architecture
VisitRequests integration
Emails integration
frontend typecheck
frontend unit
frontend build
```

## 13. Remaining BLOCKED decisions/debt

Only real unresolved items.

---

# 40. Definition of Done

- [ ] Operational contact becomes a real authenticated actor after confirmation/binding.
- [ ] Authorization is per `OperationalContactUserId` + target instance, not VISITOR role alone.
- [ ] Assigned contact can View own instance.
- [ ] Assigned contact can Edit instance-local data.
- [ ] Assigned contact can Resubmit own rejected instance only.
- [ ] Contact Resubmit revalidates 72h.
- [ ] Assigned contact can send Feedback/Response for own instance.
- [ ] Assigned contact can create Amendment after approval for own instance.
- [ ] Contact Amendment does NOT use registration 72h.
- [ ] Assigned contact can view/preview/download own instance files.
- [ ] Sibling files remain unauthorized.
- [ ] Current contact can initiate Transfer.
- [ ] Current contact can Resend/Cancel own pending transfer.
- [ ] A remains current until B accepts.
- [ ] B gets rights only after successful account reuse/provision + binding.
- [ ] Existing eligible account reused by normalized verified email.
- [ ] New account not created before email proof.
- [ ] No duplicate account for same normalized email.
- [ ] Existing account profile not silently overwritten.
- [ ] Account holder may explicitly sync approved profile fields.
- [ ] Historical snapshots not rewritten by profile sync.
- [ ] Existing contact remains outside Visit Request Edit.
- [ ] Same-email metadata update sends no confirmation.
- [ ] `contactFullName` remains correct.
- [ ] Reject recovery is business-event based.
- [ ] Old Reject mail cannot suppress later Reject mail.
- [ ] OUTCOME_UNKNOWN is not automatically resent.
- [ ] Retry exhaustion remains observable/recoverable.
- [ ] Business success is not falsely reported as failure after mail error.
- [ ] No regression to existing campus/toast/72h/Amendment/contact-email fixes.
