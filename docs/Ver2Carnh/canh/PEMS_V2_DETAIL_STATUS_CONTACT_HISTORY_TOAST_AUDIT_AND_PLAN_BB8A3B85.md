# PEMS — AUDIT & IMPLEMENTATION PLAN
## V2 Request Detail: Outcome Summary, Contact Identity, Revision, History, and Toast Standardization

**Repository:** `quangthoai04/PEMS`  
**Target branch:** `Cảnh-Iter1` / `Canh-Iter1`  
**Exact audit baseline:** `bb8a3b8573af6ca6d6dedc73861c3651fd142a3e`  
**Audit type:** Static code audit at the exact commit. No code was changed and no local test suite was executed during this audit.

---

# 1. Objective

Continue from the completed authenticated V2 create/OTP identity work and plan the remaining corrections:

1. Correct the misleading **“Lưu thay đổi”** action on the detail page.
2. Remove duplicated registrant/contact information from the overview.
3. Replace it with a user-facing current outcome summary.
4. Move primary-contact claim/transfer controls into Section 2.
5. Replace confusing campus revision wording.
6. Convert history from technical strings to business language.
7. Standardize mutation feedback through the existing global top-right toast.
8. Find and remove remaining visit-module local/misplaced toast systems.
9. Preserve Pure V2, backend scope, `allowedActions`, and per-campus isolation.

---

# 2. Sources of truth

```text
1. Code/schema/tests at bb8a3b8573af6ca6d6dedc73861c3651fd142a3e
2. Current database schema
3. PEMS_CANONICAL_BUSINESS_RULES...
4. PEMS_PER_CAMPUS_V2_MASTER_HANDOFF_PROMPT...
5. PEMS_UC_IMPLEMENTATION_RULEBOOK...
6. PEMS_UI_DESIGN_SYSTEM_PROMPT
7. Legacy documents only for historical comparison
```

Mandatory invariants:

- Backend remains the final authorization authority.
- Frontend mutation buttons remain driven by backend `allowedActions`.
- Do not leak hidden-campus counts, decisions, actors, or history.
- Do not choose the first campus as a representative campus.
- Do not fall back to V1.
- Prefer additive DTO/read-model changes; cancellation and decision metadata already exist in the entities.

---

# 3. Current implementation status

The commit already contains the completed P0 identity work:

- authenticated direct create is bound to the logged-in registrant email;
- different email follows the existing OTP initiate/verify flow;
- forged processing intent is rejected;
- V2 detail already uses blue/orange sections, status badges, and person tables.

Do not reimplement this work.

---

# 4. Finding A — “Lưu thay đổi” is misleading

## Current code

File:

```text
frontend/pems-react/src/features/visit-request/components/v2/VisitRequestV2DetailView.tsx
```

The overview button labelled **“Lưu thay đổi”** is a `<Link>` to:

```text
/dashboard/visit/v2/{visitRequestId}/edit
```

It does not save anything. The label reuses `visitRequestV2:edit.saveEdit`, which is also the correct label for the real edit-form submit button.

## Decision

Add a separate key:

```json
{
  "edit": {
    "openEdit": "Sửa đơn",
    "saveEdit": "Lưu thay đổi"
  }
}
```

Final detail actions:

```text
Sửa đơn
Sửa nhanh
```

“Lưu thay đổi” must remain only on a button that actually submits changes.

## Acceptance criteria

```text
[ ] Navigation-only action is labelled “Sửa đơn”.
[ ] Actual edit submit remains “Lưu thay đổi”.
[ ] VI and EN keys are separated.
[ ] Tests assert both labels independently.
```

---

# 5. Finding B — overview duplicates Sections 1 and 2

The overview currently repeats:

- registrant name and organization;
- primary-contact name/status.

Section 1 and Section 2 display the same information in full immediately below.

## Required overview

Keep only:

```text
Request code
Request status
Visible campus count
Submitted time
Current outcome/progress
Available actions
```

Remove:

```text
Registrant summary
Primary-contact summary
```

Those belong only in Sections 1 and 2.

---

# 6. New current-outcome design

## Pending

```text
TÌNH TRẠNG HIỆN TẠI
Đang chờ 2 cơ sở xử lý.
```

## Partial

```text
TÌNH TRẠNG HIỆN TẠI
1 cơ sở đã tiếp nhận · 1 cơ sở đang chờ xử lý.
```

