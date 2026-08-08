# PEMS — MASTER CLOSURE PROMPT v8
## Consolidated v4 → v7
### Operational Contact Management · Account Provisioning · Instance-Scoped Rights · Profile Sync · Reject/Expiry Email Recovery · Transfer · 72H · UI/Authorization Closure

> **This v8 supersedes v4, v5, v6, and v7.**
>
> Use this file as the single source of implementation instructions for the remaining closure work.
>
> Repository: `PEMS`  
> Working branch: `Cảnh-Iter1`
>
> Continue from the **CURRENT local working tree**. Preserve all existing WIP and already-completed fixes.

---

# 0. ABSOLUTE RULE — DO NOT GUESS / DO NOT INVENT BUSINESS RULES

This task contains several confirmed business decisions and several areas that may still depend on the current code/database model.

For any point where:

- current code has two or more plausible behaviors;
- code and documentation conflict;
- a requested permission cannot be made instance-scoped without changing request-level semantics;
- a handler unexpectedly mutates sibling campuses;
- a field may be global-account data or per-instance snapshot data;
- an account eligibility rule is unclear;
- a file is request-wide/shared rather than instance-owned;
- an Amendment/Feedback/Resubmit flow is currently request-wide;
- a new status/state transition would be required;
- an email delivery outcome is ambiguous;
- implementing a fix appears to require a new table/column/schema;
- an operator-recovery UX has no existing canonical pattern;

then the agent MUST:

```text
STOP only that subtask
→ collect exact code/database/test evidence
→ explain the current behavior
→ show the competing implementation options
→ ask the user for a decision
→ do NOT implement that unresolved business choice before receiving the answer
```

Continue independent work that is not blocked.

### Important

Do NOT ask the user again about decisions already explicitly CONFIRMED in this v8.

Do NOT choose "the most reasonable" behavior if it would create a new business rule.

Do NOT silently infer authorization from names such as `Visitor`, `Owner`, `Contact`, `Request`, or `Instance`.

---

# 1. CONFIRMED DOMAIN MODEL

The implementation must consistently separate these concepts:

```text
VISIT REQUEST OWNER / REGISTRANT
≠
VISIT INSTANCE OPERATIONAL CONTACT
≠
GLOBAL PEMS USER ACCOUNT
≠
PER-INSTANCE OPERATIONAL-CONTACT SNAPSHOT
≠
PENDING OPERATIONAL-CONTACT IDENTITY CHANGE
```

## 1.1 Registrant

The registrant remains the owner of the overall Visit Request according to the current Visitor workflow.

Registrant ownership does NOT automatically mean they are the operational contact of every campus.

## 1.2 Operational Contact

After successful confirmation/account binding, the operational contact becomes a **real authenticated actor** for the exact Visit Instance(s) where:

```text
visit_request_campuses.operational_contact_user_id
==
currentUser.UserId
```

The operational contact is NOT merely an email-confirmation recipient.

Their permissions must be scoped to the assigned instance.

## 1.3 Global account profile

The PEMS account represents the actual user identity.

Primary account identity:

```text
normalized verified email
```

## 1.4 Per-instance operational-contact snapshot

The snapshot represents how that person is described in a specific request/campus context.

It may contain:

```text
fullName
organization
jobTitle
phone
email
```

The same global account may legitimately have different snapshot metadata in different Visit Instances.

Example:

```text
Global account:
UserId = 501
Email = kim@example.com
Profile name = Kim Min Jae
```

Request A / HN:

```text
OperationalContactUserId = 501
FullName = Kim Min Jae
Organization = SeoulTech Global Engagement Center
JobTitle = International Partnerships Manager
Phone = ...
```

Request B / HCM:

```text
OperationalContactUserId = 501
FullName = Kim M. Jae
Organization = SeoulTech GEC
JobTitle = Global Partnership Manager
Phone = ...
```

This is valid.

Different snapshot metadata with the same verified email does NOT automatically mean a different account.

---

# 2. PREFLIGHT — PRESERVE CURRENT WIP

Before editing any code:

1. Confirm branch is `Cảnh-Iter1`.
2. Record:
   - HEAD SHA;
   - `git status`;
   - modified files;
   - untracked files;
   - current stashes count/names.
3. Preserve all current local WIP.
4. Do NOT:
   - reset;
   - checkout over modified files;
   - discard;
   - clean;
   - drop stashes;
   - rewrite existing fixes from scratch.
5. Audit the actual working tree, not only committed HEAD.

Report exact current implementation locations for at least:

```text
frontend:
- EditVisitRequestV2Page
- CampusVisitCard
- VisitRequestV2DetailView
- CampusVisitDetailCard
- contact management modal/page
- visitV2Actions
- VisitFormActions
- Visitor detail/history/action UI
- file preview/download UI
- Amendment UI
- Feedback UI

backend:
- VisitRequestV2CreateService
- VisitRequestV2EditService
- Resubmit service/handler/controller
- VisitMutationPolicy
- RejectCampusInstanceCommandHandler

- SaveOperationalContactCommand
- UpdateOperationalContactProfileCommand
- ReplaceOperationalContactCommandHandler
- InitiateOperationalContactTransferCommandHandler
- Accept/Decline/Cancel/Resend operational-contact handlers
- OperationalContactGuards
- OperationalContactInvitationService
- OperationalContactMaintenanceService
- PendingContactSnapshot parser/helper

- Amendment create/view/cancel handlers
- Feedback/response handlers
- file access authorization
- Visit Instance detail/history authorization

- account/Google SSO provisioning
- Visitor account provisioning
- User/account entity/profile fields

email/recovery:
- SystemEmailDispatcher
- ISystemEmailDispatcher
- RecoverableVisitEmailSender
- IVisitNotificationRecoveryService
- recovery hosted/background worker
- sent_emails/history model
- email_send_idempotency if applicable
```

---

# 3. PRESERVE ALL PREVIOUSLY COMPLETED FIXES

Do NOT regress the already-completed work:

```text
1. Dynamic campus count/limit.
2. Create and Edit use the SAME canonical active-campus ceiling.
3. Controlled campus select.
4. Displayed campus == form campusId == payload campusId.
5. Exactly one Edit success toast.
6. Exactly one Resubmit success toast.
7. No toast replay on refresh/back-forward.
8. 72h rule on Create.
9. 72h rule on PRE-APPROVAL Edit submission.
10. 72h rule on Resubmit after Reject.
11. Approved Amendment is NOT subject to registration 72h.
12. Passive time passage below 72h does NOT auto-expire/reject/email.
13. Reject email is per-campus and sent to registrant with correct reason/scope.
14. Contact invitation expiry notifies registrant/initiator.
15. Existing Operational Contact removed from Visit Request Edit.
16. Detail View owns Operational Contact management.
17. Same-email metadata update creates no token/mail/identity change.
18. Changed-email flow uses canonical INITIAL_CONFIRMATION / TRANSFER.
19. TRANSFER keeps current contact until acceptance.
20. contactFullName uses the real pending person's name, not their email.
21. Request Edit / Resubmit cannot mutate existing Operational Contact profile.
22. Post-commit business state remains truthful even if notification fails.
```

