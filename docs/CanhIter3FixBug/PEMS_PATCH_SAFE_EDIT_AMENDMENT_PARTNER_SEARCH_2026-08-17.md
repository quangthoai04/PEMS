# PEMS — Kế hoạch triển khai vá lỗi Sửa nhanh / Đề xuất thay đổi / Tìm kiếm đối tác

**Ngày lập kế hoạch:** 17/08/2026  
**Nhánh đối chiếu:** `Dev`  
**Repository:** `quangthoai04/PEMS`

---

## 1. Mục tiêu

Vá đồng bộ các lỗi và khoảng trống hiện tại trong luồng chỉnh sửa Visit Request sau khi đơn đã được tạo, tập trung vào:

1. `Sửa nhanh (áp dụng ngay)` đang thiếu nhãn trường rõ ràng.
2. `Sửa nhanh` có state/backend cho `Ghi chú gửi FPTU` nhưng UI chưa render.
3. `Quốc tịch người đăng ký` có trong form tạo mới nhưng không có đường sửa sau khi đơn đã được duyệt.
4. Tổ chức/đối tác khi sửa đang dùng ô text thường ở một số màn hình, không có search dropdown như form tạo / pending edit.
5. Việc sửa tên tổ chức hiện có nguy cơ làm text và `partnerId` / `organizationPartnerId` lệch nhau.
6. `Đề xuất thay đổi` chưa hiển thị đầy đủ các trường đầu mối mà backend đã hỗ trợ.
7. Danh sách khách / nhân sự hỗ trợ trong amendment chưa giữ đầy đủ `organizationPartnerId`.
8. Amendment chưa giữ được một cách chắc chắn liên kết “Đầu mối là ai trong đoàn?” khi danh sách thành viên được thay đổi.
9. Các control ở màn hình amendment chưa tái sử dụng đầy đủ `OrganizationCombobox`, `PartnerOrgCombobox`, `CountrySelect` như create/edit hiện tại.
10. Cần bổ sung test để đảm bảo không sửa xong nhưng âm thầm mất liên kết partner, mất liên kết đầu mối hoặc tạo dữ liệu không nhất quán.

Mục tiêu cuối cùng:

> Người dùng sửa được đúng trường, nhìn được rõ tên từng trường, search được đối tác/tổ chức giống các form khác, và sau khi lưu thì text hiển thị + ID liên kết trong DB phải luôn nhất quán.

---

# 2. Code hiện tại đã đối chiếu

## Frontend chính

- `frontend/pems-react/src/features/visit-request/components/VisitSafeEditModal.tsx`
- `frontend/pems-react/src/features/visit-request/components/VisitAmendmentSubmitModal.tsx`
- `frontend/pems-react/src/features/visit-request/api/visitRequestV2Api.ts`
- `frontend/pems-react/src/features/visit-request/utils/safeEditDiff.ts`
- `frontend/pems-react/src/features/visit-request/schema/visitRequestV2.schema.ts`
- `frontend/pems-react/src/features/visit-request/utils/visitRequestV2Form.ts`
- `frontend/pems-react/src/features/visit-request/components/v2/VisitRequestFormV2.tsx`
- `frontend/pems-react/src/features/visit-request/components/v2/CampusVisitCard.tsx`
- `frontend/pems-react/src/features/visit-request/components/shared/PartnerOrgCombobox.tsx`
- `frontend/pems-react/src/features/visit-request/components/shared/OrganizationCombobox.tsx`
- `frontend/pems-react/src/features/visit-request/components/shared/CountrySelect.tsx`

## Backend chính

- `backend/PEMS.Application/Common/DTOs/VisitFormV2SafeEditDtos.cs`
- `backend/PEMS.Application/Common/DTOs/VisitAmendmentDtos.cs`
- `backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs`
- `backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs`
- `backend/PEMS.Application/Delegations/Common/OperationalContactLink.cs`
- `backend/PEMS.Application/Partners/Common/GuestOrganizationPartnerPolicy.cs`
- `backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs`
- `backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs`
- `backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs`
- `backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs`
- `backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs`

---

# 3. Nguyên tắc bắt buộc khi vá

## 3.1. Không biến Sửa nhanh thành form sửa tất cả

`Sửa nhanh` chỉ dành cho field được phép áp dụng ngay.

Không được đưa các field approval-sensitive hoặc identity-managed vào đây chỉ vì UI đang thiếu.

### Sửa nhanh nên quản lý

- Người đăng ký:
  - Họ tên
  - Quốc tịch
  - Tổ chức/đối tác
  - Chức vụ
  - Số điện thoại
- Theo campus:
  - Di chuyển
  - Ghi chú gửi FPTU
  - Đồng ý truyền thông
  - Một số thông tin hiển thị của đầu mối hiện đang được backend cho safe-edit

### Không đưa vào Sửa nhanh

- Email người đăng ký
- Email đầu mối
- Tên đoàn
- Loại hình chuyến thăm
- Mục đích
- Nội dung làm việc
- Danh sách khách
- Danh sách hỗ trợ
- Thời gian chuyến thăm
- Campus
- Host