## Rejected

```text
TÌNH TRẠNG HIỆN TẠI
Đơn đã bị từ chối tại tất cả cơ sở.

Từ chối gần nhất: 20/08/2026 09:30
Người xử lý: IC Staff Leader Hà Nội
Lý do: Trùng lịch sự kiện tại cơ sở.
```

## Cancelled

```text
TÌNH TRẠNG HIỆN TẠI
Đơn đã bị hủy.

Hủy lúc: 20/08/2026 09:30
Người thực hiện: Kim Min Jae
Lý do: Thay đổi lịch công tác của đoàn.
```

## Mixed multi-campus outcome

```text
TÌNH TRẠNG HIỆN TẠI
1 cơ sở đã tiếp nhận · 1 cơ sở từ chối.
Xem quyết định chi tiết trong từng cơ sở bên dưới.
```

## Scope rule

Calculate the summary only from campus instances returned to the caller.

Do not expose:

- hidden campus totals;
- hidden decisions;
- hidden reasons;
- hidden actors;
- hidden cancellation metadata.

---

# 7. Read-model additions

`ResolvedVisitFormDto` currently lacks request-level cancellation metadata.  
`ResolvedCampusVisitDto` exposes decisions but not campus cancellation metadata.

Existing entities already contain the necessary fields.

## Request-level data

```text
VisitRequest.CancelledBy
VisitRequest.CancelledAt
VisitRequest.CancellationReason
```

## Campus-level data

```text
VisitRequestCampus.CancelledBy
VisitRequestCampus.CancelledAt
VisitRequestCampus.CancellationActorType
VisitRequestCampus.CancellationSource
VisitRequestCampus.CancellationReason
```

## Recommended additive DTO fields

Request level:

```csharp
public ulong? CancelledByUserId { get; init; }
public string? CancelledByName { get; init; }
public DateTime? CancelledAt { get; init; }
public string? CancellationReason { get; init; }
```

Campus level:

```csharp
public ulong? CancelledByUserId { get; init; }
public string? CancelledByName { get; init; }
public DateTime? CancelledAt { get; init; }
public string? CancellationActorType { get; init; }
public string? CancellationSource { get; init; }
public string? CancellationReason { get; init; }
```

Derive progress counts from the already-scoped `campusVisits` in frontend. Resolve actor names in one backend query.

No database migration should be created unless a real missing field is proven.

---

# 8. Finding C — contact management is outside Section 2

Current hierarchy:

```text
Standalone ContactIdentityPanel
Section 1 — Registrant
Section 2 — Primary contact data
```

This splits one business object across two cards and causes the duplicate contact block in the screenshot.

## Required Section 2

```text
② ĐẦU MỐI LIÊN HỆ CỦA ĐƠN

Thông tin
- Họ và tên
- Đơn vị
- Số điện thoại
- Email

Trạng thái vai trò
- Chờ xác nhận / Đã xác nhận / Đang chờ chuyển giao
- Xác nhận lúc
- Lời mời có hiệu lực đến

Quản lý đầu mối
- Gửi lại lời mời
- Nhập lại email đầu mối
- Chuyển giao vai trò đầu mối
- Hủy lời mời chuyển giao
```

## Refactor

Convert `ContactIdentityPanel` into an embeddable action block, suggested name:

```text
ContactIdentityActions
```

Remove:

- outer standalone card;
- duplicated contact heading;
- old standalone gray/dark styling.

Keep all existing claim/replace/transfer/resend/cancel workflows.

## Authorization

Do not broaden the current manager relation. Audit whether backend should emit explicit contact action codes:

```text
RESEND_CONTACT_CLAIM
REPLACE_PENDING_CONTACT
INITIATE_CONTACT_TRANSFER
RESEND_CONTACT_TRANSFER
CANCEL_CONTACT_TRANSFER
```

Preferred long-term design: backend emits capabilities and every handler re-authorizes.

---

# 9. Finding D — “Nội dung v1 · Phê duyệt v1” is confusing

The current card always renders:

```text
Phiên bản
Nội dung v{form} · Phê duyệt v{approval}
```

For `WAITING_REQUEST_APPROVAL`, this looks as if the campus has already approved the request.

## Required wording

### Waiting for first decision

