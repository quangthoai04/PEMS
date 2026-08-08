# PEMS — Repair Prompt v3
## Separate Visit Request Edit from Operational Contact Management + Fix Remaining Logic

### Target
- Repository: PEMS
- Working branch: `Cảnh-Iter1`
- Treat current `Cảnh-Iter1` / `Dev` baseline as equivalent for this task.
- Work on the current local working tree and PRESERVE all uncommitted WIP from the previous repair.
- Do not reset, checkout-over, stash-drop, or replace existing work.

---

# 0. Goal

Fix the current implementation so that **Visit Request editing** and **Operational Contact management** are two independent workflows.

```text
EDIT VISIT REQUEST
→ edits request/business/schedule/guest/support data
→ DOES NOT edit operational-contact data
→ DOES NOT show "Thay đổi đầu mối" inside the request edit form

VIEW VISIT REQUEST
→ shows operational-contact card
→ contains "Chỉnh sửa đầu mối" / "Thay đổi đầu mối" action
→ contact management happens here, scoped to ONE campus
```

Operational-contact update rule:

```text
Same normalized email
→ update contact profile/snapshot only
→ NO confirmation token
→ NO confirmation email
→ NO replacement/transfer
→ NO 72h validation

Different normalized email
→ identity is changing
→ use canonical INITIAL_CLAIM / TRANSFER confirmation workflow
→ send confirmation email to the pending/new contact
→ only canonical acceptance rules may apply the new identity
```

Do not mix this with Approved Amendment.
Do not mix contact confirmation expiry with the 72-hour visit-registration lead-time rule.

---

# 1. Preflight

Before changing code:

1. Confirm branch = `Cảnh-Iter1`.
2. Record HEAD SHA, git status, and existing modified/untracked files.
3. Preserve all current WIP from the previous repair.
4. Do not overwrite the already-added fixes for:
   - dynamic campus limit;
   - controlled campus select;
   - duplicate toast;
   - 72h Create / PRE-APPROVAL Edit / Resubmit;
   - Reject email;
   - operational-contact expiry email;
   - email templates / SQL patch / tests.
5. Audit the actual current working tree, not only committed HEAD.

---

# 2. Core separation: Request Edit must not edit Operational Contact

## 2.1 Frontend

In `EditVisitRequestV2Page`, `CampusVisitCard`, or equivalent edit-only components, remove operational-contact editing from the Visit Request edit workflow.

The edit screen must NOT contain editable fields for:

```text
operationalContactFullName
operationalContactOrganization
operationalContactJobTitle
operationalContactPhone
operationalContactEmail
```

and must NOT contain `Thay đổi đầu mối` inside the request edit form.

Preferred UI:
- either hide the whole operational-contact edit block;
- or show a compact READ-ONLY summary only if useful, with no input and no contact-management button.

Do NOT show a disabled email input next to editable name/phone fields. That still mixes two workflows.

## 2.2 Backend defense-in-depth

Request Edit / Resubmit APIs must not be usable to mutate operational-contact data.

Keep `IMMUTABLE_CONTACT_IDENTITY`.

Strengthen the boundary if necessary so PRE-APPROVAL Edit / Resubmit cannot change:
- contact full name;
- organization;
- job title;
- phone;
- email;
- `operational_contact_user_id`;
- contact confirmation relation/source.

If the DTO still transports a contact snapshot for compatibility, compare it to the persisted contact snapshot and reject mutations through the Request Edit path with a stable error.

Do NOT silently accept contact metadata changes through request edit just because email stayed unchanged.

---

# 3. Move contact-management action to View/Detail

In the Visit Request detail view, for each campus operational-contact card, add:

```text
Chỉnh sửa đầu mối
```

or keep established wording `Thay đổi đầu mối`.

It must open a dedicated contact-management modal/form, not navigate to Visit Request Edit.

Scope is ONE campus:

```text
visitRequestId
visitInstanceId
```