---

# 4. VISIT REQUEST EDIT ≠ OPERATIONAL CONTACT MANAGEMENT

## 4.1 Existing campus instance

For an existing campus:

```text
instanceId != null
```

Visit Request Edit must NOT contain editable fields for:

```text
operationalContactFullName
operationalContactOrganization
operationalContactJobTitle
operationalContactPhone
operationalContactEmail
```

It must NOT contain:

```text
Thay đổi đầu mối
Chỉnh sửa đầu mối
```

as an action inside the Visit Request Edit workflow.

A compact READ-ONLY summary is acceptable.

Do NOT show a disabled email input while keeping name/phone editable. That still mixes two workflows.

## 4.2 Newly added campus during Edit

A newly added campus has no existing operational-contact relation.

It MAY keep the initial contact-entry form if required by Create/add-campus logic.

The backend immutability guard applies to **kept/existing instances**, not a truly new campus that needs its first contact snapshot.

## 4.3 Backend boundary

PRE-APPROVAL Edit and Resubmit must not be usable to mutate an existing contact.

Keep/enforce stable errors such as:

```text
IMMUTABLE_CONTACT_IDENTITY
IMMUTABLE_CONTACT_PROFILE
```

or the current project equivalents.

The backend must reject attempts to mutate, through request-edit APIs:

```text
fullName
organization
jobTitle
phone
email
operational_contact_user_id
confirmation relation/source
```

Do not silently accept metadata changes merely because email stayed unchanged.

Contact changes belong to the dedicated contact-management workflow.

---

# 5. DETAIL VIEW IS THE HOME OF OPERATIONAL CONTACT MANAGEMENT

For each campus/instance, show a dedicated contact card.

Recommended visible information:

```text
Họ và tên
Đơn vị công tác
Chức vụ
Số điện thoại
Email
Trạng thái xác nhận
Nguồn xác nhận
Xác nhận lúc
Pending identity/transfer state if any
```

Authorized actors may see:

```text
Chỉnh sửa đầu mối
```

or the established equivalent.

The action must be scoped by:

```text
visitRequestId
visitInstanceId
```

Do NOT route to the whole-request `/edit` page.

Multi-campus isolation is mandatory:

```text
edit HN contact
→ must not change DN/HCM
```

---

# 6. TOOLTIP / EXPLANATORY TEXT

Remove the long permanent sentence:

> Thông tin đầu mối vận hành của cơ sở đã có được quản lý ở màn hình chi tiết đơn (mục "Quản lý đầu mối"), không sửa trong biểu mẫu đăng ký.

Put the guidance in the `?` tooltip instead.

Recommended VI copy:

> **Đầu mối dùng để phối hợp tại cơ sở này, không nhất thiết là tài khoản đang đăng nhập. Với đầu mối đã có, hãy chỉnh sửa tại Chi tiết đơn → Quản lý đầu mối. Email mới cần được chính đầu mối xác nhận trước khi được liên kết hoặc tạo tài khoản.**

Provide matching EN translation.

Do not duplicate the same long guidance both permanently and in tooltip.

---

# 7. DEDICATED CONTACT MANAGEMENT — EXACTLY TWO SAVE SEMANTICS

Contact-management form:

```text
Họ và tên
Đơn vị công tác
Chức vụ
Số điện thoại
Email
```

On Save:

```text
normalizedNewEmail = Normalize(newEmail)
normalizedCurrentEmail = Normalize(currentEmail)
```

There are exactly two semantic branches.

---

# 8. PATH A — SAME NORMALIZED EMAIL = METADATA/SNAPSHOT UPDATE ONLY

Condition:

```text
normalizedNewEmail == normalizedCurrentEmail
```

This is NOT an identity change.

Allowed:

```text
fullName
organization
jobTitle
phone
```

Expected:

```text
load one target instance/contact
→ authorize
→ optimistic concurrency check
→ update allowed snapshot fields
→ audit field changes
→ commit
→ return success
```

Must NOT:

```text
create VisitRequestIdentityChange
create EmailActionToken
mint confirmation token
send confirmation email
send transfer email
change OperationalContactUserId
change account identity
overwrite existing account profile automatically
clear confirmation
change confirmation source
set WAITING_CONTACT_CONFIRMATION
reopen contact gate
change campus/request approval status
bump form_revision
create Amendment
invoke registration 72h validator
```

No-op may continue returning the current stable no-change result such as:

```text
OPERATIONAL_CONTACT_PROFILE_NO_CHANGES
```

---

# 9. SAME PENDING EMAIL + METADATA CHANGE

If there is already a live PENDING invitation for the same pending email and only metadata changes:

```text
do NOT mint a new token
do NOT auto-resend
do NOT extend expires_at
do NOT increment token_version
do NOT reset resend_count
do NOT create a second identity-change row
```

Update only the pending snapshot metadata if the current data model supports it.

Purpose:

```text
later acceptance must not restore stale name/org/title/phone
```

If invitation is already:

```text
EXPIRED
DECLINED
CANCELLED
```

metadata editing must not silently revive it.

Resend remains an explicit action.

---

# 10. PATH B — CHANGED NORMALIZED EMAIL = IDENTITY CHANGE

Condition:

```text
normalizedNewEmail != normalizedCurrentEmail
```

This IS an identity change.

Only this path may:

```text
create INITIAL_CONFIRMATION / replacement
create TRANSFER
mint token
send confirmation email
```

Central rule:

```text
ONLY EMAIL/IDENTITY CHANGE
→ confirmation required
```

Changing only:

```text
name
organization
jobTitle
phone
```

must NOT send confirmation email.

---

# 11. PRE-DECISION EMAIL CHANGE

For the canonical pre-decision state, reuse:

```text
ReplaceOperationalContactCommand
INITIAL_CONFIRMATION
```

Expected:

```text
new email
→ pending confirmation
→ confirmation email to new/pending contact
→ token/action URL
→ existing confirmation lifecycle
```

Preserve the existing pre-decision semantics regarding whether the old relation is cleared and the confirmation gate closes.

Do NOT silently convert pre-decision replacement into TRANSFER semantics.

---

# 12. DECIDED / APPROVED EMAIL CHANGE = TRANSFER

For a decided/approved campus:

```text
A = current confirmed contact
B = requested new contact
```

On transfer initiation:

```text
A remains OperationalContactUserId
A keeps instance rights
B is pending
campus decision remains unchanged
request aggregate remains unchanged
host/schedule remain unchanged
```

Only on B acceptance:

```text
reuse/provision B account
→ atomically bind B
→ B becomes OperationalContactUserId
→ A loses rights that depended only on current-contact relation
```

On:

```text
decline
expiry
cancel
```

A remains current and B is not assigned.

---

# 13. PENDING TRANSFER — CURRENT A VS PENDING B MUST NEVER BE MIXED

Display clearly:

```text
Đầu mối hiện tại
A
[Chỉnh sửa thông tin] [Chuyển giao]

Đang chờ chuyển giao
B
Hết hạn: ...
[Gửi lại] [Hủy]
```

Never combine:

```text
UserId = A
Name/Email = B
```

in one edit model.

## Preferred behavior

Current A:
- can edit A metadata if authorized.

Pending B:
- separate pending block;
- Resend / Cancel;
- no ordinary "edit current contact" action.

If current production flow already allows editing B metadata and removing it would break a relied-upon business flow:

```text
show evidence
→ ASK user before removing it
```

If B metadata editing remains supported, it must update only `pending_snapshot_json` and must not:

```text
create another transfer
mint a token
resend
extend expiry
increment token_version
change email
```

Do not create a second transfer while one is already pending.

---

# 14. `contactFullName` EMAIL VARIABLE

Keep the correction.

Never:

```text
contactFullName = NewEmailNormalized
```

Use:

```text
PendingSnapshotJson.fullName
→ compatible legacy key if required
→ safe neutral fallback such as "Quý Anh/Chị"
```

For TRANSFER:
- do not fall back to outgoing A's snapshot name when rendering B's invitation.

Add/keep tests for:
- INITIAL_CONFIRMATION;
- TRANSFER.

Assert:

```text
contactFullName == actual pending person's name
contactFullName != email address
```

---

# 15. ACCOUNT IDENTITY VS CONTACT SNAPSHOT

The global account and per-instance snapshot are separate.

Core account identity:

```text
normalized verified email
```

Do NOT automatically update account identity/profile merely because a registrant submitted different snapshot metadata.

Rule:

```text
same verified email
+
different name/title/org/phone
→ SAME account identity
→ different contextual snapshot is allowed
```

---

# 16. DO NOT CREATE ACCOUNT WHEN EMAIL IS MERELY TYPED

Required lifecycle:

```text
registrant/contact enters new email
→ pending invitation
→ email ownership is proven by canonical confirmation/SSO
→ lookup user by normalized verified email
→ reuse eligible account OR provision a new eligible account
→ bind UserId to target instance
```

Before successful ownership proof:

```text
NO active account should be created solely because the email was entered
```

If pending confirmation:
- expires;
- is declined;
- is cancelled;

then:

```text
no account is created solely from that email
```

---

# 17. REUSE EXISTING ACCOUNT AFTER SUCCESSFUL CONFIRMATION

After successful email proof:

```text
lookup user by normalized verified email
```

If eligible account exists:

```text
reuse existing UserId
do not create duplicate account
bind existing UserId to target instance
do not overwrite existing account profile automatically
```

Do not silently:

```text
change role
reactivate disabled account
convert internal account to VISITOR
create second account for same email
```

### ASK if ambiguous

If repository currently has conflicting rules about:
- ACTIVE vs inactive;
- VISITOR vs internal staff;
- role compatibility;
- whether internal accounts may be operational contacts;

show exact current evidence and ask before changing eligibility behavior.

---

# 18. PROVISION NEW ACCOUNT ONLY AFTER CONFIRMATION

If no eligible account exists after email ownership proof:

reuse the canonical account / Google SSO / Visitor provisioning mechanism.

Expected external contact role:

```text
VISITOR
```

IF this matches the current canonical role model.

Do not create a parallel account-provisioning implementation inside the contact handler.

---

# 19. NEW ACCOUNT INITIAL PROFILE — DO NOT GUESS FIELD OWNERSHIP

For a newly provisioned account, seed only global-profile fields that the account schema actually owns.

Likely candidates may include:

```text
email
fullName
phone
```

But audit actual schema and usage.

Do not assume:

```text
organization
jobTitle
nationality
phone
```

are all global-account fields.

If exact field ownership is not already canonical:

```text
show User/account schema
show current write/read usage
ASK user which snapshot fields may seed account profile
```

Do not invent profile ownership.

---

# 20. EXISTING ACCOUNT MUST NOT BE SILENTLY OVERWRITTEN

Critical rule:

```text
existing account by same verified email
+
registrant-entered snapshot differs
≠
permission to overwrite account profile
```

A registrant may enter:
- stale data;
- abbreviation;
- translated title;
- context-specific job title;
- old/new phone.

Therefore:

```text
reuse account
→ preserve account profile
→ save contextual instance snapshot
```

---

# 21. SELF-SERVICE ACCOUNT PROFILE SYNC — CONFIRMED REQUIREMENT

This feature is IN SCOPE.

When an existing account holder's current account profile differs from their contact snapshot, offer ONLY to the authenticated holder:

> **Thông tin liên hệ trong yêu cầu này khác hồ sơ PEMS của bạn. Bạn có muốn cập nhật hồ sơ cá nhân không?**

Actions:

```text
Giữ nguyên hồ sơ
Cập nhật hồ sơ cá nhân
```

Rules:

```text
1. Only authenticated account holder may perform profile sync.
2. Registrant cannot sync another person's profile.
3. Staff cannot silently sync it.
4. "Giữ nguyên hồ sơ" changes no global account data.
5. "Cập nhật hồ sơ cá nhân" copies only approved global-profile fields.
6. Current instance snapshot remains contextual data.
7. Historical snapshots in older requests/instances are NEVER rewritten.
8. Profile sync must not trigger contact confirmation.
9. Profile sync must not invoke registration 72h.
```

### ASK REQUIRED

Audit global account fields first.

If unclear which fields may be copied from snapshot into account profile, STOP and ASK.

---

# 22. OPERATIONAL CONTACT BECOMES A REAL AUTHENTICATED INSTANCE ACTOR

After successful confirmation/account binding:

```text
currentUser.UserId == targetInstance.OperationalContactUserId
```

becomes an authorization basis.

Do NOT grant operational-contact permissions merely because:

```text
Role == VISITOR
```

A random VISITOR must not gain access.

Prefer one shared authorization guard/helper, for example:

```text
IsCurrentOperationalContact(targetInstance, currentUser)
```

or equivalent.

Avoid duplicated raw comparisons across handlers.

---

# 23. CONFIRMED OPERATIONAL CONTACT RIGHTS

The following rights are FINAL and MUST be implemented for the assigned instance:

```text
1. View assigned instance.
2. View relevant status/history/rejection information.
3. Edit allowed instance-local information.
4. Resubmit / gửi lại after Reject.
5. Send feedback / response.
6. Create Amendment after approval.
7. View / preview / download files of the assigned instance.
8. Manage/update own operational-contact metadata for that instance.
9. Initiate contact transfer.
10. Resend pending transfer invitation.
11. Cancel pending transfer.
```

Do NOT ask again whether these rights should exist.

The only remaining questions may concern how to scope a current request-wide implementation without leaking sibling/request-level side effects.

---

# 24. RIGHTS NOT AUTOMATICALLY GRANTED

Operational Contact does NOT automatically receive:

```text
whole-request ownership
add campus
remove campus
edit sibling campus
change registrant identity
approve/reject as Staff Leader
manage unrelated accounts
cancel whole multi-campus request
```

Do not add these unless explicitly decided later.

---

# 25. REQUEST-LEVEL VS INSTANCE-LEVEL BOUNDARY

Conceptual rule:

```text
REQUEST-LEVEL
→ registrant/request owner unless explicitly confirmed otherwise

INSTANCE-LOCAL
→ registrant OR assigned current OperationalContactUserId
```

Example:

```text
Request VR-2003

HN instance 3103 → contact User 501
DN instance 3104 → contact User 700
```

User 501:
- may use confirmed instance rights on HN;
- must not mutate DN.

User 700:
- symmetrical DN scope.

Random VISITOR:
- no instance rights merely from role.

---

# 26. MANDATORY ACTION-BY-ACTION AUDIT

Before broad authorization changes, classify every existing Visitor action:

```text
INSTANCE_LOCAL
REQUEST_LEVEL
CROSS_INSTANCE
IDENTITY_ONLY
UNCLEAR
```

At minimum audit:

```text
View detail
View history
View rejection reason
Edit
Resubmit
Feedback/respond
Create Amendment
View Amendment
Cancel Amendment
Guest list changes
Support personnel changes
Schedule changes
Additional requirements
File preview
File download
File upload
Contact management
Transfer
Resend
Cancel transfer
Cancel visit/request
Add campus
Remove campus
Registrant identity change
Request-level notes
Other Visitor CTAs currently shown in Detail/History
```

For any `UNCLEAR` action:

```text
show exact current code semantics
→ ASK user
```

Do not guess.

---

# 27. CONFIRMED RIGHT — VIEW ASSIGNED INSTANCE

Assigned Operational Contact can view their target instance and relevant status/history/rejection information.

Backend authorization must be enforced.

If the existing endpoint returns the whole multi-campus request, audit for sibling-data leakage.

If safe filtering can be implemented from existing scope, do it.

If choosing between:
- filtered whole-request response;
- dedicated instance-detail route;
- another access model;

would change API semantics and no canonical pattern exists:

```text
show evidence
→ ASK
```

---

# 28. CONFIRMED RIGHT — EDIT INSTANCE-LOCAL DATA

Assigned Operational Contact can edit allowed data belonging to their instance.

Classify each editable field:

```text
INSTANCE_LOCAL
REQUEST_SHARED
IDENTITY_ONLY
UNCLEAR
```

Grant mutation only for confirmed instance-local fields.

Never let HN contact mutate:
- DN/HCM;
- request owner;
- shared fields affecting siblings without explicit business rule.

If field scope is unclear, ASK.

---

# 29. CONFIRMED RIGHT — RESUBMIT AFTER REJECT

Operational Contact MUST be able to resubmit their own rejected instance.

Required behavior:

```text
HN rejected
HN OperationalContact edits allowed HN data
HN OperationalContact submits again
→ only HN re-enters review
→ sibling campus states remain untouched
```

Because this is Resubmit-after-Reject:

```text
target plannedStart >= serverNow + 72h
```

must be validated according to the canonical registration rule.

## Critical implementation rule

If current Resubmit is request-wide:

```text
DO NOT simply authorize contact to call the existing whole-request command
```

Refactor/add an instance-scoped path.

If current aggregate/state model cannot represent instance-only Resubmit without introducing a new business state:

```text
show exact evidence
→ ASK user before inventing that state
```

---

# 30. CONFIRMED RIGHT — FEEDBACK / RESPONSE

Assigned Operational Contact MUST be able to send feedback/respond for their target instance.

If current Feedback is instance-local:
- reuse it.

If current Feedback writes request-wide data or affects siblings:

```text
do not accidentally grant cross-instance mutation
show exact current storage/handler behavior
ASK how request-wide feedback should be scoped
```

The permission itself is confirmed; only unresolved current data scope may be asked.

---

# 31. CONFIRMED RIGHT — CREATE AMENDMENT AFTER APPROVAL

Assigned Operational Contact MUST be able to create an Amendment for their assigned approved instance.

Required:

```text
target instance is assigned to current user
target instance is Amendment-eligible
→ allow Amendment creation for THAT instance
```

Registration 72h MUST NOT apply.

Use the existing Amendment cutoff/policy, e.g. the existing action cutoff if canonical.

Do not permit:
- sibling-campus Amendment;
- accidental whole-request mutation.

If current Amendment model is structurally request-wide and cannot target one instance:

```text
show exact model/handler/schema
→ ASK before changing the data model
```

Do not revoke the confirmed permission; resolve implementation design with the user.

---

# 32. CONFIRMED RIGHT — VIEW / PREVIEW / DOWNLOAD INSTANCE FILES

Assigned Operational Contact MUST be able to access files belonging to their target instance.

Authorization chain should resolve:

```text
file
→ owning business object
→ visitInstanceId
→ currentUser == OperationalContactUserId
```

Do NOT authorize based only on:
- role VISITOR;
- knowledge of fileId;
- belonging to same request.

Sibling-campus files remain denied.

### Shared-file ambiguity

If a file is genuinely request-wide/shared and cannot be mapped to one instance:

```text
show exact ownership/data evidence
→ ASK whether assigned contacts should access that shared file
```

Do not guess.

---

# 33. CONFIRMED RIGHT — INITIATE CONTACT TRANSFER

Current Operational Contact A can initiate transfer for the instance they currently own.

Required:

```text
A current
B pending
→ A keeps current rights
→ B has no operational-contact rights yet
```

On B accept:

```text
reuse/provision B account
bind B
rights move A → B
```

On decline/expiry/cancel:
- A remains current;
- B receives no contact rights.

---

# 34. CONFIRMED RIGHT — RESEND / CANCEL PENDING TRANSFER

Current contact A can:

```text
Resend transfer invitation
Cancel pending transfer
```

only for their current assigned instance.

Resend preserves existing canonical:
- cooldown;
- resend cap;
- token version;
- expiry policy.

Cancel:

```text
pending B → CANCELLED
A remains current
```

No sibling effect.

---

# 35. CONTACT INVITATION EXPIRY

Keep invitation expiry separate from visit scheduling.

Canonical/current policy may be:

```text
INITIAL_CONFIRMATION = 72h
TRANSFER = 24h
```

These are invitation lifetimes, NOT registration lead-time rules.

On expiry:

```text
invitation → EXPIRED
token invalid
no account created merely from pending email
pending contact not bound
TRANSFER keeps current A
expiry notification sent/recovered
```

---

# 36. VERIFIED REGISTRANT SELF-MATCH

Preserve existing canonical immediate self-match if code currently requires:

```text
new email == verified registrant email
RegistrantUserId != null
EmailVerifiedAt != null
```

If identity is already proven under the canonical flow:
- no extra confirmation needed.

If repository contains competing self-match rules:
- show evidence;
- ASK before changing.

---

# 37. REJECT EMAIL RECOVERY MUST USE THE EXACT REJECTION BUSINESS EVENT

Do NOT define "already notified" as:

```text
exists SENT email for:
(templateCode, visitInstanceId)
```

Same campus can be rejected multiple times.

Example:

```text
Campus 3103

Reject #1
→ Business Event E100
→ email SENT

Resubmit

Reject #2
→ Business Event E205
→ email FAILS
```

Old SENT for E100 must NOT suppress E205 recovery.

Use immutable rejection-event identity.

Prefer existing sources in this order:

```text
1. rejection event row ID
2. audit_log ID for exact rejection
3. immutable decision/rejection revision
4. another persisted immutable business-event identity
```

Do not add a new table/column until existing event identities are proven insufficient.

Conceptual email identity:

```text
Reject #1 → E100 → notification E100
Reject #2 → E205 → notification E205
```

If current email history supports `related_type` / `related_id`, prefer linking to the event rather than only the campus object.

---

# 38. MANDATORY REPEATED-REJECT TEST

Test:

```text
Reject #1
→ email #1 SENT

Resubmit target instance

Reject #2
→ first email attempt FAILS
```

Expected:

```text
Reject #2 remains independently recoverable
Reject #1 SENT cannot suppress Reject #2
recovery retries only E205 when safe
```

After E205 succeeds:

```text
later sweep
→ no duplicate
```

Also test:
- HN and DN rejection events independently.

---

# 39. CONTACT-EXPIRY RECOVERY MUST BE EVENT-BASED

Expiry state transition:

```text
PENDING
→ EXPIRED
→ token invalid
→ commit
→ notify registrant/initiator
```

If email fails after commit:

```text
do not revert EXPIRED to PENDING
do not rely on a PENDING-only sweep to retry notification
```

Use the identity-change/expiry event as the durable notification identity.

One expiry business event should produce at most one successful notification.

---

# 40. EMAIL DELIVERY OUTCOME CLASSIFICATION

The recovery system must distinguish whether automatic retry is actually safe.

Required semantic classes:

```text
A. PROVEN_NOT_DISPATCHED
   Outbound provider call definitely did not happen.
   Automatic retry is allowed.

B. CONFIGURATION / RENDER FAILURE BEFORE OUTBOUND
   Template/config/variable failure occurred before outbound send.
   Controlled retry is allowed after correction.

C. SENT / PROVIDER_ACCEPTED
   Provider acceptance is known.
   Complete. Never retry automatically.

D. OUTCOME_UNKNOWN
   Outbound call may have reached the provider,
   but provider acceptance cannot be proven.
   Automatic resend is NOT allowed.
```

Do NOT claim exactly-once SMTP delivery.

---

# 41. AUDIT CURRENT DISPATCHER RESULT PATHS

For each actual current outcome/error path, report:

```text
EmailDeliveryStatus.Sent
EmailDeliveryStatus.Failed
EmailDeliveryStatus.Skipped
SMTP_DISABLED
SMTP_MISCONFIGURED
SMTP_SEND_FAILED
template missing
variable missing
renderer exception
provider timeout
process crash leaving QUEUED
other current provider result
```

For each answer:

```text
Was outbound attempted?
Can provider acceptance be ruled out?
Auto-retry safe? YES/NO
```

Do not classify all `FAILED` as retryable.

Do not classify all `QUEUED` as retryable.

A stale QUEUED row after outbound may be ambiguous.

---

# 42. OUTCOME_UNKNOWN MUST NEVER AUTO-RESEND

Crash-window example:

```text
DB records QUEUED
→ SMTP provider receives message
→ process crashes before DB records SENT
```

The recipient may already have the email.

Therefore:

```text
OUTCOME_UNKNOWN
→ no automatic retry
→ operator/manual decision
```

Do not treat:

```text
no SENT row
```

as proof that no mail left the system.

---

# 43. RETRY CAP / SCAN WINDOW MUST NOT MAKE NOTIFICATIONS DISAPPEAR

If current policy uses:

```text
max attempts = 5
scan window = 7 days
```

do NOT silently stop and lose visibility.

When automatic retry stops because of:
- retry cap reached;
- event aged beyond auto-scan;
- OUTCOME_UNKNOWN;
- permanent configuration problem;

leave an observable durable/derived condition such as:

```text
RETRY_EXHAUSTED
NEEDS_ATTENTION
OUTCOME_UNKNOWN
FAILED_PERMANENT
```

or an equivalent state derived from existing durable email records.

Do not add schema unless necessary.

---

# 44. OPERATOR / MANUAL RECOVERY

Document a real recovery path:

```text
1. identify affected business event
2. see why automatic retry stopped
3. see previous attempts
4. determine if previous dispatch was definitely not delivered
5. retry safely if proven safe
6. handle OUTCOME_UNKNOWN without blind automatic resend
```

An admin endpoint, CLI, runbook, or existing operational surface may be acceptable.

### ASK REQUIRED

If the project has no canonical operations pattern and implementation requires choosing between:
- new Admin UI;
- Admin endpoint;
- CLI;
- manual DB/runbook;

show options and ASK before adding a new management surface.

---

# 45. RETRY BACKOFF

Do not retry every maintenance tick.

Reuse existing timestamps where possible:

```text
last_attempt_at
created_at
updated_at
sent_at
```

Implement/reuse bounded backoff.

Avoid adding fields if existing durable data is enough.

---

# 46. EMAIL RECOVERY CONCURRENCY

Recovery must be safe with:

```text
two app instances
two hosted workers
manual retry racing automatic retry
```

Required property:

```text
same business event
→ at most one active automatic outbound attempt
```

Use DB-backed locking/idempotency.

Do not rely only on in-memory locks.

---

# 47. POST-COMMIT API CONSISTENCY

Correct order:

```text
business mutation
→ DB commit
→ email notification
```

