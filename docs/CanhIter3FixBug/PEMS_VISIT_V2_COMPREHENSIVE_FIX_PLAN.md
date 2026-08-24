# PEMS – Comprehensive Fix & Regression-Safety Plan
## Visit V2 / Amendment / Operational Contact / Instance Resubmit

**Repository:** `quangthoai04/PEMS`  
**Baseline đã audit:** `2937c32831ba72366297e5c0fc0e569142c547ce`  
**Ngày lập:** 2026-08-24  
**Mục tiêu:** Fix triệt để các lỗi đã xác minh trong Visit V2 mà **không làm hỏng business rule, phân quyền, multi-campus isolation, revision history, partner linking, Operational Contact identity workflow hoặc các luồng Create/Edit/Resubmit/Amendment khác**.

---

# 1. Phạm vi và nguyên tắc bắt buộc

Tài liệu này xử lý các nhóm lỗi đã được xác minh:

1. Amendment false-positive `CONTACT_PROFILE_NOT_AMENDABLE` khi phone `NULL`.
2. Test hiện tại che mất bug `NULL` phone.
3. Amendment cho xóa member đang giữ vai trò Operational Contact.
4. Amendment silently clear `OperationalContactClientMemberKey`.
5. UI member editor trong Amendment dùng `isCell` sai context.
6. Purpose / Working Content / Reject reason có nested scrollbar.
7. Working Content thiếu frontend validation parity.
8. Phone read-only null hiển thị không rõ.
9. Instance Resubmit chỉ sửa schedule trên UI nhưng backend full-replace member.
10. Instance Resubmit có thể làm mất:
    - `OrganizationPartnerId`
    - `ClientMemberKey`
    - `OperationalContactClientMemberKey`
    - partner/contact linkage liên quan.
11. Instance Resubmit xử lý datetime phụ thuộc timezone browser.
12. Frontend `V2ContactPointDto` drift so với backend.
13. Amendment Review có thể lộ internal ID / UUID / raw field path.

## Nguyên tắc không được vi phạm

- **Backend là nguồn xác định business rule cuối cùng.**
- Không chữa logic bằng cách chỉ “đổi format frontend”.
- Không được xóa rule `ContactProfileNotAmendable`.
- Không được tự động chọn một member khác làm Operational Contact.
- Không được đoán identity bằng:
  - array index;
  - full name;
  - fuzzy matching;
  - organization string.
- `ClientMemberKey` chỉ là correlation token trong một request/form session, không phải authorization.
- `OrganizationPartnerId` là identity của partner selection, không được suy ra chỉ từ text nếu chưa có bằng chứng.
- Mọi write path phải giữ **multi-campus isolation**.
- Không được thay đổi sibling campus khi action chỉ target một `VisitInstanceId`.
- Không được thay đổi active data trước khi amendment được approve.
- Không được làm mất audit/revision history.
- Không được phá optimistic concurrency.
- Không được làm yếu Operational Contact confirmation/transfer workflow.
- Không merge/deploy nếu regression suite chưa pass.

---

# 2. Các file chính cần audit/chỉnh sửa

## Backend

```text
backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs
backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs
backend/PEMS.Application/Delegations/Common/OperationalContactLink.cs
backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs
backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs
backend/PEMS.Application/Partners/VisitLinks/Common/GuestPartnerLinkResolver.cs
backend/PEMS.Domain/ValueObjects/PhoneNumber.cs
```

Nếu namespace thực tế của `PhoneNumber.cs` khác path logic ở trên thì giữ nguyên namespace hiện tại, không di chuyển file chỉ để phục vụ patch.

## Frontend

```text
frontend/pems-react/src/features/visit-request/components/VisitAmendmentSubmitModal.tsx
frontend/pems-react/src/features/visit-request/components/VisitAmendmentPanel.tsx
frontend/pems-react/src/features/visit-request/components/InstanceResubmitPanel.tsx
frontend/pems-react/src/features/visit-request/components/v2/CampusVisitCard.tsx
frontend/pems-react/src/features/visit-request/components/shared/AutoGrowTextarea.tsx
frontend/pems-react/src/features/visit-request/components/shared/OrganizationCombobox.tsx
frontend/pems-react/src/features/visit-request/components/shared/CountrySelect.tsx
frontend/pems-react/src/features/visit-request/components/shared/visitDateTime.ts
frontend/pems-react/src/features/visit-request/api/visitRequestV2Api.ts
frontend/pems-react/src/features/visit-request/schema/visitRequestV2.schema.ts
frontend/pems-react/src/features/visit-request/utils/visitRequestV2Form.ts
frontend/pems-react/src/shared/i18n/locales/vi/visitRequestV2.json
frontend/pems-react/src/shared/i18n/locales/en/visitRequestV2.json
```