```text
Tình trạng nội dung
Nội dung hiện tại: phiên bản 1
Trạng thái xét duyệt: Chưa được duyệt
```

### Approved/assigned

```text
Tình trạng nội dung
Đang áp dụng nội dung phiên bản 2
Được duyệt ở lần 1
```

### Rejected

```text
Nội dung phiên bản 1 đã bị từ chối
```

### Cancelled

```text
Nội dung phiên bản 2
Lịch thăm đã bị hủy
```

### Amendment pending

```text
Đang áp dụng nội dung phiên bản 2
Đề xuất thay đổi #3 đang chờ duyệt
```

Never determine approval solely from:

```text
approvalRevision > 0
```

Use:

```text
instanceStatus
decidedAt
activeAmendment
```

Confirm initial `approvalRevision` semantics against create service and tests before altering stored values. Prefer conditional UI wording instead of rewriting database revision numbers.

---

# 10. Finding E — history still exposes technical data

Frontend timeline styling is acceptable, but backend emits strings such as:

```text
source=CREATE;approvalRevision=1
source=CREATE
status=PENDING
email=***;PENDING→APPLIED
Cơ sở: REJECTED
```

The backend also loads an actor-name dictionary but constructs entries with `ActorName = null`, so actor names are not actually attached.

Initial multi-campus creation can also show identical entries without campus names.

## Preferred structured contract

```csharp
public sealed record VisitHistoryEntryDto(
    DateTime At,
    string EventCode,
    ulong? VisitInstanceId,
    string? CampusName,
    string? ActorName,
    uint? FormRevision,
    uint? ApprovalRevision,
    uint? AmendmentNo,
    string? StatusCode,
    string? SourceType,
    string? Reason,
    string? MaskedEmail,
    string? FromStatus,
    string? ToStatus);
```

Suggested event codes:

```text
REQUEST_CREATED
INSTANCE_CONTENT_CREATED
INSTANCE_APPROVED
INSTANCE_REJECTED
REQUEST_CANCELLED
INSTANCE_CANCELLED
SAFE_EDIT_APPLIED
AMENDMENT_SUBMITTED
AMENDMENT_APPROVED
AMENDMENT_REJECTED
AMENDMENT_WITHDRAWN
CONTACT_INVITATION_SENT
CONTACT_CONFIRMED
CONTACT_TRANSFER_SENT
CONTACT_TRANSFER_CANCELLED
CONTACT_TRANSFER_COMPLETED
REQUEST_RESUBMITTED
```

Frontend maps event codes/status/source through i18n.

## User-facing examples

```text
Kim Min Jae đã tạo đơn.
```

```text
Nội dung cho FPT University Hà Nội được tạo — phiên bản 1.
```

```text
IC Staff Leader Hà Nội đã từ chối tiếp nhận tại FPT University Hà Nội.
Lý do: Trùng lịch sự kiện của cơ sở.
```

## Must not render

```text
source=CREATE
approvalRevision=1
PENDING→APPLIED
STANDARD_CAMPUS_REVIEW
INTERNAL_SELF_HOST
raw enum
raw JSON
```

Fix actor-name assignment before returning entries.

---

# 11. Global top-right toast already exists

`App.tsx` already mounts:

```tsx
<Toaster position="top-right" containerStyle={{ zIndex: 9999 }} />
```

Shared helper:

```text
frontend/pems-react/src/shared/utils/toast.ts
```

Available functions:

```text
showSuccessToast
showErrorToast
showMessageErrorToast
showLoadingToast
updateToastSuccess
updateToastError
```

Do not add another library or another `<Toaster>`.

---

# 12. Finding F — cancellation toast is bottom-right

`VisitRequestManagement.tsx` implements its own:

```text
Toast type
toasts state
pushToast()
custom error parser
custom viewport
```

The viewport is:

```tsx
fixed bottom-5 right-5
```

This explains the wrong position reported for cancellation and related actions.

## Required correction

Delete the local toast implementation and use `shared/utils/toast`.

This local system currently covers operations including:

- reject;
- invitation accept;
- department assignment;
- cancellation;
- approval/host assignment paths.

After migration, all these mutation notifications must appear through the global top-right toaster.

---

# 13. Confirmed mutation-feedback gaps

## ContactIdentityPanel

Current:

- inline `message`;
- no shared toast;
- active-transfer load failure is swallowed;
- hardcoded Vietnamese;
- `new Date(...).toLocaleString('vi-VN')`.