For multi-campus requests, editing one campus must not affect sibling campuses.

Respect existing role/scope guards. Do not show the action to read-only roles if current permission rules say read-only.

---

# 4. Dedicated contact-management form

Fields:

```text
Họ và tên
Đơn vị công tác
Chức vụ
Số điện thoại
Email
```

Initial values come from the selected campus's current operational-contact snapshot.

On Save, normalize the email and compare with persisted/current email.

There are exactly TWO semantic paths.

---

# 5. Path A — Email UNCHANGED: profile update only

Condition:

```text
Normalize(newEmail) == Normalize(currentEmail)
```

Then this is NOT an identity change.

Allowed changes:
- full name;
- organization;
- job title;
- phone.

Expected:

```text
update operational-contact snapshot
→ commit
→ audit changes
→ return success
```

Must NOT:
- create `VisitRequestIdentityChange`;
- create `EmailActionToken`;
- send confirmation email;
- send transfer email;
- change `OperationalContactUserId`;
- clear confirmation;
- change confirmation source;
- set `WAITING_CONTACT_CONFIRMATION`;
- reopen the global contact gate;
- change request/campus approval status;
- invoke 72h validator;
- create Amendment.

If only name/organization/job title/phone changes, it is an ordinary contact-information update.

## 5.1 Pending invitation nuance

If this campus currently has a PENDING contact invitation and the user edits only metadata while keeping the same pending email:
- do not mint a new token;
- do not resend automatically;
- do not extend `expires_at`;
- do not increment `token_version`;
- update the pending snapshot/contact display data consistently if supported;
- preserve the existing invitation lifecycle.

If the invitation is already EXPIRED, metadata-only edit must not silently revive it. Resend remains a separate explicit action.

---

# 6. Path B — Email CHANGED: identity workflow

Condition:

```text
Normalize(newEmail) != Normalize(currentEmail)
```

Then this IS an identity change.

Use the canonical operational-contact identity workflow already present. Do NOT implement a new ad-hoc token/email mechanism.

## 6.1 Before a campus decision

Use canonical pre-decision replace / INITIAL_CONFIRMATION behavior.

Expected:

```text
new email
→ pending confirmation
→ confirmation email to NEW/PENDING contact
→ token/action URL
→ confirmation lifecycle
```

Preserve the existing canonical rule for whether the campus temporarily re-enters `WAITING_CONTACT_CONFIRMATION` and whether the prior relation is cleared before confirmation.

## 6.2 Decided / Approved campus

Use canonical `TRANSFER`.

```text
A = current confirmed contact
B = requested new contact
```

When transfer starts:

```text
A remains current contact
B is pending
campus decision remains unchanged
request aggregate remains unchanged
host/schedule remain unchanged
```

Only after B accepts may the canonical transfer transaction replace A.

If B declines/expires/cancels:

```text
A remains current contact
B is not assigned
```

---

# 7. Email rule for contact editing

Central rule:

```text
ONLY an EMAIL/IDENTITY change triggers a confirmation invitation email.
```

Metadata-only changes to name / organization / job title / phone must NOT trigger contact confirmation email.

## 7.1 Existing verified self-match

Audit the existing rule when:

```text
new email == verified registrant email
```

Current code may link the verified registrant immediately without another confirmation email.

Do not remove this exception accidentally. Report explicitly whether it remains and why.

---

# 8. Fix existing bug: contactFullName is mapped from email

Audit `OperationalContactInvitationService`.

Current bad mapping:

```text
contactFullName = NewEmailNormalized
```

`contactFullName` must be the person's actual full name from the pending/contact snapshot.

Expected source order:

```text
PendingSnapshotJson.fullName
→ persisted contact full name if valid for this invitation
→ safe neutral fallback for legacy data
```

Never pretend an email address is a person's full name.

Add tests for both INITIAL_CLAIM and TRANSFER confirmation email.