Never send email before the business commit.

But if notification fails AFTER commit:

```text
business mutation remains successful
notification failure is recorded/recovered separately
```

Example:

```text
campus REJECTED committed
template/render error occurs
```

Do NOT return a misleading generic mutation failure as if Reject rolled back.

Same principle for:
- contact expiry;
- other post-commit notification flows.

---

# 48. REGISTRATION 72H BOUNDARY

Registration lead-time 72h applies ONLY to:

```text
1. Create Visit Request
2. PRE-APPROVAL Edit submission
3. Resubmit after Reject
```

It does NOT apply to:

```text
Approved Amendment
Operational Contact metadata update
Operational Contact account provisioning
Self-service profile sync
INITIAL_CONFIRMATION
TRANSFER
Accept
Decline
Resend
Cancel transfer
Contact invitation expiry
Email recovery
Passive time passage
```

Operational Contact Resubmit after Reject **does use 72h**, because it is Resubmit.

Operational Contact Amendment after approval **does NOT use registration 72h**.

Keep Amendment's existing canonical cutoff/policy.

---

# 49. CAMPUS MAX MUST REMAIN ONE SOURCE OF TRUTH

Create and Edit must use the same helper/formula.

Do not allow divergence such as:

```text
Create:
Math.min(activeCampuses.length, V2_MAX_CAMPUSES)

Edit:
activeCampuses.length
```

unless that is actually the shared canonical helper.

The goal is:

```text
one source of truth
```

not merely removing a hardcoded `/10`.

---

# 50. CONTROLLED CAMPUS SELECT MUST REMAIN FIXED

Keep campus selection controlled.

Required:

```text
displayed selected campus
==
form state campusId
==
submitted payload campusId
```

Do not reintroduce uncontrolled `register()` select behavior that drifts after async options load.

---

# 51. SINGLE TOAST OWNERSHIP MUST REMAIN FIXED

Edit success:

```text
exactly one toast
```

Resubmit success:

```text
exactly one toast
```

React StrictMode mount/cleanup/mount must not duplicate flash.

Refresh/back-forward must not replay stale success.

---

# 52. MANDATORY ACCOUNT TESTS

## ACCOUNT-01 — typing email does not create account

```text
new contact email entered
invitation pending
```

Expected:
- no active user account created solely from entered email.

## ACCOUNT-02 — new account after successful confirmation

```text
confirmed email has no eligible account
```

Expected:
- canonical provisioning;
- no duplicate;
- bind UserId to target instance.

## ACCOUNT-03 — existing eligible account

Expected:
- reuse UserId;
- do not create duplicate.

## ACCOUNT-04 — same email, different contextual metadata

Expected:
- same UserId;
- instance snapshot stores contextual values;
- global account profile not auto-overwritten.

## ACCOUNT-05 — incompatible/inactive account

Expected:
- preserve canonical eligibility;
- no silent role conversion/reactivation;
- if canonical eligibility is ambiguous, mark blocked and ASK.

## ACCOUNT-06 — decline/expiry/cancel

Expected:
- no account created solely from pending email.

---

# 53. MANDATORY PROFILE-SYNC TESTS

For an existing account whose snapshot differs:

Expected prompt:

```text
Thông tin liên hệ trong yêu cầu này khác hồ sơ PEMS của bạn.
Bạn có muốn cập nhật hồ sơ cá nhân không?
```

Test:

```text
Giữ nguyên hồ sơ
→ account unchanged
→ snapshot unchanged
```

Test:

```text
Cập nhật hồ sơ cá nhân
→ only authenticated holder allowed
→ only approved global-profile fields copied
→ snapshot remains contextual
→ historical snapshots untouched
```

Unauthorized:
- registrant cannot sync another person's account;
- unrelated Visitor cannot;
- Staff cannot silently do it.

---

# 54. MANDATORY INSTANCE AUTHORIZATION TEST MATRIX

Use at least this setup:

```text
Request R

HN instance → contact A
DN instance → contact B
Random Visitor C
Registrant R-owner
```

Verify backend, not only UI:

| Action | Registrant | Contact A on HN | Contact B on DN | Random Visitor |
|---|---:|---:|---:|---:|
| View HN | existing policy | ALLOW | DENY unless separately assigned | DENY |
| Edit HN instance-local fields | existing policy | ALLOW | DENY | DENY |
| Resubmit HN | existing policy | ALLOW | DENY | DENY |
| Feedback HN | existing policy | ALLOW | DENY | DENY |
| Amendment HN | existing policy | ALLOW | DENY | DENY |
| HN files | existing policy | ALLOW | DENY | DENY |
| Transfer HN contact | existing policy | ALLOW if current | DENY | DENY |
| Resend/Cancel HN transfer | existing policy | ALLOW if current | DENY | DENY |
| Mutate DN by A | existing policy | DENY | — | DENY |
| Add/remove campus | existing policy | DENY | DENY | DENY |
| Approve/Reject | role policy | DENY | DENY | DENY |

---

# 55. MANDATORY RESUBMIT TESTS

## RESUBMIT-CONTACT-01

```text
HN rejected
A is current HN contact
A edits allowed HN data
A resubmits
```

Expected:
- HN re-enters canonical review;
- DN/HCM unchanged.

## RESUBMIT-CONTACT-02

HN starts in less than 72h.

Expected:
- resubmit blocked by 72h rule.

## RESUBMIT-CONTACT-03

Sibling contact/random Visitor attempts HN resubmit.

Expected:
- denied.

---

# 56. MANDATORY AMENDMENT TESTS

```text
HN approved
A = HN current contact
```

Expected:
- A may create HN-targeted Amendment;
- registration 72h does NOT block it;
- canonical Amendment cutoff still applies;
- DN/HCM not affected.

Sibling/random Visitor:
- denied.

---

# 57. MANDATORY FILE AUTHORIZATION TESTS

Test:

```text
A current contact for HN
→ preview/download HN-owned file = allowed
→ DN-owned file = denied
```

Random Visitor:
- denied.

Direct fileId access must not bypass business ownership.

If shared/request-wide file exists:
- mark test/decision blocked until user answers ownership if code does not already define it.

---

# 58. MANDATORY TRANSFER TESTS

## TRANSFER-01

A current, B pending.

Expected:
- A remains `OperationalContactUserId`;
- A keeps rights;
- B has no instance operational rights.

## TRANSFER-02

B accepts.

Expected:
- B account reused/provisioned;
- B bound;
- rights transfer A → B.

## TRANSFER-03

B declines/expires/cancels.

Expected:
- A remains current;
- B not assigned.

## TRANSFER-04

A edits metadata while B pending.

Expected:
- A metadata updated;
- B invitation unchanged;
- token unchanged;
- version unchanged;
- expiry unchanged;
- no new transfer;
- no mail.