Plan:

```text
[ ] Shared success/error toast.
[ ] Client field validation remains inline.
[ ] Shared Vietnam time formatter.
[ ] Fetch error is not converted into “no transfer”.
[ ] Inline load error + retry.
[ ] VI/EN i18n.
```

## Safe edit modal

Current:

- sets `applied`;
- immediately calls parent `onSaved`;
- parent closes the modal;
- success panel is effectively unreachable;
- no success toast.

Plan:

```text
[ ] Show top-right success toast before close.
[ ] Reload detail.
[ ] Remove unreachable success state or change callback semantics.
[ ] Use shared error extraction.
```

## Amendment submit modal

Current:

- closes on success;
- no success toast.

Plan:

```text
[ ] Toast “Đã gửi đề xuất thay đổi cho {campus}.”
[ ] Close and reload after success.
[ ] Keep actionable stable-code errors inline.
[ ] Generic/non-field failures use shared error toast.
```

## Amendment decision panel

Current:

- approve/reject/withdraw result is inline only;
- load failure is swallowed;
- old date formatting.

Plan:

```text
[ ] Shared toast for approve/reject/withdraw.
[ ] Refresh panel after mutation.
[ ] Visible load error + retry.
[ ] Shared date formatter.
[ ] i18n.
```

## Edit/resubmit page

Current:

```ts
navigate(detailRoute, { state: { flash: res.message } })
```

but the detail view does not consume `state.flash`.

Plan:

- detail page consumes flash;
- displays exactly one success toast;
- immediately clears flash with replace-state to prevent replay.

## Cancellation modal feedback rule

```text
Client validation → inline.
API success → top-right success toast.
Non-field API/business failure → top-right error toast and modal stays open.
Field-specific backend error → inline at the field.
```

---

# 14. Remaining visit-module toast inventory