---

## 3.2. Email đầu mối vẫn phải đi workflow riêng

Không cho đổi email đầu mối qua amendment hoặc safe edit.

Email đầu mối quyết định **ai đang giữ vai trò đầu mối**, nên phải qua:

- replace / transfer
- invitation
- accept / decline
- confirmation workflow

Không mở thêm “cửa thứ hai” để thay email trực tiếp.

---

## 3.3. Text tổ chức và ID partner phải đi cùng nhau

Không chấp nhận tình trạng:

```text
organization = "SeoulTech"
partnerId = 15  // nhưng id 15 là tổ chức khác
```

hoặc:

```text
organization = "SeoulTech"
organizationPartnerId = null
```

trong trường hợp người dùng vừa chọn chính SeoulTech từ dropdown.

### Quy tắc

- Gõ tự do:
  - giữ text
  - ID = `null`
- Chọn từ dropdown:
  - lấy text canonical
  - lưu đúng ID
- Sau khi đã chọn mà người dùng sửa lại text:
  - tự clear ID
  - chuyển về free text

Đây phải là hành vi giống `PartnerOrgCombobox` / `OrganizationCombobox` ở create hiện tại.

---

# 4. VÁ 1 — Sửa UI “Sửa nhanh” để có label đầy đủ

## File

`frontend/pems-react/src/features/visit-request/components/VisitSafeEditModal.tsx`

## Hiện trạng

Một số `<input>` render trực tiếp chỉ có value, không có label riêng.

Ví dụ người dùng nhìn thấy:

```text
Kim Min Jae              +821012340001
SeoulTech...              Director...
```

nhưng không có nhãn rõ:

- Họ và tên
- Số điện thoại
- Đơn vị
- Chức vụ

## Yêu cầu sửa

Mỗi input phải có label thật, không dùng placeholder thay cho label.

### Người đăng ký

Hiển thị:

- `Họ và tên`
- `Quốc tịch`
- `Tổ chức / Đối tác`
- `Chức vụ`
- `Số điện thoại`

### Đầu mối tại campus

Hiển thị rõ:

- `Họ và tên`
- `Đơn vị công tác`
- `Số điện thoại`

Nếu `Chức vụ đầu mối` vẫn thuộc amendment theo classifier hiện tại thì không được tự ý đưa vào safe-edit.

Có thể hiển thị helper:

```text
Chức vụ đầu mối được thay đổi trong "Đề xuất thay đổi".
```

nếu UX cần giải thích.

## Accessibility

- Mỗi label phải trỏ đúng `htmlFor`.
- Modal giữ `role="dialog"`.
- Không dùng placeholder làm tên trường duy nhất.
- Test bằng keyboard tab.

---

# 5. VÁ 2 — Thêm `Ghi chú gửi FPTU` vào Sửa nhanh

## Root cause

`VisitSafeEditModal.tsx` đã khởi tạo:

```ts
notes: c.notes ?? ''
```

`safeEditDiff.ts` cũng đã có logic:

```ts
if (changed(current.notes, draft.notes)) {
  patch.notes = norm(draft.notes);
}
```

Backend DTO cũng đã có `Notes`.

Nhưng UI chưa render field này.

## File sửa

`frontend/pems-react/src/features/visit-request/components/VisitSafeEditModal.tsx`

## Yêu cầu

Trong từng campus, thêm:

```text
Ghi chú gửi FPTU
[ textarea ]
```

Khuyến nghị dùng component dùng chung:

- `AutoGrowTextarea`

thay vì `<textarea>` thuần nếu phù hợp.

## Không sửa backend nếu contract hiện tại đã chạy đúng

Phải verify lại:

- `SafeEditPayload.instances[].notes`
- `SafeInstancePatchDto.Notes`
- `VisitSafeEditService`

Nếu đủ thì chỉ vá UI + test.

## Test

1. Mở Sửa nhanh.
2. Đổi chỉ `notes`.
3. Payload chỉ chứa campus vừa đổi.
4. Không gửi các campus không thay đổi.
5. Backend lưu note.
6. History / audit có entry safe edit.
7. Reload vẫn thấy note mới.

---

# 6. VÁ 3 — Thêm Quốc tịch người đăng ký vào Sửa nhanh

## Hiện trạng

Create V2 có:

```text
registrant:
- fullName
- nationality
- organization
- jobTitle
- phone
- email
```

Safe edit hiện thiếu `nationality`.

## Frontend

### 6.1. `VisitSafeEditModal.tsx`

State registrant thêm:

```ts
nationality: form.registrant.nationality
```

UI phải dùng:

```tsx
<CountrySelect ... />
```

Không dùng input text thường, để đồng nhất create/edit hiện tại.

---

### 6.2. `safeEditDiff.ts`

Sửa:

```ts
export interface SafeEditRegistrantDraft {
  fullName: string;
  nationality: string;
  organization: string;
  jobTitle: string;
  phone: string;
  partnerId: number | null;
}
```

`registrantChanged` phải tính thêm:

```text
nationality
partnerId
```

Khi có bất kỳ request-level safe field nào thay đổi, payload registrant gửi snapshot safe đầy đủ.

---

### 6.3. `visitRequestV2Api.ts`

Safe payload thêm `nationality`.

Không thêm email.

---

## Backend

### 6.4. `VisitFormV2SafeEditDtos.cs`

`SafeRegistrantPatchDto` thêm:

```csharp
string? Nationality
```

---

### 6.5. `VisitFieldClassifier.cs`

Thêm stable path:

```csharp
public const string RegistrantNationality = "request.registrant.nationality";
```

Classify:

```text
SAFE
```

---

### 6.6. `VisitSafeEditService.cs`

Thêm diff:

```text
request.RegistrantNationality
```

Normalize/trim giống các request-level field khác.

## Test

- Korea → Japan lưu được.
- Quốc tịch rỗng bị validate nếu business rule yêu cầu.
- Không làm đổi email / user identity.
- Có audit change.
- Concurrency 409 vẫn giữ nguyên.

---

# 7. VÁ 4 — Tổ chức/Đối tác trong Sửa nhanh phải có search dropdown

Đây là lỗi UX + data consistency cần vá cùng lúc.

## Hiện trạng

Create V2 đang dùng:

```tsx
<PartnerOrgCombobox
  organization={...}
  partnerId={...}
/>
```

Control này có:

- debounce search
- dropdown
- chọn existing partner
- free text
- clear `partnerId` nếu user sửa text
- badge trạng thái đã chọn partner

Nhưng `VisitSafeEditModal` đang dùng input text thường cho organization.

Kết quả:

- Không search được partner.
- Không chọn lại partner như create.
- Có thể sửa text nhưng `request.PartnerId` cũ vẫn giữ nguyên.
- Dễ tạo mismatch giữa `RegistrantOrganization` và `PartnerId`.

## Phải sửa theo hướng atomic

### 7.1. Frontend UI

File:

`VisitSafeEditModal.tsx`

Thay input organization người đăng ký bằng:

```tsx
<PartnerOrgCombobox
  organization={registrant.organization}
  partnerId={registrant.partnerId}
  onChange={next => {
    setRegistrant(prev => ({
      ...prev,
      organization: next.organization,
      partnerId: next.partnerId,
    }));
  }}
/>
```

Không cần gửi `partnerSelectionMode` xuống backend safe-edit nếu backend có thể suy ra:

```text
partnerId != null => existing partner
partnerId == null => free text
```

---

### 7.2. Khởi tạo state partner

Dùng:

```text
form.partnerId
```

từ `ResolvedVisitForm`.

Không suy partner bằng tên.

---

### 7.3. Safe payload

`SafeRegistrantPatchDto` / FE payload thêm:

```text
PartnerId
```

Partner ID phải đi cùng request-level registrant block.

### Vì sao không để `PartnerId` ở ngoài?

Vì người dùng đang chỉnh tổ chức người đăng ký.

Nếu organization đổi mà partner link không đổi thì dữ liệu sai.

Hai giá trị phải nằm trong cùng một mutation / transaction.

---

## Backend xử lý partner

### 7.4. Không trust text + id từ client

Nếu `PartnerId != null`:

1. Query partner bằng policy dành cho request form.
2. Bắt buộc partner hợp lệ.
3. Lấy canonical display name server-side.
4. Set:
   - `request.PartnerId`
   - `request.RegistrantOrganization`

Không nhận một id nhưng lưu text tùy ý khác partner.

Nếu `PartnerId == null`:

- `request.PartnerId = null`
- lưu `RegistrantOrganization` từ free text đã trim.

---

### 7.5. Đồng nhất policy search và write

Dropdown request form hiện phải chỉ dùng partner phù hợp với request-form audience.

Không để UI search một set nhưng backend accept một set khác.

Nên centralize:

```text
ACTIVE
+ APPROVED
+ PUBLIC
```

cho request form nếu đây là rule đang dùng ở endpoint search.

Khuyến nghị tái sử dụng:

`GuestOrganizationPartnerPolicy.RequestFormSelectable(...)`

hoặc tách policy tên tổng quát hơn nếu request-level partner không nên phụ thuộc class tên `GuestOrganizationPartnerPolicy`.

### Không được

```text
UI chỉ hiện PUBLIC
nhưng API handcrafted lại lưu PRIVATE/PENDING
```

---

### 7.6. Classifier

Nếu request-level partner association được phép thay đổi cùng organization qua safe edit, thêm path:

```text
request.partnerId
```

class:

```text
SAFE
```

Lý do:

- organization đã đang là SAFE.
- partnerId là identity đi kèm chính organization đó.
- không thể cho đổi text mà giữ ID cũ.

Nếu team quyết định `partnerId` không được phép đổi sau approval thì phải làm ngược lại:

- khóa cả organization tương ứng,
- không được tiếp tục cho sửa organization free-text một mình.

**Không chấp nhận trạng thái nửa vời.**

---

## Test bắt buộc

### Case A — chọn existing partner

Ban đầu:

```text
organization = Old Org
partnerId = null
```

User gõ:

```text
Seoul
```

Dropdown hiện partner hợp lệ.

Chọn:

```text
SeoulTech Global Engagement Center
```

Sau save:

```text
RegistrantOrganization = canonical SeoulTech...
PartnerId = đúng id
```

---

### Case B — sửa text sau khi đã chọn

Ban đầu:

```text
organization = SeoulTech
partnerId = 15
```

User sửa thành:

```text
SeoulTech Custom Unit
```

Kết quả:

```text
partnerId = null
organization = "SeoulTech Custom Unit"
```

---

### Case C — invalid partner id bằng direct API

Gửi `partnerId`:

- không tồn tại
- PENDING
- REJECTED
- PRIVATE
- inactive

Backend phải refuse theo policy.

Không được dựa vào frontend.

---

# 8. VÁ 5 — Organization dropdown cho đầu mối trong Sửa nhanh

Field:

```text
Đơn vị công tác của đầu mối
```

hiện là text thường.

Nên tái sử dụng:

```tsx
<OrganizationCombobox />
```

### Lưu ý

Operational contact hiện không có `organizationPartnerId`.

Do đó ở field này:

- dropdown chỉ giúp search và điền canonical text.
- không persist partner identity cho đầu mối nếu schema chưa có cột tương ứng.
- `partnerId` callback có thể được bỏ qua ở field text-only.

Không được giả vờ rằng đầu mối đã được link partner nếu DB không lưu ID đó.

---

# 9. VÁ 6 — Hoàn thiện UI Đề xuất thay đổi

## File

`frontend/pems-react/src/features/visit-request/components/VisitAmendmentSubmitModal.tsx`

## Hiện trạng đầu mối

State `opContact` chứa full object, nhưng UI hiện chưa render đủ.

## Phải hiển thị

- Họ và tên
- Đơn vị công tác
- Chức vụ
- Số điện thoại
- Email: read-only

### Email

Hiển thị nhưng không editable:

```text
Email đầu mối
abc@company.com
Email được quản lý qua chức năng Chỉnh sửa đầu mối.
```

Không gửi email thay đổi qua amendment.

Backend hiện đã refuse email khác, giữ nguyên guard.

---

## Organization đầu mối

Không dùng input text thường.

Dùng:

```tsx
<OrganizationCombobox />
```

text-only vì operational contact hiện không có partner identity riêng.

---

# 10. VÁ 7 — Amendment: danh sách khách / hỗ trợ phải search tổ chức như create

## Hiện trạng

Create / pending edit hiện dùng:

```tsx
<OrganizationCombobox
  value={...}
  partnerId={organizationPartnerId}
  ...
/>
```

Amendment hiện dùng input text thường cho:

```text
organization
```

và `EditableMember` còn làm mất `organizationPartnerId`.

## Phải sửa

### 10.1. `EditableMember`

Từ:

```ts
interface EditableMember {
  key: string;
  fullName: string;
  jobTitle: string;
  organization: string;
  nationality: string;
}
```

thành tối thiểu:

```ts
interface EditableMember {
  key: string;
  clientMemberKey: string;
  fullName: string;
  jobTitle: string;
  organization: string;
  organizationPartnerId: number | null;
  nationality: string;
}
```

---

### 10.2. Clone current member

Phải giữ:

```text
organizationPartnerId
```

Không clone text rồi vứt ID.

---

### 10.3. Organization editor

Trong member editor:

```tsx
<OrganizationCombobox
  value={member.organization}
  partnerId={member.organizationPartnerId}
  searchMode="REQUEST_FORM"
  onChange={(value, pickedPartnerId) => {
    updateMember({
      organization: value,
      organizationPartnerId: pickedPartnerId,
    });
  }}
/>
```

### Behavior

- chọn partner → lưu text + id
- gõ tự do → id null
- sửa text sau khi chọn → id null

---

### 10.4. Nationality editor

Không dùng input text thường.

Tái sử dụng:

```tsx
<CountrySelect />
```

để amendment nhất quán với create/pending edit.

---

# 11. VÁ 8 — Amendment payload phải giữ `organizationPartnerId`

## Frontend

`AmendmentProposalPayload` visitor/support member phải gửi:

```text
organizationPartnerId
clientMemberKey
```

không chỉ:

```text
fullName
jobTitle
organization
nationality
```

---

## Backend hiện có lợi thế

`VisitorDto` và `SupportTeamMemberDto` đã có:

```text
OrganizationPartnerId
ClientMemberKey
```

`StageReplaceMembers()` cũng đã set:

```text
OrganizationPartnerId = ...
```

Nên backend data model cơ bản đã hỗ trợ.

## Phải bảo đảm