## Test

```text
tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs
tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs
tests/PEMS.IntegrationTests/VisitRequests/GuestPartnerLinkResolverSharedMemberTests.cs

frontend/pems-react/src/features/visit-request/__tests__/VisitV2Modals.test.tsx
frontend/pems-react/src/features/visit-request/__tests__/InstanceResubmitPanel.test.tsx
frontend/pems-react/src/features/visit-request/__tests__/EditPendingCampusV2Page.test.tsx
frontend/pems-react/src/features/visit-request/__tests__/visitRequestV2Form.test.ts
frontend/pems-react/src/features/visit-request/__tests__/visitRequestV2Required.test.ts
```

---

# 3. FIX-A – Amendment false-positive Operational Contact profile change

## 3.1 Root cause

Trong `VisitAmendmentService.BuildChangeRows(...)`, phone hiện được so sánh trong hai semantic space khác nhau.

Tình huống:

```text
DB = null
Proposal = null
```

nhưng current side có thể thành:

```text
NormalizeOrOriginal(null) => ""
```

còn proposed side vẫn là:

```text
null
```

Kết quả:

```text
"" != null
```

và backend throw:

```text
ContactProfileNotAmendable
```

dù user không sửa contact profile.

## 3.2 Cách sửa

Dùng **cùng một normalization** cho cả hai phía.

Khuyến nghị:

```csharp
var currentPhone = PhoneNumber.NormalizeOrNull(
    detail.OperationalContactPhone);

var proposedPhone = PhoneNumber.NormalizeOrNull(
    proposedContact.Phone);

var phoneChanged = !string.Equals(
    currentPhone,
    proposedPhone,
    StringComparison.Ordinal);
```

Sau đó đưa `phoneChanged` vào `changed`.

## 3.3 Guardrail

Không được thay:

```text
null / blank semantic equality
```

bằng logic khiến phone thực sự khác nhau bị coi là giống nhau.

Phải giữ:

```text
+84901234567 != +84907654321
```

và vẫn throw `ContactProfileNotAmendable`.

## 3.4 Test bắt buộc

- DB `null`, proposal `null` → không lỗi.
- DB `null`, proposal `""` → không lỗi.
- DB whitespace legacy, proposal null → xử lý theo semantic no-value.
- National/E.164 equivalent → không lỗi nếu normalization coi là cùng số.
- Phone khác thật → vẫn lỗi.
- FullName khác → vẫn lỗi.
- Organization khác → vẫn lỗi.
- JobTitle khác → vẫn lỗi.
- Email khác → vẫn phải đi theo identity rule hiện hành, không được vô tình mở đường sửa email bằng amendment.

---

# 4. FIX-B – Sửa test gap của Amendment phone

## 4.1 Vấn đề

Test helper hiện có chỗ dùng:

```csharp
d.OperationalContactPhone ?? ""
```

làm mất trường hợp `null` thực tế.

## 4.2 Cách sửa

Không dùng helper ép null thành empty cho testcase regression.

Tạo testcase phản ánh đúng frontend contract:

```text
Phone = null
```

## 4.3 Assertion bắt buộc

Không chỉ assert HTTP success.

Phải assert thêm:

- amendment row được tạo;
- không có contact-profile change row giả;
- active form detail chưa bị thay đổi;
- approval state đúng;
- revision/audit không có contact change giả.

---

# 5. FIX-C – Đồng bộ rule xóa member đang là Operational Contact

## 5.1 Vấn đề

Create/Edit đã block delete contact member.

Amendment hiện không block và sau đó `contactMemberKey` có thể tự về `null`.

## 5.2 Business rule chuẩn

Nếu member đang giữ contact relationship:

```text
Delete
→ BLOCK
```

User phải chủ động:

```text
1. chọn member khác làm contact
hoặc
2. chọn "Không nằm trong danh sách đoàn"
```

sau đó mới xóa.

## 5.3 Frontend implementation

Trong `VisitAmendmentSubmitModal.tsx`:

- xác định member bằng `clientMemberKey`;
- trước remove, check:

```ts
member.clientMemberKey === contactMemberKey
```

Nếu đúng:

- không mutate visitors/support list;
- không clear key;
- show localized error;
- giữ focus/state hiện tại.

## 5.4 `useEffect` clear key

Không nên xóa defensive effect hoàn toàn.

Giữ nó như **last-resort invariant repair**, nhưng:

- bình thường user flow không được kích hoạt;
- thêm comment `defensive fallback only`;
- test phải chứng minh delete UI không đi vào nhánh này.

## 5.5 Test

- Contact = Visitor A; delete A → bị block.
- Contact = Support B; delete B → bị block.
- Đổi contact A → C; delete A → thành công.
- Chọn “not in delegation”; delete A → thành công.
- Xóa unrelated member → thành công.
- Reorder/add member → contact key vẫn trỏ đúng người.

---

# 6. FIX-D – Purpose / WorkingContent auto-grow + validation parity

## 6.1 Purpose

Thay fixed textarea bằng:

```tsx
<AutoGrowTextarea
  minRows={3}
  maxLength={2000}
/>
```

## 6.2 WorkingContent

Thay fixed textarea bằng:

```tsx
<AutoGrowTextarea
  minRows={3}
  maxLength={4000}
/>
```

## 6.3 Validation parity

Backend yêu cầu WorkingContent.

Frontend Amendment phải validate:

```text
trim().length > 0
```

và map error vào chính field.

## 6.4 Không duplicate component

Phải reuse:

```text
components/shared/AutoGrowTextarea.tsx
```

Không tạo component mới chỉ cho modal.

## 6.5 Test

- long initial value tự mở chiều cao khi modal mở;
- paste 10+ lines không tạo nested scrollbar;
- WorkingContent empty → frontend block submit;
- WorkingContent chỉ whitespace → frontend block;
- 4000 chars pass;
- >4000 chars bị chặn/validate theo contract.

---

# 7. FIX-E – Organization / Nationality UI trong Amendment

## 7.1 Root cause

`OrganizationCombobox` và `CountrySelect` nhận:

```tsx
isCell
```

nhưng Amendment không nằm trong real table cell.

## 7.2 Fix

Ưu tiên:

```text
remove isCell
```

tại Amendment call site.

Không sửa global `isCell` behavior nếu Create/Edit đang phụ thuộc nó.

## 7.3 Vì sao không sửa shared component globally

`CampusVisitCard` dùng `isCell` bên trong `<td>` thật, nơi border/cell chrome nằm ở table.

Nếu thay global style của `isCell`, có nguy cơ phá:

- Create Visit;
- Edit Visit;
- Pending Campus Edit;
- responsive table.

Do đó fix phải **scope ở Amendment**.

## 7.4 Member row grouping

Sau khi bỏ `isCell`, kiểm tra lại row.

Nếu vẫn rời rạc, thêm wrapper nhẹ:

```text
rounded-lg
border
bg-white
p-2
```

Không thêm nested table chỉ để giống Create/Edit.

## 7.5 Test

- Organization có border/focus/error state.
- Nationality có border/focus/error state.
- `×` / dropdown icon nằm trong control.
- Create/Edit desktop table vẫn giữ `isCell`.
- Mobile card không bị regression.

---

# 8. FIX-F – Read-only Operational Contact presentation

## 8.1 Phone null

Thay:

```tsx
{phone}
```

bằng localized fallback:

```text
Chưa có thông tin
```

hoặc shared empty-state wording nếu dự án đã có key phù hợp.

## 8.2 Read-only visual

Không render value như editable input.

Ưu tiên semantic:

```html
<dl>
  <dt>...</dt>
  <dd>...</dd>
</dl>
```

hoặc read-only card tương tự `CampusVisitCard`.

## 8.3 Không biến thành editable

Màn Amendment không được mở lại đường chỉnh contact profile.

Operational Contact profile phải tiếp tục quản lý qua contact-management workflow riêng.

---

# 9. FIX-G – Instance Resubmit: bỏ full-replace member khi UI chỉ sửa schedule

Đây là phần rủi ro cao nhất.

## 9.1 Hiện trạng

UI `InstanceResubmitPanel` chỉ cho sửa:

```text
plannedStartAt
plannedEndAt
```

nhưng payload gửi full campus snapshot.

Backend `ApplyInstanceResubmitAsync(...)` lại:

```text
ApplyFormDetail
StageReplaceMembers
LinkMembers
```

Do đó một action “sửa lịch” thực tế có thể rewrite content/member identity.

## 9.2 Giải pháp ưu tiên: schedule-only semantics

Refactor contract/handler để resubmit rejected instance chỉ update các trường thực sự cho phép edit trong UI.

### Payload mong muốn

Ví dụ:

```csharp
public record ResubmitRejectedInstanceDto(
    int ExpectedRowVersion,
    DateTime PlannedStartAt,
    DateTime PlannedEndAt
);
```

Nếu endpoint còn cần CampusId để invariant check thì giữ CampusId, nhưng không gửi full member snapshot.

## 9.3 Backend apply

Resubmit rejected instance nên:

1. load request + target instance;
2. authorization;
3. ensure instance rejected;
4. optimistic concurrency;
5. validate schedule;
6. validate campus availability/config nếu nghiệp vụ yêu cầu re-check;
7. snapshot rejection state vào audit;
8. update:
   - PlannedStartAt
   - PlannedEndAt
   - instance status → waiting approval
   - clear decision metadata
   - coordinator assignment theo rule hiện hành
   - row versions
9. recompute aggregate request status;
10. write revision snapshot từ **current persisted content/member**;
11. không `StageReplaceMembers`;
12. không `LinkMembers`;
13. không `ResolvePartnerLinksAsync` nếu member/link không thay đổi.

## 9.4 Không được vô tình bỏ logic cần thiết

Phải giữ:

- campus cannot be swapped;
- 72h lead-time rule;
- campus active/configurable;
- current Staff Leader assignment logic;
- per-instance concurrency;
- sibling campus untouched;
- request aggregate recompute;
- resubmission counters/audit;
- revision history.

## 9.5 Nếu chưa thể đổi API contract

Fallback tạm thời:

- frontend gửi đầy đủ metadata:
  - `organizationPartnerId`
  - stable `clientMemberKey`
  - `operationalContactClientMemberKey`
- backend detect content/member unchanged và skip replace.

Tuy nhiên đây **không phải phương án ưu tiên**, vì vẫn giữ một full-snapshot write path cho một UI schedule-only.

---

# 10. FIX-H – Bảo toàn partner/contact identity trong Resubmit

Nếu chọn schedule-only semantics thì lỗi này tự được loại bỏ ở nguồn.

## Test bắt buộc

### Partner identity

Given:

```text
Guest A.OrganizationPartnerId = 25
```

Resubmit chỉ đổi schedule.

Expected:

```text
OrganizationPartnerId vẫn = 25
```

### Partner link

Given member A có confirmed `visit_guest_partner_links`.

After resubmit:

- link không bị orphan;
- không sinh duplicate;
- không đổi partner;
- không bị remove/reseed vô cớ.

### Contact-member relation

Given:

```text
OperationalContactGuestMemberId = Guest A
```

After schedule-only resubmit:

```text
vẫn là Guest A
```

không chuyển null.

### Member identity

- GuestMemberId không bị thay chỉ vì resubmit schedule.
- Không delete/reinsert member.

### Sibling isolation

Request có HN + DN.

Resubmit HN:

- DN member ids không đổi;
- DN partner links không đổi;
- DN decision không đổi;
- DN form revision không tăng.

---

# 11. FIX-I – Instance Resubmit timezone browser-independent

## 11.1 Không dùng `new Date(...).getHours()` cho Vietnam wall-clock form

Bỏ logic kiểu:

```ts
new Date(iso)
getHours()
toISOString()
```

## 11.2 Dùng helper hiện có

Ưu tiên reuse:

```text
components/shared/visitDateTime.ts
shared/utils/vietnamTime.ts
```

Phải giữ invariant:

```text
2026-10-01T09:00 Vietnam
```

luôn hiển thị là:

```text
09:00
```

dù browser timezone là:

- Asia/Ho_Chi_Minh
- UTC
- America/Los_Angeles
- Europe/London

## 11.3 Hydration

Nếu API trả `+07:00`, dùng helper chuyển về Vietnam wall-clock string.

Không dùng host-local getters.

## 11.4 Submit

Gửi bare Vietnam wall-clock nếu API contract hiện hỗ trợ:

```text
YYYY-MM-DDTHH:mm
```

Backend converter đã định nghĩa bare datetime là Vietnam wall-clock.

## 11.5 Test browser-independent