Repository search found local `pushToast` patterns in at least:

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx
frontend/pems-react/src/pages/dashboard/visit/MinutesCard.tsx
frontend/pems-react/src/features/delegations/components/ParticipantInvitationSection.tsx
frontend/pems-react/src/features/delegations/components/LogisticsRequestSection.tsx
```

Run:

```bash
rg -n "type Toast|pushToast\(|toasts\.map|fixed bottom-|fixed top-|toast\.|showSuccessToast|showErrorToast|setMessage\("   frontend/pems-react/src/pages/dashboard/visit   frontend/pems-react/src/features/visit-request   frontend/pems-react/src/features/delegations
```

Classification:

```text
A. Mutation success/error → global top-right toast.
B. Client field validation → inline.
C. Query/page load error → inline error state + retry.
D. Persistent business state → panel/badge.
E. Confirmation → modal.
```

---

# 15. Implementation slices

## Slice 0 — Preflight

```text
git status
git branch --show-current
git rev-parse HEAD
git log -n 10 --oneline
git diff --check
```

Expected baseline:

```text
bb8a3b8573af6ca6d6dedc73861c3651fd142a3e
```

Do not reset automatically if local HEAD differs.

## Slice 1 — Wording and overview

Commit:

```text
fix(visit-ui): clarify v2 detail actions and remove duplicate summary
```

```text
[ ] Add edit.openEdit.
[ ] Rename detail action to “Sửa đơn”.
[ ] Keep actual submit “Lưu thay đổi”.
[ ] Remove registrant/contact from overview.
[ ] Add initial scoped progress summary.
```

## Slice 2 — Outcome metadata

Commit:

```text
feat(visit): expose scoped request and campus outcome metadata
```

```text
[ ] Add request cancellation metadata to V2 read model.
[ ] Add campus cancellation metadata.
[ ] Resolve actor names.
[ ] Preserve scope.
[ ] Render rejected/cancelled actor/time/reason.
```

## Slice 3 — Contact workflow in Section 2

Commit:

```text
refactor(visit-ui): integrate contact identity actions into contact section
```

```text
[ ] Remove standalone contact card.
[ ] Embed actions in Section 2.
[ ] Display ACTIVE/PENDING/TRANSFER_PENDING clearly.
[ ] Add toast, i18n, shared date formatter, retry.
[ ] Preserve backend authorization.
```

## Slice 4 — User-facing revision state

Commit:

```text
fix(visit-ui): present campus content versions by lifecycle state
```

```text
[ ] Waiting says “Chưa được duyệt”.
[ ] Active says “Đang áp dụng nội dung phiên bản X”.
[ ] Rejected/cancelled wording matches outcome.
[ ] Amendment remains separate.
[ ] Technical numbers become secondary.
```

## Slice 5 — User-facing history

Commit:

```text
refactor(visit-history): replace technical audit strings with business events
```

```text
[ ] Structured event code/metadata.
[ ] Correct actor names.
[ ] Campus name included.
[ ] No raw source/status/JSON.
[ ] i18n mapping.
[ ] Preserve scope and masked identity data.
```

## Slice 6 — Toast standardization

Commit:

```text
refactor(visit-ui): standardize mutation feedback at top right
```

```text
[ ] Delete VisitRequestManagement local toast viewport.
[ ] Fix cancellation toast location.
[ ] Consume edit/resubmit flash once.
[ ] Add safe-edit toast.
[ ] Add amendment toast.
[ ] Add contact-identity toast.
[ ] Audit remaining visit local toast systems.
[ ] Keep exactly one global Toaster.
```

---

# 16. Tests

## Frontend

```text
1. Detail action = “Sửa đơn”.
2. Edit submit = “Lưu thay đổi”.
3. Overview does not repeat registrant/contact.
4. Pending/partial/rejected/cancelled summaries.
5. Scope-restricted summary.
6. Contact actions are inside Section 2.
7. Contact actions hidden for read-only/out-of-scope users.
8. Waiting campus does not say “Phê duyệt v1”.
9. Revision wording for all lifecycle states.
10. Timeline has no “source=” or raw enum.
11. Actor and campus names appear in history.
12. Edit/resubmit flash creates exactly one toast.
13. Safe edit, amendment, contact, cancel create exactly one toast.
14. No local bottom-right toast viewport remains.
```

## Backend/integration

```text
1. Request cancellation metadata mapping.
2. Campus cancellation metadata mapping.
3. Actor-name resolution.
4. Hidden-campus metadata excluded.
5. History actor names populated.
6. Structured history event mapping.
7. Identity history masked.
8. Staff Leader sees only own campus.
9. Host sees only own instance.
10. HO remains read-only.
```

## Real-stack

```text
A. Pending request detail has no duplicate overview.
B. Pending edit returns with top-right toast.
C. Safe edit returns with top-right toast.
D. Cancel shows top-right toast and outcome metadata.
E. Staff Leader rejects a campus; campus card/history are user-facing.
F. Partial multi-campus summary is scope-correct.
G. Contact claim/transfer actions live in Section 2.
H. Amendment submit/approve/reject shows toast and clean history.
```

---

# 17. Required gates

```bash
dotnet build
dotnet test tests/PEMS.ArchitectureTests
dotnet test tests/PEMS.UnitTests
dotnet test tests/PEMS.IntegrationTests
npm run lint
npm run test
npm run build
git diff --check
```

Use a disposable database for integration/E2E.

---

# 18. Definition of Done

```text
[ ] Detail navigation button is not named “Lưu thay đổi”.
[ ] Overview no longer repeats Sections 1 and 2.
[ ] Overview explains the current business outcome.
[ ] Rejected/cancelled outcomes show actor/time/reason where in scope.
[ ] Contact management is inside Section 2.
[ ] Waiting campus does not display misleading approval wording.
[ ] Timeline contains no raw technical strings.
[ ] Timeline actor names work.
[ ] Multi-campus entries are distinguishable.
[ ] Cancellation uses global top-right toast.
[ ] Edit/resubmit, safe edit, amendment, and contact actions use toast.
[ ] No additional Toaster is mounted.
[ ] No local bottom-right toast remains in the audited flow.
[ ] Backend scope and Pure V2 isolation remain intact.
[ ] All gates and real-stack journeys are green.
[ ] Documentation is updated.
```

---

# 19. Final report format

```text
1. Branch and HEAD before/after.
2. Files changed.
3. DTO/API changes.
4. UI changes.
5. Contact identity changes.
6. Revision/history changes.
7. Toast migrations.
8. Tests added.
9. Test results.
10. Real-stack evidence.
11. Database impact.
12. Known limitations.
13. Remaining local-toast candidates.
```

Every completion claim must include file/test/runtime evidence.