## TRANSFER-05

Resend/Cancel authorization:
- only current contact/other canonical authorized actor;
- sibling/random Visitor denied.

---

# 59. MANDATORY EMAIL RECOVERY TESTS

## REJECT-RECOVERY-01

Reject #1 → SENT.

## REJECT-RECOVERY-02

```text
Reject #1 SENT
Resubmit
Reject #2 first attempt FAILED
```

Expected:
- #2 remains independently recoverable.

## REJECT-RECOVERY-03

After recovery #2 success:
- another sweep sends no duplicate.

## REJECT-RECOVERY-04

HN and DN rejection events:
- independent identities.

## EMAIL-AMBIGUOUS-01

Provider may have accepted, DB outcome uncertain.

Expected:
- OUTCOME_UNKNOWN;
- no automatic resend.

## EMAIL-SAFE-RETRY-01

Failure proven before outbound.

Expected:
- automatic retry allowed.

## EMAIL-EXHAUSTION-01

Automatic retry cap reached.

Expected:
- unresolved notification remains visible/operable;
- not silently forgotten.

## EXPIRY-RECOVERY-01

Expiry business transition committed, first email fails.

Expected:
- invitation remains EXPIRED;
- token invalid;
- notification remains recoverable if safe;
- no revert to PENDING.

---

# 60. STOP-AND-ASK DECISION POINTS THAT REMAIN

Do NOT ask again whether Operational Contact should have:
- View;
- Edit instance-local data;
- Resubmit;
- Feedback;
- Amendment;
- File access;
- Transfer;
- Resend/Cancel transfer.

Those are CONFIRMED.

Only ask if code audit leaves one of these unresolved:

```text
1. Which existing account roles/statuses are eligible as Operational Contact?
2. Which contact snapshot fields are global-account profile fields?
3. Which fields may be copied during self-service profile sync?
4. A specific Edit field is unclear whether instance-local or request-shared.
5. Existing Resubmit is whole-request and instance-only state cannot be represented without a new state/model.
6. Existing Feedback is request-wide and cannot be safely scoped.
7. Existing Amendment is request-wide and cannot target one instance.
8. A file is truly request-wide/shared and instance ownership is undefined.
9. Pending B metadata editing conflicts with an existing relied-upon workflow.
10. Operator recovery requires a new management surface with no canonical project pattern.
11. Any proposed new table/column/schema change.
12. Any action unexpectedly mutates sibling campuses/request aggregate.
13. Existing verified-self-match rules conflict.
14. Any new account provisioning path conflicts with current SSO/account activation rules.
```

When asking:
- cite exact files/functions/schema;
- explain why current code cannot satisfy the confirmed requirement safely;
- give concrete options;
- wait for user answer before implementing that branch.

---

# 61. DO NOT DO THESE

```text
- do not guess unclear business semantics
- do not authorize by VISITOR role alone
- do not give all Visitor accounts Operational Contact rights
- do not let HN contact mutate DN/HCM
- do not grant whole-request ownership to contact
- do not put existing-contact editing back into Visit Request Edit
- do not send confirmation for metadata-only change
- do not change OperationalContactUserId on same-email metadata update
- do not overwrite existing account profile from registrant snapshot
- do not create account before email ownership proof
- do not create duplicate account for same normalized verified email
- do not silently convert internal/incompatible account to VISITOR
- do not sync account profile without holder consent
- do not rewrite historical snapshots after profile sync
- do not remove A when TRANSFER merely starts
- do not confuse A current with B pending
- do not create second transfer while one is pending
- do not apply registration 72h to Amendment
- do not apply registration 72h to contact/profile/account actions
- do not dedupe Reject by visitInstanceId alone
- do not use old SENT Reject to suppress a later Reject
- do not auto-retry every FAILED email
- do not auto-retry every QUEUED email
- do not auto-retry OUTCOME_UNKNOWN
- do not claim exactly-once SMTP delivery
- do not revert REJECTED/EXPIRED to trigger mail
- do not silently abandon notification after 5 attempts/7 days
- do not return false business failure after a post-commit mail failure
- do not create a parallel SMTP sender bypassing canonical dispatcher
- do not create a new table/column before proving existing infrastructure is insufficient
```

---

# 62. SUGGESTED IMPLEMENTATION ORDER

```text
PHASE A
Preflight + preserve WIP

PHASE B
Audit current account provisioning / eligibility / profile fields

PHASE C
Finalize Visit Edit vs Detail contact UX + tooltip

PHASE D
Lock same-email metadata-only update

PHASE E
Lock changed-email INITIAL_CONFIRMATION / TRANSFER

PHASE F
Implement/reuse account lookup + post-confirmation provision/reuse

PHASE G
Implement self-service account-profile comparison/sync

PHASE H
Create/reuse canonical OperationalContact instance authorization guard

PHASE I
Audit action-by-action instance/request data scope

PHASE J
Implement instance-scoped View

PHASE K
Implement instance-scoped Edit

PHASE L
Implement instance-scoped Resubmit after Reject + 72h

PHASE M
Implement instance-scoped Feedback/Response

PHASE N
Implement instance-scoped Amendment after approval

PHASE O
Implement instance-scoped file preview/download

PHASE P
Lock Transfer / Resend / Cancel authorization and A/B UI

PHASE Q
Finish Reject recovery using exact business-event identity

PHASE R
Finish contact-expiry recovery

PHASE S
Classify SMTP outcomes + block OUTCOME_UNKNOWN auto-retry

PHASE T
Add retry exhaustion/operator recovery/backoff/concurrency

PHASE U
Verify post-commit API behavior

PHASE V
Run full regression gates
```

---

# 63. REQUIRED FINAL REPORT FORMAT

## 1. Preflight

```text
Branch:
Start HEAD:
End HEAD:
Modified/untracked WIP preserved:
Stashes untouched:
```

## 2. Confirmed vs Asked decisions

```text
Confirmed directly by this prompt:
...

Ambiguities found in code and asked to user:
...
```

Do not hide inferred decisions.

## 3. Visit Edit / Detail UX

```text
Existing campus Edit:
New campus Edit:
Detail contact management:
Tooltip:
Removed permanent text:
```

## 4. Contact save matrix

| State | Email changed? | Behavior | Confirmation mail? | UserId change? |
|---|---:|---|---:|---:|
| current contact | No | metadata snapshot update | No | No |
| pre-decision | Yes | INITIAL_CONFIRMATION | Yes | canonical acceptance |
| decided | Yes | TRANSFER | Yes | only on accept |

## 5. Account provisioning

```text
When account lookup occurs:
Lookup key:
Eligible existing account behavior:
New account behavior:
Role/status:
Fields seeded:
No-account-before-confirmation proof:
```

