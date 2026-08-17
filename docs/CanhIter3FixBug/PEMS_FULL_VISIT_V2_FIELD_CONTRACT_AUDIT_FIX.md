# PEMS — FULL VISIT V2 FIELD CONTRACT AUDIT & FIX
## Required / Optional / Read-only / Legacy Compatibility / Validation UX

## 0. MỤC TIÊU

Không sửa riêng:

- Phone bị gắn `*` sai;
- Organization legacy NULL gây `1 error`;
- một màn Edit cụ thể.

Phải rà soát **toàn bộ contract field của Visit Request V2** để frontend, backend, read model, payload, validation và UI thống nhất.

Bug hiện tại cho thấy đang có **policy drift**:

```text
Create rule
≠
Edit UI rule
≠
Edit backend validator
≠
Read-model legacy compatibility
≠
One-door contact business rule
```

Điều này tạo ra các lỗi kiểu:

```text
Phone vốn OPTIONAL
→ Edit lại hiện *

Organization của contact cũ = NULL
→ Edit schema báo lỗi
→ UI chỉ hiện "1 error"
→ field contact lại read-only
→ user không thể sửa
→ submit bị block
```

Không được fix bằng cách thêm một message đỏ dưới Organization rồi dừng.

Phải sửa **root contract**.

---

# 1. TRƯỚC KHI LÀM

Bắt buộc chạy:

```bash
git status
git diff
```

Working tree hiện có nhiều thay đổi chưa commit.

KHÔNG:

- reset;
- revert;
- checkout file về Dev;
- stash làm mất thay đổi;
- overwrite các patch đang có;
- commit khi chưa được yêu cầu.

Phải làm trên **CURRENT WORKING TREE**.

Đặc biệt phải giữ nguyên:

- one-door Operational Contact model;
- Quick Edit không sửa contact profile;
- Amendment không sửa contact profile;
- Manage Contact là cửa duy nhất sửa contact profile/identity;
- Organization-required patch mới;
- legacy pending-transfer Organization guard;
- durable contact-member relation;
- validation UX/focus/scroll hiện có;
- partner ID / organizationPartnerId logic.

---

# 2. BUSINESS RULE CHỐT — KHÔNG ĐƯỢC TỰ SUY DIỄN

## 2.1 Registrant

Rule chuẩn:

| Field | Rule |
|---|---|
| Full name | REQUIRED |
| Nationality | REQUIRED |
| Organization | REQUIRED |
| Job title | REQUIRED |
| Phone | **OPTIONAL** |
| Email | REQUIRED |

Phone:

```text
blank/null
→ VALID

có nhập
→ phải đúng phone format
```

Không được gắn `*` cho Phone ở bất kỳ form nào nếu rule backend vẫn optional.

---

## 2.2 Operational Contact — NEW WRITE

Khi đang THỰC SỰ tạo/sửa contact profile:

| Field | Rule |
|---|---|
| Full name | REQUIRED |
| Organization | REQUIRED |
| Job title | REQUIRED |
| Phone | **OPTIONAL** |
| Email | REQUIRED |

Áp dụng cho:

```text
Create Visit
Manage Contact
Replace Contact
Transfer Contact
```

---

## 2.3 Operational Contact — EXISTING CAMPUS trong Edit

Đối với campus đã tồn tại:

```text
Whole Pending Edit
Resubmit
Per-campus Pending Edit
```

contact snapshot là:

```text
READ-ONLY
```

Không phải editable field của Visit Edit.

Do đó:

```text
FullName
Organization
JobTitle
Phone
Email
```

không được block một unrelated Edit chỉ vì legacy snapshot thiếu dữ liệu.

Ví dụ:

```text
legacy Organization = NULL
```

user vẫn phải được sửa:

```text
Purpose
Schedule
Guest list
Working content
Notes
...
```

mà không bị:

```text
1 error
→ không biết error ở đâu
→ không submit được
```

---

# 3. NGUYÊN TẮC QUAN TRỌNG NHẤT

Phải phân biệt:

```text
CREATE VALIDATION
```

và:

```text
REPLAY OF IMMUTABLE EXISTING DATA
```

Không được dùng rule:

> "Create không cho tạo trạng thái này nên Edit cũng phải reject trạng thái legacy này"

khi:

```text
Edit không có quyền sửa field đó.
```

Nếu field là read-only trong Edit thì Edit không được yêu cầu user sửa nó qua chính Edit.

---

# 4. AUDIT TOÀN BỘ FIELD CONTRACT

Tạo một matrix thật trước khi code.

Phải audit ít nhất:

## Request-level Registrant

```text
fullName
nationality
organization
jobTitle
phone
email
partnerId
```

## Campus content

```text
campus
startDatetime
endDatetime
delegationName
visitType
visitTypeOther
purpose
workingContent
workingLanguage
transportationNote
mediaConsentStatus
notes
```

## Guest / Support member

```text
fullName
jobTitle
organization
organizationPartnerId
nationality
clientMemberKey
```

## Operational Contact

```text
fullName
organization
jobTitle
phone
email
operationalContactClientMemberKey
```

Với mỗi field phải ghi:

| Field | Create | Whole Edit | Resubmit | Pending Campus Edit | Safe Edit | Amendment | Manage Contact |
|---|---|---|---|---|---|---|---|

Mỗi cell chỉ được là:

```text
REQUIRED
OPTIONAL
READ_ONLY
NOT_PRESENT
CONDITIONAL
```

Không code trước khi matrix được xác định từ code/business rule hiện tại.

---

# 5. FRONTEND — PHONE OPTIONAL CONSISTENCY

Audit:

```text
frontend/pems-react/src/features/visit-request/components/v2/VisitRequestFormV2.tsx
frontend/pems-react/src/pages/dashboard/visit/EditVisitRequestV2Page.tsx
frontend/pems-react/src/pages/dashboard/visit/EditPendingCampusV2Page.tsx
frontend/pems-react/src/features/visit-request/components/v2/CampusVisitCard.tsx
frontend/pems-react/src/features/visit-request/components/ContactIdentityActions.tsx
```

Tìm mọi:

```tsx
<FormField ... phone ... required>
```

và mọi schema rule cho Phone.

## Fix

Registrant Phone:

```text
Create       → OPTIONAL
Whole Edit   → OPTIONAL
Resubmit     → OPTIONAL
```

Operational Contact Phone:

```text
Create           → OPTIONAL
Manage Contact   → OPTIONAL
Replace          → OPTIONAL
Transfer         → OPTIONAL
```

Không chỉ bỏ dấu `*`.

Phải xác nhận:

```text
blank phone
→ frontend valid
→ payload hợp lệ
→ backend valid
→ DB nullable/normalization đúng
```

Có phone:

```text
invalid format
→ đúng field đỏ
→ message cụ thể
```

---

# 6. FRONTEND — EXISTING CONTACT KHÔNG ĐƯỢC GÂY FORM ERROR

Audit:

```text
CampusVisitCard.tsx
EditVisitRequestV2Page.tsx
EditPendingCampusV2Page.tsx
visitRequestV2.schema.ts
```

Hiện existing campus dùng:

```tsx
contactReadOnly={instanceId != null}
```

hoặc:

```tsx
contactReadOnly
```

nhưng form schema vẫn validate OperationalContact bằng Create rule.

Đây là conflict.

## Kỳ vọng

Với existing campus:

```text
contactReadOnly = true
```

thì validation của edit phải KHÔNG block vì:

```text
operationalContact.fullName
operationalContact.organization
operationalContact.jobTitle
operationalContact.phone
operationalContact.email
```

nếu đây là legacy snapshot.

---

# 7. KHÔNG ĐƯỢC "FIX" BẰNG CÁCH BỎ CONTACT KHỎI PAYLOAD MÙ QUÁNG

Backend edit hiện có thể cần contact snapshot trong payload để:

- canonical comparison;
- immutable-field comparison;
- rebuild relation;
- operationalContactClientMemberKey.