Unit test pure helper với cùng input.

Không cần đổi process timezone nếu helper pure; nếu test cần timezone, chạy ít nhất hai environment hoặc mock timezone.

Assertions:

```text
+07 input → 09:00
UTC browser → 09:00
US browser → 09:00
```

và round trip:

```text
API → form → submit
```

không đổi instant/wall-clock ngoài ý muốn.

---

# 12. FIX-J – Đồng bộ frontend/backend `V2ContactPointDto`

## 12.1 Backend contract

```csharp
ContactPointDto(
    string FullName,
    string Organization,
    string JobTitle,
    string? Phone,
    string Email)
```

## 12.2 Frontend phải phản ánh đúng

Sửa:

```ts
export interface V2ContactPointDto {
  fullName: string;
  organization: string;
  jobTitle: string;
  phone: string | null;
  email: string;
}
```

## 12.3 Không sửa domain để chiều theo frontend

Không làm phone required ở backend chỉ để hết TypeScript mismatch.

Phone đang là optional business data.

## 12.4 Audit call sites

Sau type change, TypeScript compile sẽ chỉ ra nơi đang giả định phone luôn string.

Mỗi chỗ phải phân loại:

### Editable input

```text
null → ""
```

chỉ tại UI input boundary.

### Read-only display

```text
null → "—" / "Chưa có thông tin"
```

### API replay

Giữ `null` nếu contract backend cho phép.

Không convert toàn cục.

---

# 13. FIX-K – Amendment Review không lộ internal technical values

## 13.1 Không dùng `Object.values(...)` để render DTO

Hiện cách này có thể render:

- OrganizationPartnerId
- ClientMemberKey
- các internal property mới thêm sau này.

## 13.2 Tạo typed presentation mapper

Ví dụ:

```ts
type AmendmentMemberPresentation = {
  fullName: string;
  jobTitle: string;
  organization: string;
  nationality: string;
};
```

Render đúng 4 field người dùng hiểu.

## 13.3 OperationalContactMemberKey

Không hiển thị raw:

```text
instance.operationalContact.clientMemberKey
UUID
```

Nếu backend change row hiện chỉ lưu key mà không đủ display snapshot thì có hai lựa chọn:

### Ưu tiên

Backend amendment change nên lưu user-readable member identity snapshot cho presentation, hoặc DTO trả về được enrich khi đọc.

### Không được

Frontend tự guess name từ UUID bằng string matching.

## 13.4 Unknown field path

Nếu gặp unknown path:

- không show raw path cho end user;
- log/telemetry developer-side nếu có;
- UI dùng localized generic label như “Thay đổi khác” chỉ khi vẫn an toàn;
- tốt hơn fail explicit trong test để buộc thêm mapping khi backend thêm field.

## 13.5 Test

- member diff không chứa UUID;
- không chứa numeric PartnerId;
- không chứa property key `clientMemberKey`;
- known field path đều có friendly label;
- Operational Contact relationship change hiển thị tên người nếu dữ liệu đủ;
- raw JSON không leak.

---

# 14. FIX-L – Amendment reject reason auto-grow

Thay:

```tsx
<textarea rows={2} ... />
```

bằng:

```tsx
<AutoGrowTextarea
  value={note}
  minRows={2}
  maxLength={500}
  ...
/>
```

Giữ rule:

```text
reject reason required
```

Không làm thay đổi approve flow.

---

# 15. Migration/compatibility review

Bản fix không yêu cầu DB schema migration nếu áp dụng đúng các phương án trên.

Tuy nhiên trước merge phải kiểm tra:

- legacy requests có OperationalContactPhone null;
- legacy member rows có thể thiếu client-side keys nhưng DB IDs vẫn tồn tại;
- old pending amendments có legacy contact profile change rows;
- `VisitFieldClassifier` giữ legacy paths để approve proposal cũ;
- không xóa backward compatibility chỉ vì new UI không tạo các row đó nữa.

## Pending amendment cũ

Nếu có amendment được tạo trước patch và đã chứa:

```text
instance.operationalContact.fullName
organization
jobTitle
phone
```

phải vẫn xử lý theo policy hiện có.

Không được làm dữ liệu pending cũ trở thành un-approvable do đổi presentation/classifier.

---

# 16. Test strategy toàn diện

Không chấp nhận chỉ test happy path.

## 16.1 Backend unit/integration

### Amendment