## 6. Account profile sync

```text
Difference detection:
Prompt:
Authorized actor:
Fields copied:
Keep-profile behavior:
Historical snapshot behavior:
```

## 7. Instance authorization matrix

Report exact behavior for:
- registrant;
- assigned current contact;
- sibling contact;
- random VISITOR;
- relevant read-only/internal roles.

## 8. Resubmit

```text
Target instance:
Sibling effects:
72h:
Status transition:
Authorization:
```

## 9. Feedback

```text
Storage scope:
Authorization:
Sibling isolation:
```

## 10. Amendment

```text
Target instance:
Authorization:
Registration 72h exclusion:
Canonical Amendment cutoff:
Sibling isolation:
```

## 11. File access

```text
Ownership resolution:
Preview:
Download:
Sibling denial:
Shared-file decisions:
```

## 12. Transfer

```text
Current A:
Pending B:
Resend:
Cancel:
Accept:
Rights handover:
Metadata behavior:
```

## 13. Reject / Expiry recovery

```text
Reject event identity:
Expiry event identity:
RelatedType/RelatedId or equivalent:
Repeated Reject behavior:
```

## 14. Delivery outcome classification

| Outcome | Outbound attempted? | Acceptance ambiguous? | Auto retry? |
|---|---:|---:|---:|
| ... | ... | ... | ... |

## 15. Retry policy

```text
Attempt cap:
Backoff:
Scan window:
OUTCOME_UNKNOWN:
RETRY_EXHAUSTED:
Operator recovery:
Concurrency:
```

## 16. Post-commit semantics

```text
Reject:
Expiry:
Contact mutation:
```

## 17. Changed files

List file + reason.

## 18. Tests

List all new/updated tests and counts.

## 19. Gates

Run and report:

```text
dotnet build
backend unit tests
architecture tests
VisitRequests integration tests
Emails integration tests
frontend typecheck
frontend unit tests
frontend build
```

Known unrelated pre-existing test failures must be proven against clean baseline rather than casually labeled pre-existing.

## 20. Remaining BLOCKED decisions / real debt

Only actual unresolved items.

---

# 64. DEFINITION OF DONE

## Contact-management separation
- [ ] Existing Operational Contact is not editable in Visit Request Edit.
- [ ] Newly added campus can still collect initial contact if required.
- [ ] Detail View owns existing-contact management.
- [ ] Long instruction text moved to `?` tooltip.
- [ ] Backend Edit/Resubmit contact immutability remains enforced.

## Contact metadata / identity
- [ ] Same normalized email updates snapshot only.
- [ ] Same-email update sends no confirmation mail.
- [ ] Same-email update creates no token/identity change.
- [ ] Pending same-email metadata update does not reissue/restart invitation.
- [ ] Changed email uses canonical INITIAL_CONFIRMATION / TRANSFER.
- [ ] A remains current until B accepts.
- [ ] Pending A/B identities are never mixed.
- [ ] `contactFullName` is actual pending person's name.

## Account provisioning
- [ ] Account is not created merely because email was typed.
- [ ] Email ownership is proven before account provision/binding.
- [ ] Existing eligible account is reused by normalized verified email.
- [ ] No duplicate account for same email.
- [ ] Existing account is not silently role-converted/reactivated.
- [ ] Existing global profile is not silently overwritten by registrant snapshot.
- [ ] New account uses canonical provisioning path.

## Profile sync
- [ ] Existing holder sees explicit profile-difference prompt where applicable.
- [ ] Only holder can choose to update their profile.
- [ ] Keep-profile path changes nothing.
- [ ] Update-profile copies only confirmed global-profile fields.
- [ ] Historical snapshots are never rewritten.

## Instance rights
- [ ] Authorization uses assigned `OperationalContactUserId`, not VISITOR role alone.
- [ ] Assigned contact can View own instance.
- [ ] Assigned contact can Edit instance-local data.
- [ ] Assigned contact can Resubmit own rejected instance.
- [ ] Contact Resubmit validates 72h.
- [ ] Resubmit does not reset sibling campuses.
- [ ] Assigned contact can Feedback/Respond for own instance.
- [ ] Assigned contact can create Amendment after approval for own instance.
- [ ] Contact Amendment is NOT subject to registration 72h.
- [ ] Assigned contact can preview/download own instance files.
- [ ] Sibling files remain unauthorized.
- [ ] Current contact can initiate Transfer.
- [ ] Current contact can Resend/Cancel own pending transfer.
- [ ] Pending target gets rights only after acceptance/binding.
- [ ] Random VISITOR has none of these rights merely from role.

## Recovery
- [ ] Reject recovery is keyed by exact rejection business event.
- [ ] Old Reject SENT cannot suppress a later Reject notification.
- [ ] Contact-expiry recovery does not depend on PENDING-only scan.
- [ ] Proven pre-dispatch failures can retry.
- [ ] SENT/provider-accepted never auto-retries.
- [ ] OUTCOME_UNKNOWN never auto-retries.
- [ ] Retry exhaustion does not silently disappear.
- [ ] Operator recovery path exists.
- [ ] Recovery has backoff.
- [ ] Recovery is DB-concurrency-safe.

## Post-commit consistency
- [ ] Business state commits before notification.
- [ ] Notification failure does not falsely make committed business mutation look rolled back.
- [ ] Notification failure remains observable/recoverable.

## Previous fixes
- [ ] Create/Edit campus max still same source-of-truth.
- [ ] Campus select remains controlled.
- [ ] Edit success toast exactly once.
- [ ] Resubmit success toast exactly once.
- [ ] Registration 72h remains Create / PRE-APPROVAL Edit / Resubmit only.
- [ ] Approved Amendment remains outside registration 72h.
- [ ] Passive `<72h` still does nothing automatically.

---

# 65. FINAL PRINCIPLE

The final architecture should read conceptually as:

```text
Registrant
└── owns the Visit Request

Operational Contact
└── authenticated co-operator of ONLY assigned Visit Instance(s)
    ├── View
    ├── Edit instance-local data
    ├── Resubmit after Reject
    ├── Feedback / Respond
    ├── Create Amendment after approval
    ├── View / Preview / Download instance files
    ├── Manage own contact metadata
    ├── Transfer role
    └── Resend / Cancel transfer

Global Account
└── identity of the real person
    └── normalized verified email

Operational Contact Snapshot
└── contextual information for one instance
    └── must not silently overwrite the global account

Email Recovery
└── keyed by exact business event
    ├── safe failures may retry
    ├── OUTCOME_UNKNOWN never auto-resends
    └── exhausted notifications remain visible/recoverable
```

If current code cannot satisfy any confirmed behavior without a genuine new business-model decision, stop that subtask and ask with evidence. Never silently invent the missing rule.