---

# 9. Fix existing gap: Reject email recovery after post-commit send failure

Keep:

```text
Reject business transaction
→ commit
→ notification delivery
```

But prove recovery.

Failure scenario:

```text
campus becomes REJECTED
DB commit succeeds
email delivery/rendering fails
```

A repeated Reject cannot be the retry mechanism because the campus is already REJECTED.

Implement/reuse a durable recovery path so the notification can be retried without replaying the Reject command.

Acceptance:

```text
DB committed + first email attempt FAILED
→ rejection event remains correct
→ notification is recoverable/retryable
→ no duplicate outbound email on successful retry
```

---

# 10. Fix existing gap: Contact-expiry email recovery

Keep semantics:

```text
PENDING
→ EXPIRED
→ token invalid
→ commit
→ notify registrant
```

But if:

```text
EXPIRED commit succeeds
email fails
```

the notification must not be permanently lost.

The next maintenance scan normally sees only PENDING rows, so rerunning the expiry sweep alone is not sufficient.

Acceptance:

```text
one invitation
→ one EXPIRED business transition
→ registrant eventually gets at most one successful expiry notification
→ repeat worker scans do not create duplicate mail
```

Do not revert EXPIRED back to PENDING just to retry email.

---

# 11. Fix post-commit API consistency

For business actions such as Reject/contact mutation, a successful business transaction must not be presented as though the mutation failed merely because a post-commit notification had a template/render/provider problem.

Expected:

```text
business state success
+
notification outcome recorded separately
```

Do not return a misleading generic mutation failure after the database already committed the requested action.

---

# 12. Keep the existing 72h scope exactly as already fixed

Visit-registration 72h applies ONLY to:

```text
1. Create Visit Request
2. PRE-APPROVAL Edit submission
3. Resubmit after rejection
```

It does NOT apply to:

```text
Approved Amendment / Đề xuất thay đổi
Operational-contact profile update
INITIAL_CLAIM
TRANSFER
Accept/Decline/Resend/Cancel contact invitation
Contact invitation expiry
Passive passage of time
```

Contact edit from Detail must never invoke `MinScheduleLeadHours`.

---

# 13. Preserve previous frontend fixes

Do not regress:
- dynamic campus max;
- Create/Edit same canonical campus ceiling;
- controlled campus select;
- exactly one Edit success toast;
- exactly one Resubmit success toast;
- no replay on reload/back-forward.

---

# 14. Detail UI behavior

The operational-contact card should show:

```text
Họ và tên
Đơn vị công tác
Chức vụ
Số điện thoại
Email
Trạng thái xác nhận
Nguồn xác nhận
Xác nhận lúc
```

Place `Chỉnh sửa đầu mối` in this card/header/action area.

Do not send the user to `/edit` to manage the contact.

If there is a pending identity change, show a clear pending state and existing permitted resend/cancel actions.

Do not falsely display a pending email as an already-confirmed current identity.

---

# 15. Backend API design

Prefer a dedicated contact-management contract.

Acceptable:
- one PATCH/PUT operational-contact endpoint with server-side branching;
- or two explicit endpoints: metadata update and change email.

Do NOT route metadata-only changes through `ReplaceOperationalContactCommandHandler` if that handler always supersedes invitation, clears current contact, creates a new identity change, and sends mail.

Reuse shared validation/audit helpers rather than duplicating business logic.

---

# 16. Concurrency and audit

Metadata update:
- load one campus/contact;
- verify authorization;
- verify row version/current identity;
- update allowed fields;
- increment row version;
- audit;
- commit.

A stale modal must not overwrite newer contact information.

For email identity change, canonical identity-change concurrency rules remain authoritative.

---

# 17. Required tests

## UI
- Edit Request has no editable contact fields and no contact-change button.
- Detail View has contact edit action for authorized user.
- Read-only role has no action.