- null phone replay
- blank phone replay
- normalized equivalent phone
- real profile mutation rejected
- member add/remove
- contact member re-point
- invalid contact key fails closed
- approval applies only after approve
- rejection leaves active data unchanged
- withdraw leaves active data unchanged

### Resubmit instance

- rejected only
- cancelled request rejected
- non-rejected instance rejected
- rowVersion conflict
- campus immutable
- schedule end > start
- min duration
- lead time
- campus inactive
- no active Staff Leader
- invalid Staff Leader configuration
- reset only target instance
- aggregate status recomputed
- request resubmission counter increments
- audit written
- revision written
- member IDs unchanged
- partner links unchanged
- contact guest member id unchanged
- sibling untouched

### Operational Contact

- metadata update still works
- phone optional
- email identity change cannot sneak through metadata endpoint
- pending invitation snapshot refresh still works
- transfer/replace behavior unchanged

### Partner links

- no orphan introduced by schedule resubmit
- confirmed links remain idempotent
- shared legacy member behavior unchanged

---

## 16.2 Frontend tests

### Amendment Submit Modal

- long purpose
- long working content
- workingContent required
- phone null fallback
- organization/nationality bordered
- selected contact member cannot be deleted
- switch contact then delete old
- not-in-delegation explicit then delete
- backend field errors map correctly
- ContactProfileNotAmendable still shown for genuine backend refusal

### Amendment Panel

- member diff is human-readable
- no UUID
- no partner ID
- no raw internal field path
- reject note required
- reject note auto-grow

### Instance Resubmit Panel

- only schedule fields editable
- payload contains only schedule contract if refactored
- no full member content submit
- rowVersion sent
- Vietnam wall-clock preserved
- no `toISOString()` timezone drift
- stale version UI remains usable
- rejection reason shown
- success toast exactly once

### API contract

Type tests / TS build should enforce:

```text
phone: string | null
jobTitle: string
```

---

# 17. Cross-flow regression matrix

| Flow | Must remain working | Specific risk from patch |
|---|---|---|
| Create Visit V2 | yes | shared DTO/phone type |
| Edit whole pending request | yes | contact replay / phone nullable |
| Edit one pending campus | yes | shared schema/helper |
| Resubmit whole request | yes | do not accidentally reuse instance-only contract |
| Resubmit one rejected campus | yes | main refactor target |
| Safe Edit | yes | field classifier must remain unchanged |
| Submit Amendment | yes | phone equality / member guard |
| Approve Amendment | yes | legacy change rows |
| Reject Amendment | yes | reject textarea only |
| Withdraw Amendment | yes | no behavior change |
| Operational Contact profile edit | yes | phone optional |
| Replace Contact | yes | identity workflow |
| Transfer Contact | yes | identity workflow |
| Reinvite/Resend Contact | yes | untouched |
| Guest partner resolution | yes | resubmit must not recreate links |
| Revision history | yes | snapshot correctness |
| Multi-campus mixed status | yes | isolation |
| Notifications | yes | no target relation regression |

---

# 18. Multi-campus isolation tests bắt buộc

Tạo fixture:

```text
Request
├── HN – Rejected
├── DN – Approved
└── HCM – Waiting
```

## Resubmit HN

Assert:

### HN

- schedule updated;
- status → waiting approval;
- decision cleared;
- rowVersion ++;
- revision appended.

### DN

- status remains approved;
- host remains;
- decision remains;
- members same IDs;
- contact same ID;
- partner links same;
- revision unchanged.

### HCM

- untouched.

### Request

- aggregate status đúng theo three-campus state;
- RowVersion/bookkeeping đúng;
- không reset toàn bộ request.

---

# 19. Concurrency tests

## Amendment

- two submit attempts with same stale version:
  - exactly one succeeds if mutation contract says exclusive;
  - loser gets expected conflict.
- approval after content changed elsewhere:
  - must not apply stale proposal if current concurrency rules forbid it.

## Resubmit instance

Two concurrent resubmits on same rejected campus:

```text
one winner
one INSTANCE_VERSION_CONFLICT
```

Sibling campus write must not create false conflict if instance-scoped concurrency is the intended rule.

---

# 20. Audit/revision integrity tests

Mỗi mutation phải kiểm tra cả state lẫn history.

## Amendment submit

- only proposal/audit state changes;
- active form stays same.

## Amendment approve

- correct before/after;
- contact relationship re-link đúng;
- no fake phone change.