Khi amendment chỉ sửa một field khác của member:

```text
OrganizationPartnerId cũ không được biến thành null.
```

---

## Test quan trọng

Ban đầu:

```text
Guest:
name = Kim
organization = SeoulTech
organizationPartnerId = 15
```

User amendment chỉ sửa:

```text
jobTitle
```

Sau approve:

```text
organizationPartnerId vẫn = 15
```

Không chấp nhận test chỉ nhìn text.

Phải query DB / DTO xác minh ID.

---

# 12. VÁ 9 — Backend validate partner member trong Amendment

Hiện create/edit đã có policy cho `organizationPartnerId`.

Amendment cũng phải gọi cùng rule trước khi lưu proposal.

## Trong `VisitAmendmentService.SubmitAsync`

Trước khi tạo amendment:

Collect:

```text
proposal.Visitors[].OrganizationPartnerId
proposal.ExternalSupportMembers[].OrganizationPartnerId
```

Validate một lần theo set:

```text
GuestOrganizationPartnerPolicy.EnsureRequestFormSelectableAsync(...)
```

Không query N lần cho N thành viên.

## Direct API tamper

Partner không hợp lệ phải bị chặn trước khi amendment được lưu.

---

# 13. VÁ 10 — Giữ liên kết “Đầu mối là ai trong đoàn?” qua Amendment

Đây là phần không được sửa nửa vời.

## Vấn đề hiện tại

Create đã dùng stable `clientMemberKey`.

Nhưng amendment:

- clone member bằng key UI tự tạo
- payload hiện chưa truyền đầy đủ client key
- khi approve và replace member, backend tạo member row mới
- `LinkMembers(...)` hiện không có durable reference của người được chọn làm đầu mối
- backend có thể phải fallback bằng fingerprint/name/org/title

Điều này có thể sai nếu:

- đổi tên
- đổi chức vụ
- đổi organization
- có hai người gần giống nhau
- guest/support đổi danh sách

## Yêu cầu

Amendment mới không được phụ thuộc string guessing để xác định contact member.

---

# 14. Thiết kế đề xuất cho durable contact member reference

Không lưu raw array index từ UI trong lúc đang edit.

Frontend vẫn dùng stable `clientMemberKey`.

## Bước 1 — Frontend

Trong amendment modal:

- mỗi member có `clientMemberKey`
- picker:
  - Khách
  - Nhân sự hỗ trợ
  - Không thuộc đoàn

State:

```text
operationalContactClientMemberKey
```

Khi chọn member:

- copy name/jobTitle/organization sang snapshot đầu mối nếu UX hiện hành yêu cầu
- phone/email không tự blank
- key vẫn là identity

---

## Bước 2 — Submit backend resolve key ngay

Backend nhận full proposed member arrays + key.

Trước khi lưu amendment:

1. Tìm chính xác member proposal có `ClientMemberKey`.
2. Refuse nếu:
   - không tồn tại
   - trùng key
   - member không hợp lệ
3. Chuyển key sang reference bền cho amendment.

### Khuyến nghị persist semantic reference

Không cần giữ raw key lâu dài.

Sau khi proposal đã immutable, có thể persist:

```text
ContactMemberLinkMode:
- MEMBER
- OUTSIDE_DELEGATION
- LEGACY_UNSPECIFIED

ProposedContactMemberType:
- GUEST
- EXTERNAL_SUPPORT

ProposedContactMemberOrdinal:
- 0..N
```

Vì sau submit amendment, member arrays là immutable snapshot nên ordinal trong chính proposal đó là ổn định.

Đây khác hoàn toàn với dùng array index trong form đang edit.

---

# 15. DB migration đề xuất cho amendment contact link

Bảng:

```text
visit_instance_amendments
```

Thêm ví dụ:

```sql
ALTER TABLE visit_instance_amendments
  ADD COLUMN proposed_contact_member_link_mode VARCHAR(30) NULL,
  ADD COLUMN proposed_contact_member_type VARCHAR(30) NULL,
  ADD COLUMN proposed_contact_member_ordinal INT NULL;
```

### Ý nghĩa

`MEMBER`

```text
type + ordinal bắt buộc
```

`OUTSIDE_DELEGATION`

```text
type = null
ordinal = null
```

`LEGACY_UNSPECIFIED`

Dùng cho amendment cũ trước migration.

Có thể dùng nullable + default phù hợp schema hiện tại, nhưng phải phân biệt được:

```text
user chủ động chọn "không thuộc đoàn"
```

với:

```text
amendment cũ không hề có metadata này
```

---

# 16. Apply amendment sau khi approve

Trong `VisitAmendmentService.ApproveAsync`:

## Nếu member lists thay đổi

Sau `StageReplaceMembers()` và flush tạo ID:

- lấy đúng member mới theo `type + ordinal`
- set:

```text
detail.OperationalContactGuestMemberId
```

## Nếu member lists không thay đổi

- lấy member hiện tại của instance theo type + order
- set đúng existing `guest_member_id`