## Metadata-only
- same email + changed name/org/title/phone updates data;
- no token;
- no mail;
- no identity-change row;
- no status/gate/approval change;
- no 72h validation;
- approved campus <72h still allows metadata-only contact update.

## Pending invitation
- same pending email + metadata change does not mint/reissue token;
- no auto resend;
- expiry/version unchanged;
- expired invitation is not revived.

## Email change
- pre-decision changed email uses canonical initial confirmation/replace;
- approved/decided changed email uses TRANSFER;
- old current contact stays active during transfer;
- decline/expiry does not assign new contact;
- case/whitespace-normalized same email does NOT trigger confirmation.

## Email variable
- `contactFullName` equals actual person's name;
- it is never the email address.

## Request Edit backend
- attempted contact mutation via Edit is rejected;
- attempted contact mutation via Resubmit is rejected.

## Notification reliability
- Reject commit succeeds + first mail fails → retryable without replaying Reject;
- Contact expiry commit succeeds + first mail fails → retryable without reverting EXPIRED.

---

# 18. Do NOT do these

```text
- put contact inputs back into Visit Request Edit
- send confirmation email for metadata-only update
- change OperationalContactUserId for metadata-only update
- clear confirmation for metadata-only update
- reopen WAITING_CONTACT_CONFIRMATION for metadata-only update
- apply 72h to contact edit
- apply 72h to Approved Amendment
- route Detail "Chỉnh sửa đầu mối" to request /edit
- use email as contactFullName
- use repeat Reject as the retry mechanism
- use a PENDING-only expiry sweep as retry for an already EXPIRED notification
- send mail before business commit
- remove IMMUTABLE_CONTACT_IDENTITY
- bypass ISystemEmailDispatcher with direct SMTP/Resend
```

---

# 19. Suggested implementation order

```text
A. Preflight + preserve WIP
B. Remove contact management from Visit Request Edit UI
C. Add contact-edit action/modal to Visit Detail
D. Add/adjust dedicated metadata-only backend update
E. Branch changed-email path into canonical INITIAL_CLAIM / TRANSFER
F. Strengthen request-edit immutability for ALL contact fields
G. Fix contactFullName mapping
H. Fix Reject notification recovery
I. Fix contact-expiry notification recovery
J. Verify post-commit API semantics
K. Run all regression gates
```

---

# 20. Final report

Report:
1. Preflight.
2. Exact frontend separation.
3. Exact backend endpoint/handler for metadata-only update.
4. Exact changed-email path for pre-decision and approved campus.
5. Verified-self-match behavior.
6. `contactFullName` fix.
7. Reject retry/recovery semantics.
8. Expiry retry/recovery semantics.
9. Post-commit API behavior.
10. Changed files.
11. Tests/gates.
12. Remaining debt.

Decision matrix:

| Current state | Email changed? | Expected action | Confirmation email? | Current contact replaced when? |
|---|---:|---|---:|---|
| pre-decision | No | metadata update | No | never |
| pre-decision | Yes | canonical initial confirmation/replace | Yes | canonical rule |
| approved/decided | No | metadata update | No | never |
| approved/decided | Yes | TRANSFER | Yes | only on accept |

---

# 21. Definition of Done

- [ ] Visit Request Edit no longer edits operational contact.
- [ ] Detail View owns the contact-management action.
- [ ] Same email + metadata changes = ordinary update, no email/token.
- [ ] Changed email = canonical identity confirmation workflow.
- [ ] TRANSFER preserves current contact until acceptance.
- [ ] Request Edit/Resubmit backend cannot mutate contact fields.
- [ ] Contact edit never invokes registration 72h validation.
- [ ] Approved Amendment remains outside registration 72h.
- [ ] `contactFullName` uses real name, not email.
- [ ] Reject notification survives post-commit mail failure.
- [ ] Contact-expiry notification survives post-commit mail failure.
- [ ] Existing campus/toast/72h/email fixes remain green.