Do đó trước khi đổi payload shape phải audit thật.

Không được tự:

```ts
delete operationalContact
```

nếu backend contract vẫn cần nó.

Phải quyết định rõ:

### Option A — Edit-specific schema

Frontend:

```text
existing campus contact
→ hydrate
→ read-only
→ không required-validate
```

payload vẫn gửi snapshot cũ để backend compare immutable.

HOẶC:

### Option B — Schema discriminated by visitInstanceId/contactReadOnly

Ví dụ logic:

```text
visitInstanceId == null
→ New campus
→ full Create validation

visitInstanceId != null
→ Existing campus
→ contact snapshot compatibility validation only
```

Chọn cách phù hợp architecture hiện tại.

Không duplicate schema bừa.

---

# 8. BACKEND — ĐÂY LÀ PHẦN BẮT BUỘC

Audit:

```text
CampusVisitFormDtoValidator
OperationalContactV2Validator
UpdatePendingVisitRequestV2CommandValidator
ResubmitRejectedVisitRequestV2CommandValidator
UpdatePendingVisitInstanceV2CommandValidator
VisitRequestV2EditService
EnsureContactSnapshotUnchanged
```

Hiện các Edit validator có pattern:

```csharp
content.ToFormDto()
    .SetValidator(new CampusVisitFormDtoValidator());
```

Điều này khiến existing contact legacy phải pass Create validation.

Phải sửa contract cho đúng one-door architecture.

---

# 9. BACKEND — PHÂN BIỆT NEW CAMPUS VÀ EXISTING CAMPUS

Rule:

## New campus

Nếu một flow thật sự cho phép add campus và:

```text
VisitInstanceId == null
```

thì Operational Contact là NEW WRITE:

```text
FullName       REQUIRED
Organization   REQUIRED
JobTitle       REQUIRED
Phone          OPTIONAL
Email          REQUIRED
```

## Existing campus

Nếu:

```text
VisitInstanceId != null
```

thì Operational Contact snapshot:

```text
IMMUTABLE THROUGH EDIT
```

Backend phải:

1. Không yêu cầu legacy snapshot phải đạt Create completeness.
2. Nhưng vẫn phải bảo đảm client không thay đổi snapshot.

Tức:

```text
stored Organization = NULL
payload Organization = ""
→ acceptable replay / canonical-equivalent

stored Organization = NULL
payload Organization = "ABC"
→ REFUSE contact profile mutation through Edit

stored Phone = NULL
payload Phone = ""
→ acceptable replay

stored Phone = "+849..."
payload Phone = "+841..."
→ REFUSE
```

Dùng normalize/canonical comparison phù hợp.

---

# 10. `EnsureContactSnapshotUnchanged` LÀ AUTHORITY CHO EXISTING CONTACT

Audit thật kỹ:

```text
EnsureContactSnapshotUnchanged(...)
```

Đây nên là nơi bảo vệ:

```text
Edit cannot mutate existing contact snapshot
```

Không để Create validator vô tình trở thành:

```text
legacy data migration gate
```

Phải test:

```text
legacy incomplete contact
+
no contact changes
→ Edit allowed
```

và:

```text
legacy incomplete contact
+
client attempts contact modification
→ Refused
```

---

# 11. READ MODEL / HYDRATION LEGACY COMPATIBILITY

Audit:

```text
VisitFormReadService.cs
ResolvedVisitFormDto.cs
visitRequestV2Api.ts
resolvedFormToV2Schema()
```

Hiện read service có thể normalize DB NULL thành:

```text
""
```

để JSON/frontend dễ dùng.

Đây có thể giữ.

Nhưng comment/type phải nói đúng:

```text
Empty string may represent legacy missing snapshot data.
New contact writes require Organization/JobTitle/Email etc.
```

Không được comment:

```text
Organization is optional
```

nếu chỉ đang nói "nullable for legacy compatibility".

---

# 12. LEGACY WARNING UX

Nếu existing contact read-only có missing required-under-current-policy field:

Ví dụ:

```text
Organization = —
```

không hiện:

```text
1 error
```

như form error.

Thay vào đó, hiển thị informational/actionable warning:

EN:

```text
Some contact details are incomplete.
Update them from "Manage the contact role".
```

VI:

```text
Một số thông tin đầu mối chưa đầy đủ.
Vui lòng cập nhật tại "Quản lý đầu mối".
```

Có thể chỉ hiện warning khi thật sự thiếu:

```text
FullName
Organization
JobTitle
Email
```

Phone không tính vì Phone optional.

---

# 13. KHÔNG BLOCK UNRELATED EDIT

Case bắt buộc:

Stored legacy:

```text
OperationalContact.Organization = NULL
```

User mở Whole Edit và chỉ sửa:

```text
Purpose
```

Expected:

```text
NO contact form error
NO "1 error" from legacy Organization
Submit allowed
Backend accepts
Organization remains NULL
Purpose changes
```

Sau đó user muốn sửa Organization:

```text
Detail
→ Manage the contact role
→ Organization *
→ Save
```

Đây mới là one-door flow.

---

# 14. CARD ERROR COUNT

Audit:

```text
countFieldErrors(cardErrors)
```

và:

```text
Campus 1 — 1 error
```

Read-only legacy contact field **không được tính vào editable-form error count**.

Nếu warning legacy tồn tại:

```text
warning count
≠
validation error count
```

Không trộn hai khái niệm.

---

# 15. FOCUS / SCROLL

Hiện validation UX đã có:

```text
open card
scroll
focus first invalid
```

Giữ nguyên.

Nhưng first-invalid chỉ được focus tới field user **có thể sửa trong flow hiện tại**.

Không được focus:

```text
read-only Organization
```

hoặc một summary `<dd>`.

Legacy contact warning không tham gia invalid-field focus.

---

# 16. FRONTEND SERVER ERROR MAPPING

Audit:

```text
mapServerFieldPathToFormPath()
applyServerErrors()
```

Nếu backend vẫn trả error path cho immutable legacy contact replay thì đó là dấu hiệu backend contract chưa sửa đúng.

Sau fix, expected:

```text
backend field errors
→ editable field
```

Contact mutation refusal qua Edit:

```text
IMMUTABLE_CONTACT_PROFILE
IMMUTABLE_CONTACT_IDENTITY
```

→ global business error / correct workflow message,
không fake thành editable field error.

---

# 17. AUDIT SAFE EDIT

Xác minh Safe Edit hiện đúng one-door:

```text
Operational Contact fields
→ NOT_PRESENT / BLOCKED
```

Không sửa nếu đúng.

Nhưng thêm test parity nếu cần để bảo đảm future regression không đưa contact trở lại Safe Edit.

---

# 18. AUDIT AMENDMENT

Xác minh Amendment:

```text
contact profile fields
→ READ_ONLY / NOT_AMENDABLE
```

Chỉ relationship:

```text
operationalContactClientMemberKey
```

nếu business rule hiện cho phép.

Không để Amendment validator vẫn yêu cầu "complete operational contact" chỉ vì Proposal DTO carry snapshot legacy.

Đặc biệt audit test cũ:

```text
Amendment_requires_a_complete_operational_contact
```

Nếu one-door model đã thay business rule thì test này có thể đang pin behavior cũ.

Không sửa test chỉ để xanh.

Phải xác định contract thật trước.

---

# 19. AUDIT TEST `VisitRequestV2WritePathParityTests`

Đọc:

```text
tests/PEMS.UnitTests/VisitRequests/VisitRequestV2WritePathParityTests.cs
```

Không được mặc định:

```text
Create required
→ Edit required y hệt
```

với field immutable/read-only.

Thay concept parity thành:

```text
editable field parity
```

và:

```text
immutable field replay compatibility
```

Ví dụ test mới:

```text
Create requires OperationalContact.Organization
```

nhưng:

```text
Pending edit allows unchanged legacy blank Organization
```

và:

```text
Pending edit rejects changing legacy blank Organization through Edit
```

---

# 20. REQUIRED TEST MATRIX

## Registrant Phone

### PHONE-01

Create:

```text
phone = ""
→ VALID
```

### PHONE-02

Whole Edit:

```text
phone = ""
→ VALID
```

### PHONE-03

Resubmit:

```text
phone = ""
→ VALID
```

### PHONE-04

Invalid nonblank:

```text
090abc
→ INVALID
```

### PHONE-05

UI Whole Edit:

```text
Phone label
→ NO *
```

---

# 21. LEGACY CONTACT TESTS

## LEGACY-CONTACT-01

Stored existing campus:

```text
Organization = NULL
```

Hydrate Whole Edit.

Expect:

```text
Organization: —
warning shown
NO form-validation error
NO card "1 error" from Organization
```

---

## LEGACY-CONTACT-02

Same data.

User edits Purpose.

Expect:

```text
submit succeeds
Organization remains NULL
Purpose updates
```

---

## LEGACY-CONTACT-03

Per-campus Pending Edit.

Legacy Organization NULL.

Expect:

```text
edit page loads
no blocking validation
unrelated field can save
```

---

## LEGACY-CONTACT-04

Resubmit.

Legacy contact incomplete but unchanged.

Expect:

```text
resubmit validation does NOT reject solely for immutable legacy contact
```

provided this matches lifecycle/business rule after audit.

---

## LEGACY-CONTACT-05

Client attempts:

```text
Organization NULL
→ "New Org"
```

through Whole Edit.

Expect:

```text
backend REFUSES
IMMUTABLE_CONTACT_PROFILE
```

or existing stable code.

---

# 22. NEW CONTACT STILL STRICT

Phải bảo đảm fix legacy compatibility không nới Create.

Tests:

```text
Create missing Organization → reject
Create missing FullName → reject
Create missing JobTitle → reject
Create missing Email → reject
Create blank Phone → accept
```

Manage Contact:

```text
Organization blank → reject
Phone blank → accept
```

Replace:

```text
Organization blank → reject
Phone blank → accept
```

Transfer:

```text
Organization blank → reject
Phone blank → accept
```

---

# 23. MEMBER RULE AUDIT

Vì đang làm FULL FIELD CONTRACT AUDIT, kiểm cả:

```text
Guest
Support staff
```

Chốt hiện tại:

Guest row:

```text
FullName      REQUIRED
JobTitle      REQUIRED
Organization  REQUIRED
Nationality   REQUIRED
```

Support list:

```text
list itself optional
```

nhưng nếu một row tồn tại:

```text
row phải complete
```

Không sửa rule nếu backend/business hiện xác nhận như vậy.

Kiểm:
- Create
- Edit
- Resubmit
- Amendment nếu Amendment được phép sửa member list.

---

# 24. CONDITIONAL FIELD AUDIT

Kiểm:

```text
VisitType == OTHER
→ VisitTypeOther REQUIRED
```

và:

```text
VisitType != OTHER
→ VisitTypeOther not required / normalized null
```

Kiểm tương tự:

```text
schedule end > start
duration >= minimum
lead time rules
```

Không để Edit schema vô tình block unchanged historical schedule chỉ vì thời gian hiện tại đã tiến gần ngày visit nếu business rule nói chỉ validate lead time khi schedule MOVES.

---

# 25. NULL / EMPTY / WHITESPACE NORMALIZATION

Audit tất cả field optional:

```text
Phone
TransportationNote
Notes
VisitTypeOther when not OTHER
WorkingContent nếu business rule optional/required theo current contract
```

Không được có tình trạng:

```text
Frontend null
Backend ""
DB null
Read ""
```

mà equality comparison coi đó là 4 giá trị khác nhau.

Tạo canonical rule rõ:

```text
null / "" / whitespace
→ equivalent for optional text
```

nếu đúng business semantics.

---

# 26. TYPESCRIPT TYPES

Audit:

```text
V2ContactPointDto
ResolvedOperationalContact
Registrant
```