## Nếu mode = OUTSIDE_DELEGATION

```text
OperationalContactGuestMemberId = null
```

Snapshot contact vẫn giữ nguyên.

## Nếu mode = LEGACY_UNSPECIFIED

Chỉ amendment cũ mới được phép dùng fallback legacy:

```text
OperationalContactLink.Resolve(detail, members)
```

### Quy tắc

> Amendment mới tuyệt đối không được im lặng fallback từ stable identity sang string guess.

---

# 17. VÁ 11 — Không để member partner link mất khi approve Amendment

Sau khi sửa FE payload, backend approve phải verify:

```text
StageReplaceMembers(
  visitors with organizationPartnerId,
  support with organizationPartnerId
)
```

và sau flush:

```text
LinkMembers(...)
```

Khi link lại operational contact, không được làm mất:

- member type
- organizationPartnerId
- contact member relation

---

# 18. VÁ 12 — i18n

Phải sửa cả:

- `frontend/pems-react/src/shared/i18n/locales/vi/visitRequestV2.json`
- `frontend/pems-react/src/shared/i18n/locales/en/visitRequestV2.json`

## Key cần rà

### Safe edit

- registrant full name
- nationality
- organization / partner
- job title
- phone
- notes
- contact field labels
- partner search helper
- field-specific error

### Amendment

- contact organization
- contact job title
- contact email read-only helper
- contact member picker
- member organization search
- nationality
- partner selected/free text/no result/searching

Không hard-code Vietnamese trực tiếp vào component mới.

---

# 19. VÁ 13 — Validation

## Frontend

### Safe edit nationality

- required nếu backend yêu cầu.
- dùng `CountrySelect`.

### Partner search

- debounce.
- minimum input behavior giống component hiện tại.
- race-safe.
- clear id khi user sửa text.

### Amendment member

Mỗi row vẫn validate:

- fullName
- jobTitle
- organization
- nationality

Không cho `organizationPartnerId` tự biến thành ID không còn phù hợp với text.

---

## Backend

Backend là authority.

Phải revalidate:

- partner id tồn tại
- partner trạng thái được dùng trong request form
- contact member reference hợp lệ
- client key không duplicate
- selected member thực sự nằm trong proposal
- row version
- amendment lifecycle
- cutoff
- base revision

---

# 20. VÁ 14 — Audit / history

## Safe edit

Nếu đổi:

```text
RegistrantNationality
PartnerId
RegistrantOrganization
```

audit phải ghi đúng.

Không ghi raw internal object khó đọc nếu history hiện có field mapper.

## Amendment

Nếu đổi “đầu mối thuộc member nào”:

history nên phản ánh bằng label người dùng hiểu được, ví dụ:

```text
Đầu mối trong đoàn:
Kim Min Jae → Park Ji Hoon
```

Không hiển thị:

```text
clientMemberKey
ordinal
guestMemberId
```

cho end user.

Các ID chỉ phục vụ internal identity/audit kỹ thuật.

---

# 21. Danh sách file dự kiến sửa

## Frontend bắt buộc

```text
frontend/pems-react/src/features/visit-request/components/VisitSafeEditModal.tsx
frontend/pems-react/src/features/visit-request/components/VisitAmendmentSubmitModal.tsx
frontend/pems-react/src/features/visit-request/api/visitRequestV2Api.ts
frontend/pems-react/src/features/visit-request/utils/safeEditDiff.ts
frontend/pems-react/src/shared/i18n/locales/vi/visitRequestV2.json
frontend/pems-react/src/shared/i18n/locales/en/visitRequestV2.json
```

Có thể tái sử dụng, không duplicate:

```text
frontend/pems-react/src/features/visit-request/components/shared/PartnerOrgCombobox.tsx
frontend/pems-react/src/features/visit-request/components/shared/OrganizationCombobox.tsx
frontend/pems-react/src/features/visit-request/components/shared/CountrySelect.tsx
```

Không copy-paste một combobox mới nếu component hiện tại đáp ứng được.

---

## Backend bắt buộc

```text
backend/PEMS.Application/Common/DTOs/VisitFormV2SafeEditDtos.cs
backend/PEMS.Application/Common/DTOs/VisitAmendmentDtos.cs
backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs
backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs
backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs
```

Có thể phải chỉnh:

```text
backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs
backend/PEMS.Application/Partners/Common/GuestOrganizationPartnerPolicy.cs
backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs
backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs
```

nếu centralize request-level partner selection rule.

---

## Database

Nếu triển khai durable member reference cho amendment:

```text
docs/database/scripts/migrations/<new_migration>.sql
```

Không sửa file schema canonical theo cách phá lịch sử nếu dự án đang dùng migration append-only.

Migration phải:

- idempotent nếu convention dự án yêu cầu
- có comment cột
- có backfill/default an toàn cho amendment cũ
- không tự đoán contact relation cho historical amendment

---

# 22. Test frontend phải thêm/cập nhật

## Safe edit

Target:

```text
frontend/pems-react/src/features/visit-request/__tests__/VisitV2Modals.test.tsx
```

hoặc split file nếu test quá lớn.

### Test cases

1. Render label cho từng field.
2. Render `Ghi chú gửi FPTU`.
3. Render `CountrySelect` quốc tịch.
4. Render partner search cho organization.
5. Search >= minimum length gọi API.
6. Select partner lưu đúng id.
7. Edit text sau select clear id.
8. Free text gửi partnerId null.
9. No changes không gọi API.
10. Notes-only patch chỉ gửi campus touched.
11. Locked campus không cho sửa.
12. Contact organization search hoạt động.
13. Keyboard/accessibility.

---

## Amendment

1. Render organization + jobTitle + phone + name của contact.
2. Email contact read-only.
3. Member organization dùng dropdown.
4. Member nationality dùng CountrySelect.
5. Existing `organizationPartnerId` được restore.
6. Chọn partner khác cập nhật id.
7. Gõ free text clear id.
8. Submit payload giữ `organizationPartnerId`.
9. Contact member picker gồm GUEST + EXTERNAL_SUPPORT.
10. Key selected tồn tại trong payload.
11. Xóa member đang giữ contact phải được xử lý rõ.
12. Không tự reassign contact sang người cùng tên.

---

# 23. Test backend bắt buộc

## Safe edit integration

File hiện có:

```text
tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs
```

Thêm case:

### SE-01

Sửa nationality → apply ngay.

### SE-02

Select existing partner → update `PartnerId` + canonical organization.

### SE-03

Free text organization → `PartnerId = null`.

### SE-04

Partner không selectable → reject.

### SE-05

Organization/partner update có audit + revision.

### SE-06

Concurrent safe edit → 409.

### SE-07

Notes-only → sibling campus không bị bump ngoài ý muốn.

---

## Amendment integration

File hiện có:

```text
tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs
```

Thêm:

### AM-01

Member organizationPartnerId được giữ khi chỉ sửa job title.

### AM-02

Đổi member partner bằng selectable partner → approve → đúng ID.

### AM-03

Invalid partner id → reject ở submit.

### AM-04

Contact = visitor A, amendment sửa tên A → approve → contact vẫn link đúng A.

### AM-05

Contact = support member B → approve → vẫn đúng B.

### AM-06

Contact chọn OUTSIDE_DELEGATION → link null, snapshot còn.

### AM-07

Stale/made-up client key → reject.

### AM-08

Duplicate client key → reject.

### AM-09

Không có member list change nhưng đổi contact selection → approve đúng relation.

### AM-10

Có add/remove member + đổi contact → relation đúng member mới.

### AM-11

Amendment cũ không metadata → legacy fallback còn hoạt động nếu cần backward compatibility.

---

# 24. Không được coi UI test là đủ

Các lỗi này liên quan đến ID / relationship.

Do đó test phải kiểm tra DB hoặc response model.

Ví dụ không được chỉ assert:

```text
"SeoulTech" xuất hiện trên màn hình
```

Phải assert thêm:

```text
organization_partner_id = 15
```

hoặc:

```text
visit_request.partner_id = 15
```

và:

```text
operational_contact_guest_member_id = đúng member
```

---

# 25. Acceptance Criteria

Chỉ báo PASS khi đạt toàn bộ:

- [ ] Sửa nhanh có label rõ cho từng input.
- [ ] Sửa nhanh có `Ghi chú gửi FPTU`.
- [ ] Sửa nhanh có Quốc tịch người đăng ký.
- [ ] Quốc tịch dùng country dropdown, không input text thô.
- [ ] Tổ chức người đăng ký trong Sửa nhanh có search dropdown.
- [ ] Chọn partner từ dropdown lưu đúng `PartnerId`.
- [ ] Gõ text tự do clear `PartnerId`.
- [ ] Backend không cho text và partnerId mismatch.
- [ ] Đơn vị đầu mối có organization search.
- [ ] Amendment hiển thị đủ organization + jobTitle của đầu mối.
- [ ] Email đầu mối vẫn read-only / workflow riêng.
- [ ] Member organization trong amendment có search dropdown.
- [ ] Member nationality trong amendment dùng CountrySelect.
- [ ] `organizationPartnerId` được giữ xuyên suốt amendment.
- [ ] Direct API không thể gửi partner không hợp lệ.
- [ ] Amendment có stable/durable contact-member reference.
- [ ] Amendment mới không fallback string guess nếu stable reference bị lỗi.
- [ ] GUEST và EXTERNAL_SUPPORT đều có thể là contact.
- [ ] Không tự đổi contact sang người khác khi reorder/add/remove.
- [ ] Audit/history đúng.
- [ ] VI/EN đầy đủ.
- [ ] Unit tests pass.
- [ ] Integration tests pass.
- [ ] Frontend tests pass.
- [ ] Lint pass.
- [ ] Build pass.

---

# 26. Regression bắt buộc kiểm tra

Sau patch phải retest:

1. Public create V2.
2. Authenticated create V2.
3. Pending edit.
4. Per-campus pending edit.
5. Resubmit.
6. Safe edit.
7. Amendment submit.
8. Amendment approve.
9. Amendment reject.
10. Contact replace/transfer.
11. Partner search.
12. Member import Excel.
13. Member duplicate detection.
14. Minutes autofill.
15. History timeline.
16. Multi-campus request.

Không được fix amendment làm hỏng create/pending edit.

---

# 27. Gate chạy cuối

Backend:

```bash
dotnet test
```

Nếu solution lớn, chạy tối thiểu:

```text
PEMS.UnitTests
PEMS.IntegrationTests
architecture tests nếu project có
```

Frontend:

```bash
npm run lint
npm run test:unit
npm run build
```

Nếu repo có real-stack/E2E liên quan visit:

```text
chạy suite visit-request phù hợp
```

Không báo `PASS` nếu chỉ compile.

---

# 28. Yêu cầu triển khai cho dev / AI agent

1. Đọc code thật trên `Dev` trước khi sửa.
2. Không suy đoán tên field/endpoint.
3. Không tạo component partner search mới nếu component hiện tại tái sử dụng được.
4. Không hard-code data mock.
5. Không bỏ validation backend vì frontend đã validate.
6. Không dùng string matching làm identity cho amendment mới.
7. Không làm mất `organizationPartnerId`.
8. Không cho email đầu mối đi qua amendment.
9. Không sửa sibling campus ngoài scope.
10. Giữ optimistic concurrency.
11. Có audit/revision.
12. Có test chứng minh DB relationship.
13. Báo chính xác file đã sửa.
14. Báo chính xác test đã chạy và kết quả.
15. Nếu gặp rule mâu thuẫn, dừng và nêu bằng chứng code thay vì tự quyết business rule.

---

# 29. Kết quả mong muốn sau vá

## Sửa nhanh

Người dùng nhìn thấy rõ:

```text
Người đăng ký
- Họ và tên
- Quốc tịch
- Tổ chức / Đối tác   ← có search dropdown
- Chức vụ
- Số điện thoại

FPT University Hà Nội
- Di chuyển
- Đầu mối
  - Họ tên
  - Đơn vị công tác   ← có search dropdown
  - Số điện thoại
- Đồng ý truyền thông
- Ghi chú gửi FPTU
```

---

## Đề xuất thay đổi

Người dùng có:

```text
Tên đoàn
Loại hình
Thời gian
Ngôn ngữ
Mục đích
Nội dung

Danh sách khách
- Họ tên
- Chức vụ
- Tổ chức/đối tác     ← search dropdown + giữ organizationPartnerId
- Quốc tịch           ← CountrySelect

Nhân sự hỗ trợ
- tương tự

Đầu mối
- Đầu mối là ai trong đoàn?
- Họ tên
- Đơn vị              ← search dropdown
- Chức vụ
- Điện thoại
- Email               ← read-only

Lý do đề xuất
```

Sau approve:

```text
text đúng
partner ID đúng
member ID đúng
contact-member link đúng
history đúng
```

---

# 30. Điều kiện không được chấp nhận

Không merge nếu còn một trong các tình trạng:

```text
UI search được nhưng backend không validate ID
```

```text
UI hiện đúng tên partner nhưng DB mất organizationPartnerId
```

```text
Sửa organization nhưng PartnerId cũ vẫn giữ
```

```text
Amendment approve xong contact được đoán lại bằng tên
```

```text
Member đổi jobTitle nhưng partner link biến thành null
```

```text
Email đầu mối bị sửa trực tiếp qua amendment
```

```text
Test chỉ assert UI text mà không assert relationship
```

---

# 31. Ưu tiên triển khai

## P0

1. Label Sửa nhanh.
2. Ghi chú gửi FPTU.
3. Quốc tịch người đăng ký.
4. Partner search + atomic `organization/partnerId` ở Safe Edit.
5. Amendment contact organization + job title.
6. Amendment member OrganizationCombobox.
7. Preserve `organizationPartnerId`.

## P1

8. Durable contact-member reference cho Amendment.
9. Migration/backward compatibility.
10. Audit/history.

## P2

11. UX polish.
12. Accessibility.
13. Full regression / real-stack.

---

## Kết luận

Bản vá không chỉ nhằm “thêm vài ô còn thiếu”.

Phần quan trọng nhất là bảo đảm ba lớp luôn khớp nhau:

```text
UI hiển thị
        ↓
Payload / business identity
        ↓
Database relationship
```

Đặc biệt với tổ chức/đối tác:

```text
organization text ↔ partnerId / organizationPartnerId
```

và đầu mối:

```text
contact snapshot ↔ đúng guest_member_id
```

Sau patch, người dùng phải có trải nghiệm search dropdown đồng nhất với create/edit hiện tại, đồng thời hệ thống không được âm thầm mất hoặc giữ sai các ID liên kết khi sửa dữ liệu.