## Instance resubmit

- rejection state recorded before clear;
- revision snapshot contains existing members;
- schedule changed;
- member IDs do not churn if schedule-only implementation adopted.

---

# 21. Manual QA checklist

## QA-01 – Amendment null phone

1. Campus contact phone = null.
2. Open Amendment.
3. Chỉ đổi Purpose.
4. Submit.

Expected:

- thành công;
- không có ContactProfileNotAmendable.

---

## QA-02 – Amendment long text

Purpose/WorkingContent 10+ dòng.

Expected:

- auto-grow;
- không nested scroll.

---

## QA-03 – Delete contact member

1. A là contact.
2. Delete A.

Expected:

- bị block;
- giải thích rõ.

3. Chọn B.
4. Delete A.

Expected:

- thành công;
- B giữ contact role.

---

## QA-04 – Amendment member UI

Expected:

- Name / Job / Organization / Nationality cùng visual language;
- dropdown/clear icons nằm trong control.

---

## QA-05 – Read-only phone

Phone null:

```text
Số điện thoại
Chưa có thông tin
```

---

## QA-06 – Amendment review

Create proposal thay member.

Reviewer screen phải thấy:

```text
Họ tên / Chức vụ / Đơn vị / Quốc tịch
```

Không thấy:

```text
UUID
partnerId
clientMemberKey
instance.operationalContact.clientMemberKey
```

---

## QA-07 – Schedule-only resubmit

1. Ghi lại GuestMemberId, OrganizationPartnerId, contact GuestMemberId, partner links.
2. Resubmit chỉ đổi ngày giờ.
3. Re-read DB/API.

Expected:

- tất cả identity data giữ nguyên.

---

## QA-08 – Timezone

Test cùng request trên browser timezone khác nhau.

Expected:

- lịch hiển thị giống nhau theo giờ Việt Nam.

---

# 22. Thứ tự triển khai an toàn

## Phase 0 – Baseline

- tạo branch riêng;
- ghi nhận commit baseline;
- chạy test hiện tại;
- lưu danh sách pass/fail trước patch.

## Phase 1 – Backend correctness

1. Fix Amendment phone comparison.
2. Add null-phone regression.
3. Refactor instance resubmit thành schedule-only semantics.
4. Add data-preservation tests.
5. Run backend Visit V2 tests.

## Phase 2 – Contract alignment

6. Fix `V2ContactPointDto`.
7. Fix impacted call sites.
8. TypeScript build.
9. API/form mapper tests.

## Phase 3 – Amendment business UX

10. Block delete active contact member.
11. Keep defensive key-clear fallback only.
12. Add contact relationship tests.

## Phase 4 – UI

13. Auto-grow Purpose.
14. Auto-grow WorkingContent.
15. Add WorkingContent validation.
16. Remove `isCell` only in Amendment.
17. Read-only phone fallback.
18. Read-only contact styling.
19. Amendment review typed presentation.
20. Reject note auto-grow.

## Phase 5 – Time

21. Replace browser-local Date transformations in InstanceResubmit.
22. Add timezone-independent tests.

## Phase 6 – Full regression

23. Backend integration suite.
24. Frontend unit/component tests.
25. Typecheck.
26. Lint.
27. Build.
28. Manual QA.
29. Multi-campus smoke test.

---

# 23. Không được làm các “quick fix” sau

## Không làm

```ts
phone: phone ?? ''
```

rồi coi là đã fix backend.

## Không làm

```text
xóa ContactProfileNotAmendable check
```

## Không làm

```text
resubmit vẫn full-replace member nhưng bỏ test
```

## Không làm

```text
guess Operational Contact by fullName
```

## Không làm

```text
guess partner by organization substring
```

## Không làm

```text
change global isCell style
```

chỉ để sửa Amendment.

## Không làm

```text
new Date(...).toISOString()
```

cho Vietnam wall-clock form.

## Không làm

```text
Object.values(dto)
```

để render business diff.

---

# 24. Definition of Done

Patch chỉ được coi là hoàn thành khi:

### Correctness

- [ ] Amendment phone null không còn false-positive.
- [ ] Genuine contact profile change vẫn bị chặn.
- [ ] WorkingContent required được enforce ở frontend + backend.
- [ ] Contact member không thể bị xóa âm thầm.
- [ ] Instance resubmit không replace member khi chỉ sửa schedule.
- [ ] Member IDs giữ nguyên qua schedule resubmit.
- [ ] OrganizationPartnerId giữ nguyên.
- [ ] OperationalContactGuestMemberId giữ nguyên.
- [ ] Partner links giữ nguyên.
- [ ] Browser timezone không làm đổi giờ visit.