Nếu API read contract có thể chứa nullable DB field nhưng server normalize thành `""`, type `string` có thể giữ.

Nếu server thật trả null, type phải phản ánh null.

Không để TS contract nói:

```text
phone: string
```

trong khi API có thể trả:

```json
"phone": null
```

Xác minh bằng backend serializer/read DTO, không đoán.

---

# 27. COMMENTS / DOCUMENTATION DRIFT

Search các comment kiểu:

```text
phone required
organization optional
job title optional
all contact fields required everywhere
create/edit use exact same bar
```

Sửa comment sai.

Không để comment cũ dẫn agent/dev sau tạo lại bug.

---

# 28. VALIDATION SUMMARY UX

Khi form invalid:

```text
N errors
```

N phải chỉ đếm:

```text
editable validation errors
```

Không đếm:

- legacy warning;
- read-only snapshot gap;
- informational notice.

Nếu warning contact legacy tồn tại, UI nên phân biệt màu:

```text
validation error → red
legacy incomplete contact → amber
```

---

# 29. ACCESSIBILITY

Editable invalid field:

```text
aria-invalid=true
aria-describedby=...
role="alert"
```

Read-only incomplete legacy field:

```text
không aria-invalid
```

vì user không thể sửa tại đó.

Warning có thể:

```text
role="status"
```

hoặc appropriate non-blocking alert semantics.

---

# 30. KHÔNG ĐƯỢC LÀM

- Không biến Phone thành required.
- Không thêm `*` cho Phone.
- Không bỏ Organization required trên NEW contact writes.
- Không cho Edit sửa Contact profile.
- Không đưa contact profile trở lại Quick Edit.
- Không đưa contact profile trở lại Amendment.
- Không migrate toàn DB chỉ để Edit pass.
- Không auto-fill Organization từ Registrant.
- Không bỏ validation Create.
- Không ignore backend validation bằng catch.
- Không remove tests để suite xanh.
- Không sửa chỉ một screen.
- Không chỉ sửa frontend.
- Không commit.

---

# 31. CÁC FILE PHẢI AUDIT TỐI THIỂU

Frontend:

```text
frontend/pems-react/src/features/visit-request/schema/visitRequestV2.schema.ts
frontend/pems-react/src/features/visit-request/components/v2/VisitRequestFormV2.tsx
frontend/pems-react/src/features/visit-request/components/v2/CampusVisitCard.tsx
frontend/pems-react/src/pages/dashboard/visit/EditVisitRequestV2Page.tsx
frontend/pems-react/src/pages/dashboard/visit/EditPendingCampusV2Page.tsx
frontend/pems-react/src/features/visit-request/utils/visitRequestV2Form.ts
frontend/pems-react/src/features/visit-request/api/visitRequestV2Api.ts
frontend/pems-react/src/features/visit-request/components/VisitAmendmentSubmitModal.tsx
frontend/pems-react/src/features/visit-request/components/VisitSafeEditModal.tsx
frontend/pems-react/src/features/visit-request/components/ContactIdentityActions.tsx
frontend/pems-react/src/features/visit-request/components/shared/FormField.tsx
frontend/pems-react/src/features/visit-request/components/shared/PhoneField.tsx
frontend/pems-react/src/features/visit-request/utils/formErrorNavigation.ts
```

Backend:

```text
backend/PEMS.Application/Common/DTOs/VisitFormV2Dtos.cs
backend/PEMS.Application/Common/DTOs/VisitFormV2EditDtos.cs
backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/*
backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequestV2/*
backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitInstanceV2/*
backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequestV2/*
backend/PEMS.Application/Delegations/Commands/VisitAmendments/*
backend/PEMS.Application/Delegations/Commands/OperationalContact/*
backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs
backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs
backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs
backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs
backend/PEMS.Application/Delegations/Services/VisitFormRead/*
```

Tests:

```text
tests/PEMS.UnitTests/VisitRequests/VisitRequestV2WritePathParityTests.cs
tests/PEMS.IntegrationTests/VisitRequests/UpdatePendingVisitRequestV2ServiceTests.cs
tests/PEMS.IntegrationTests/VisitRequests/ResubmitRejectedVisitRequestV2ServiceTests.cs
tests/PEMS.IntegrationTests/VisitRequests/InstanceResubmitAuthorizationTests.cs
tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs
tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs
frontend visit v2 form/edit tests
```

---

# 32. TEST GATES

Frontend:

```bash
npx tsc --noEmit
npm run lint
npm run test:unit --no-file-parallelism
npm run build
```

Backend:

```bash
dotnet build
dotnet test
```

Integration phải chạy real disposable MySQL theo repo convention.

Không build output ra ngoài repo nếu test root-discovery phụ thuộc vị trí binary.

---

# 33. BROWSER SMOKE BẮT BUỘC

## Smoke A — Phone optional

Whole Edit:

```text
Registrant Phone = blank
```

Expected:

```text
label không *
Save không bị block vì Phone
```

---

## Smoke B — Legacy Organization

Seed/existing:

```text
OperationalContactOrganization = NULL
```

Open `/dashboard/visit/v2/{id}/edit`.

Expected:

```text
Organization: —
No fake "1 error" caused solely by this field
Optional amber warning
```

Change:

```text
Purpose
```

Save.

Expected:

```text
Save succeeds
Organization stays NULL
Purpose persists
```

---

## Smoke C — Manage Contact

Open detail:

```text
Manage the contact role
```

Organization blank:

```text
→ field đỏ
→ Save blocked
```

Phone blank:

```text
→ no required error
```

Fill Organization valid:

```text
→ Save succeeds
```

---

# 34. REPORT SAU FIX

Báo cáo theo format:

## A. Field-contract matrix

Bảng đầy đủ:

```text
Create / Edit / Resubmit / Pending Campus / Safe Edit / Amendment / Manage Contact
```

## B. Root causes

Không ghi chung chung.

Nêu:
- Phone `required` marker drift ở đâu;
- Create schema reuse sai ở Edit ở đâu;
- backend shared validator reuse sai ở đâu;
- read-model legacy normalization gây interaction gì.

## C. Files changed

Exact files.

## D. Before / After

Ví dụ:

```text
Before:
legacy Organization NULL
→ Edit card = 1 error
→ no visible editable field
→ save blocked

After:
legacy Organization NULL
→ read-only "—"
→ non-blocking warning
→ unrelated edit saves
→ Organization can only be fixed in Manage Contact
```

## E. Tests

Exact numbers.

## F. Browser smoke

Actual flows clicked.

## G. Remaining drift

Search toàn repo và ghi rõ còn chỗ nào:
- Phone marked required sai;
- comment policy stale;
- validator mismatch;
- legacy read mismatch.

Không báo DONE nếu còn.

---

# 35. DEFINITION OF DONE

Chỉ DONE khi:

- [ ] Registrant Phone optional ở Create/Edit/Resubmit.
- [ ] Operational Contact Phone optional ở mọi new-write path.
- [ ] Organization required ở mọi NEW contact write.
- [ ] Existing contact snapshot read-only trong Edit.
- [ ] Legacy incomplete contact không block unrelated Edit.
- [ ] Existing contact mutation qua Edit vẫn bị backend refuse.
- [ ] Whole Edit không còn fake `1 error` từ legacy read-only contact.
- [ ] Pending Campus Edit không còn cùng bug.
- [ ] Resubmit không còn cùng bug nếu contact unchanged.
- [ ] Safe Edit one-door giữ nguyên.
- [ ] Amendment one-door giữ nguyên.
- [ ] Card error count chỉ tính editable errors.
- [ ] Required markers khớp validator thật.
- [ ] FE/BE field contracts thống nhất.
- [ ] Legacy read compatibility giữ nguyên.
- [ ] Full tests pass thật.
- [ ] Browser smoke pass.
- [ ] Không commit.

DỪNG sau khi báo cáo để tôi review.