### UI

- [ ] Purpose auto-grow.
- [ ] WorkingContent auto-grow.
- [ ] Reject reason auto-grow.
- [ ] Organization/Nationality có border đúng.
- [ ] Phone null có fallback.
- [ ] Read-only contact nhìn đúng là read-only.
- [ ] Amendment review không leak UUID/ID/raw field path.

### Regression

- [ ] Create Visit pass.
- [ ] Whole Edit pass.
- [ ] Pending Campus Edit pass.
- [ ] Whole Resubmit pass.
- [ ] Instance Resubmit pass.
- [ ] Safe Edit pass.
- [ ] Amendment submit/approve/reject/withdraw pass.
- [ ] Operational Contact update/replace/transfer pass.
- [ ] Partner linking pass.
- [ ] Multi-campus isolation pass.
- [ ] Revision/audit tests pass.
- [ ] Concurrency tests pass.

### Build gates

- [ ] `dotnet test` relevant suites green.
- [ ] frontend unit tests green.
- [ ] TypeScript typecheck green.
- [ ] lint green.
- [ ] production build green.
- [ ] no new console error/warning in target flows.

---

# 25. Commit strategy đề xuất

Không gom một commit khổng lồ nếu có thể tránh.

## Commit 1

```text
fix(visit-amendment): normalize nullable contact phone comparison
```

## Commit 2

```text
fix(visit-resubmit): preserve instance data with schedule-only resubmission
```

## Commit 3

```text
fix(visit-v2): align operational contact api types
```

## Commit 4

```text
fix(visit-amendment): guard contact member removal
```

## Commit 5

```text
fix(visit-amendment): improve form and diff presentation
```

## Commit 6

```text
fix(visit-resubmit): make schedule handling timezone-safe
```

## Commit 7

```text
test(visit-v2): add amendment and resubmit regression coverage
```

Nếu project policy yêu cầu squash thì vẫn nên phát triển theo các commit logic riêng rồi squash khi merge.

---

# 26. Review checklist trước merge

Reviewer phải kiểm tra bằng code, không chỉ nhìn test xanh:

- [ ] `VisitAmendmentService.BuildChangeRows()` normalize phone đối xứng.
- [ ] Không có frontend-only null workaround được dùng như root fix.
- [ ] `ApplyInstanceResubmitAsync()` không còn replace member nếu UI schedule-only.
- [ ] Không có path khác gọi legacy full-replace instance resubmit ngoài ý muốn.
- [ ] `GuestPartnerLinkResolver` không bị gọi vô cớ bởi schedule-only mutation.
- [ ] `OperationalContactLink` không bị đổi semantics.
- [ ] `VisitFieldClassifier` không bị mở thêm writable contact-profile path.
- [ ] `V2ContactPointDto` khớp backend.
- [ ] Amendment modal không dùng `isCell` sai context.
- [ ] Create/Edit vẫn dùng `isCell` trong table đúng context.
- [ ] Không còn `Object.values` render member DTO ở Amendment review.
- [ ] Không còn browser-local datetime conversion trong InstanceResubmit.
- [ ] Test mới assert dữ liệu persisted, không chỉ HTTP/status.

---

# 27. Kết luận kỹ thuật

Bản fix cần được coi là một **correctness + data-preservation + contract + UX patch**, không phải một đợt “chỉnh giao diện”.

Ba nguyên nhân có rủi ro cao nhất là:

```text
1. asymmetric null/empty normalization
2. schedule-only UI nhưng full-snapshot backend write
3. browser-local Date dùng cho Vietnam wall-clock
```

Ba lỗi này phải được xử lý ở **nguồn logic**, sau đó mới làm UI cleanup.

Mục tiêu cuối cùng là:

```text
User thay đổi đúng một thứ
→ hệ thống chỉ thay đổi đúng thứ đó
→ identity/link/status của phần khác không bị side effect
→ history/audit phản ánh đúng
→ UI trình bày đúng business meaning
→ behavior giống nhau giữa các timezone và các flow tương đương
```

Đây là tiêu chí chính để đánh giá patch có “triệt để” hay chỉ là vá triệu chứng.
