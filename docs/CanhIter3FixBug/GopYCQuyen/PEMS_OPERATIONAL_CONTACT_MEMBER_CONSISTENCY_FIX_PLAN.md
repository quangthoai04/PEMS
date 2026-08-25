# PEMS — Operational Contact ↔ Delegation Member Consistency Fix Plan

**Mục tiêu:** chuẩn hóa toàn bộ logic giữa **Đầu mối đoàn khách** (`Operational Contact`) và **thành viên trong danh sách đoàn** (`VisitGuestMember`) để không còn trạng thái mâu thuẫn, không xóa nhầm người đang là đầu mối, không đổi người đầu mối bằng một dropdown quan hệ, và không tạo đường vòng vượt qua workflow phê duyệt / chuyển giao.

**Repository:** `quangthoai04/PEMS`  
**Baseline đã rà soát:** commit `bb3e5c545fd68759b60bbf9dabf25c0433cc5161`  
**Ngày lập kế hoạch:** 25/08/2026

---

# 1. Kết luận thiết kế cần chốt trước khi code

Sau khi lần theo Create, Pending Edit, Safe Edit, Amendment và Contact Transfer, phương án thực tế nhất **không phải** là để mọi màn hình đều tự do sửa cả member và contact hai chiều.

Lý do: sau khi campus đã được duyệt, **danh sách đoàn là nội dung đã được phê duyệt**. Nếu `Sửa nhanh` sửa Operational Contact rồi tự động sửa luôn `VisitGuestMember`, hệ thống sẽ vô tình thay đổi nội dung đoàn mà **không qua Amendment / approval**.

Vì vậy contract cuối cùng nên là:

> **Relation nói “đây là cùng một người”. Identity change và content approval vẫn là hai nghiệp vụ khác nhau.**

## 1.1. Ba loại thao tác phải tách rõ

### A. Sửa nội dung đoàn

Ví dụ:

- tên khách;
- chức vụ khách;
- đơn vị của khách;
- quốc tịch;
- thêm/xóa thành viên.

Đi qua:

- **Pending Edit** khi campus còn chờ quyết định;
- **Amendment** sau khi campus đã được duyệt.

### B. Sửa metadata của cùng một Operational Contact

Ví dụ:

- số điện thoại;
- sửa snapshot cho khớp lại với member;
- link/unlink với danh sách đoàn.

Đi qua:

- **Safe Edit / Operational Contact Profile**.

### C. Đổi sang một con người khác làm Operational Contact

Ví dụ:

- Kim Min Jae → Moon Jae Sung;
- email đầu mối thay đổi.

Đi qua:

- **Replace / Transfer / Invitation / Confirmation**.

Không được thực hiện bằng relation picker.

---

# 2. Invariant nghiệp vụ cuối cùng

## 2.1. Contact không nằm trong đoàn

```text
OperationalContactGuestMemberId = null
```

Operational Contact là snapshot độc lập.

Cho phép sửa:

- FullName
- Organization
- JobTitle
- Phone

Email vẫn khóa; đổi email phải qua Replace/Transfer.

## 2.2. Contact nằm trong đoàn

```text
OperationalContactGuestMemberId = X
```

`X` phải là một member:

- tồn tại;
- thuộc đúng VisitRequest;
- thuộc đúng VisitInstance;
- đúng campus;
- `GUEST` hoặc `EXTERNAL_SUPPORT`.

Relation có nghĩa:

> Operational Contact và member X là **cùng một con người**.

## 2.3. Ba field dùng chung

Các field dùng chung giữa contact snapshot và member:

```text
FullName
JobTitle
Organization
```

Các field riêng:

```text
Contact only:
- Phone
- Email

Member only:
- Nationality
- OrganizationPartnerId
- MemberType
- DisplayOrder
```

## 2.4. Không cho relation trỏ sang người khác

Ví dụ:

```text
Contact snapshot:
Kim Min Jae
Director
SeoulTech

Member được chọn:
Moon Jae Sung
Protocol Officer
Jeju Tourism Technology Institute
```

Phải từ chối.

Nếu người dùng thật sự muốn Moon trở thành đầu mối:

```text
Chuyển đầu mối
→ invitation
→ confirmation
→ apply
```

## 2.5. Không được xóa member đang là contact

Nếu:

```text
OperationalContactGuestMemberId = 123
```

thì `GuestMemberId = 123` không được biến mất qua:

- nút Delete;
- Excel Replace;
- Replace Both;
- Apply To All;
- handcrafted API request;
- Amendment;
- Pending Edit.

Muốn xóa phải xử lý relation trước:

```text
Unlink
hoặc
Transfer
```

---

# 3. Điều chỉnh quan trọng so với ý tưởng “auto-sync hai chiều mọi lúc”

Không triển khai rule:

```text
Safe Edit contact
→ tự sửa luôn member sau khi campus đã duyệt
```

vì đây là đường vòng thay đổi approved delegation content mà không qua Amendment.

Thay vào đó dùng rule theo lifecycle:

| Lifecycle / workflow | Hành vi |
|---|---|
| Create | Member được chọn là source cho 3 shared fields của contact |
| Pending Edit | Nếu sửa **member đang linked**, sau save backend sync 3 shared fields Member → Contact |
| Amendment Submit | Chưa đụng active Contact |
| Amendment Approve | Nếu member đang linked được thay đổi, sync 3 shared fields Member → Contact trong cùng transaction |
| Safe Edit + contact unlinked | Cho sửa FullName/Org/JobTitle/Phone |
| Safe Edit + contact linked | Phone sửa trực tiếp; 3 shared fields không cho nhập tùy ý |
| Safe Edit + legacy mismatch | Cho action **Đồng bộ theo thành viên** |
| Relation change | Chỉ link tới member có identity phù hợp hoặc unlink |
| Identity change | Replace / Transfer |

Điều này đảm bảo:

1. dữ liệu thực tế nhất quán;
2. không bypass approval;
3. không có hai workflow cùng quyền đổi người đầu mối;
4. không phá lịch sử / revision semantics hiện tại.

---

# 4. Các vấn đề xác minh trong code hiện tại

## 4.1. Create đã có logic đúng

File:

```text
frontend/pems-react/src/features/visit-request/components/v2/CampusVisitCard.tsx
backend/PEMS.Application/Delegations/Common/OperationalContactLink.cs
```

Create đang:

- giữ `operationalContactClientMemberKey`;
- đồng bộ member được chọn sang contact draft;
- backend có `ApplySnapshotFromMember(...)`.

Giữ nguyên nguyên tắc này.

---

## 4.2. Pending Edit đang để contact read-only nhưng relation vẫn xuất hiện

File:

```text
CampusVisitCard.tsx
```

Existing campus dùng:

```text
contactReadOnly = true
```

nhưng hiện tại vẫn còn logic relation-only.

Điều này gây UX khó hiểu:

> form nói contact không sửa ở đây nhưng lại cho chọn một member khác dưới contact.

Phải xóa relation picker khỏi Pending Edit của existing campus.

---

## 4.3. Pending Edit đang bảo vệ direct delete

Trong `CampusVisitCard.tsx` đã có:

```text
memberHoldsContactRole(...)
```

và chặn `remove(rowIndex)` khi row là contact.

Giữ guard này.

Tuy nhiên bulk path như Excel Replace hiện thiên về:

```text
clear relation / warning
```

Thay vì invariant cứng.

Phải harden.

---

## 4.4. Backend Pending Edit vẫn hỗ trợ relation-only update

File:

```text
backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs
```

Hiện có:

```text
relationChanged
```

và direct assignment:

```text
detail.OperationalContactGuestMemberId =
    content.OperationalContactGuestMemberId;
```

Đây là nguyên nhân Pending Edit có thể trở thành một “cửa thứ hai” để quản lý relation.

Phải loại bỏ khả năng **user-driven relation change** khỏi Pending Edit.

Pending Edit chỉ được:

- preserve relation;
- re-point relation kỹ thuật sau copy-on-write;
- sync snapshot từ **cùng member** sau khi member đó được sửa.

---

## 4.5. Safe Edit hiện enforce fingerprint trên mọi contact edit

File:

```text
backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs
```

Hiện logic dùng:

```text
PersonIdentity.Key(contact)
==
PersonIdentity.Key(member)
```

trên effective relation.

Điều này đúng khi **đang tạo/chuyển relation**, nhưng quá mạnh khi chỉ sửa `Phone`.

Ví dụ legacy / prior edit:

```text
Contact:
Kim / Director

Member:
Kim / Senior Director
```

User chỉ sửa phone cũng bị block.

Phải đổi validation thành **operation-aware**, không phải “mỗi lần save đều bắt fingerprint bằng nhau”.

---

## 4.6. Detail page bỏ sót capability `UPDATE_OPERATIONAL_CONTACT_PROFILE`

File:

```text
frontend/pems-react/src/features/visit-request/components/v2/VisitRequestV2DetailView.tsx
```

Hiện `canOpenSafeEdit` chủ yếu dựa trên:

```text
SUBMIT_SAFE_EDIT
```

trong khi backend có thể grant:

```text
UPDATE_OPERATIONAL_CONTACT_PROFILE
```

cho:

```text
WAITING_CONTACT_CONFIRMATION
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
```

Kết quả:

```text
backend cho sửa contact
nhưng UI không mở được Sửa nhanh
```

Phải fix.

---

# 5. Kiến trúc nghiệp vụ sau khi fix

```text
                         CREATE
                           │
              ┌────────────┴─────────────┐
              │                          │
       Contact in delegation       Contact outside
              │                          │
      relation -> member                 │
      member -> contact snapshot         │
              │                          │
              └────────────┬─────────────┘
                           │
                    WAITING APPROVAL
                           │
          ┌────────────────┼────────────────┐
          │                │                │
    Edit member       Edit relation     Change person
          │                │                │
     Pending Edit       Safe Edit       Replace/Transfer
          │                │                │
 linked member ->         exact-match       confirmation
 contact 3 fields         or unlink
          │
          ▼
       APPROVED
          │
    ┌─────┴───────────────┐
    │                     │
Amend member         Edit contact metadata
    │                     │
Amendment                 Safe Edit
    │                     │
Approve              phone / relation repair
    │
member -> contact
(shared 3 fields)
```

---

# 6. Backend implementation plan

# 6.1. `OperationalContactLink.cs` — tạo shared domain primitives

File:

```text
backend/PEMS.Application/Delegations/Common/OperationalContactLink.cs
```

## Thêm helper 1 — resolve linked member

Conceptual:

```csharp
public static VisitGuestMember? FindLinkedMember(
    VisitInstanceFormDetail detail,
    IReadOnlyList<VisitGuestMember> members)
```

Behavior:

```text
relation null
→ null

relation != null nhưng không có member
→ throw MemberNotFound

wrong member type
→ throw MemberNotEligible

valid
→ return member
```

Không fuzzy-match.

---

## Thêm helper 2 — sync contact snapshot FROM linked member

Conceptual:

```csharp
public static bool SyncSnapshotFromLinkedMember(
    VisitInstanceFormDetail detail,
    VisitGuestMember member)
```

Chỉ copy:

```text
OperationalContactFullName      <- member.FullName
OperationalContactJobTitle      <- member.JobTitle
OperationalContactOrganization  <- member.Organization
```

Không copy:

```text
Phone
Email
OperationalContactUserId
ConfirmedAt
ConfirmationSource
```

Return `true` nếu có field thật sự thay đổi.

---

## Thêm helper 3 — exact identity check khi tạo relation

Conceptual:

```csharp
public static void EnsureRelationMatchesContact(
    VisitInstanceFormDetail detail,
    VisitGuestMember member)
```

So sánh:

```csharp
PersonIdentity.Key(
    detail.OperationalContactFullName,
    detail.OperationalContactJobTitle,
    detail.OperationalContactOrganization
)
```

với member.

Mismatch:

```text
OPERATIONAL_CONTACT_RELATION_PROFILE_MISMATCH
```

---

# 6.2. `VisitRequestV2EditService.cs` — Pending Edit

File:

```text
backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs
```

## Mục tiêu

Pending Edit không còn là cửa user-driven relation management.

### Giữ:

```text
EnsureContactSnapshotUnchanged(...)
```

để client không tự gửi contact profile mới.

### Bỏ:

logic relation-only dạng:

```text
relationChanged
→ direct assignment OperationalContactGuestMemberId
```

### Thay bằng:

```text
relation trong payload phải giữ nguyên intent của persisted relation
```

Nếu client cố đổi relation khi không có content-relink kỹ thuật:

```text
reject
```

Stable code đề xuất:

```text
OPERATIONAL_CONTACT_RELATION_NOT_EDITABLE_IN_PENDING_EDIT
```

Message:

```text
Liên kết đầu mối không được thay đổi trong Sửa đơn.
Hãy sử dụng Sửa nhanh để cập nhật liên kết hoặc Chuyển đầu mối nếu đổi người phụ trách.
```

---

## 6.2.1. Khi member list không đổi

Relation phải giữ nguyên:

```text
old OperationalContactGuestMemberId
==
payload OperationalContactGuestMemberId
```

Không direct assign.

---

## 6.2.2. Khi member list thay đổi — copy-on-write

Hiện flow:

```text
StageReplaceMembers
→ SaveChanges #1
→ LinkMembers
→ SaveChanges #2
```

Sau fix:

```text
StageReplaceMembers
→ SaveChanges #1
→ LinkMembers để re-point SAME logical member
→ resolve linked new row
→ SyncSnapshotFromLinkedMember
→ refresh pending invitation snapshot nếu cần
→ SaveChanges #2
```

Điểm quan trọng:

`guest_member_id` cũ bị thay bởi ID mới do copy-on-write.

Không được dùng ID cũ để suy ra identity sau replace.

Dùng:

```text
operationalContactClientMemberKey
```

chỉ để tìm lại **cùng row logic trong editing session**.

---

## 6.2.3. Nếu contact member bị mất khỏi payload

Backend phải reject:

```text
OPERATIONAL_CONTACT_MEMBER_NOT_FOUND
```

Không:

```text
relation = null
```

Không guess member khác.

---

## 6.2.4. Sync audit

Nếu member edit làm contact snapshot thay đổi:

```text
Kim / Director
→
Kim / Senior Director
```

thêm audit change:

```text
operational_contact_job_title
Director
→ Senior Director
```

Nguồn:

```text
PENDING_EDIT
```

Có thể giữ cùng `correlationId` với edit.

Không tạo IdentityChange.

Không gửi email.

Không đổi:

```text
OperationalContactUserId
ConfirmedAt
ConfirmationSource
status
```

---

# 6.3. Pending invitation snapshot phải được refresh

Nếu campus đang:

```text
WAITING_CONTACT_CONFIRMATION
```

và linked member được chỉnh trước khi người được mời accept:

```text
member updated
→ contact snapshot synced
```

thì:

```text
VisitRequestIdentityChange.PendingSnapshotJson
```

cũng phải cập nhật.

Nếu không:

```text
edit mới
→ invitation vẫn giữ snapshot cũ
→ user accept
→ snapshot cũ ghi đè trở lại
```

Reuse:

```text
OperationalContactProfileMutation
    .RefreshPendingInvitationSnapshotAsync(...)
```

Do `VisitRequestV2EditService` hiện chưa có invitation service, cần inject:

```text
IOperationalContactInvitationService
```

hoặc extract một orchestration helper dùng chung.

Không extend:

```text
expires_at
token_version
resend_count
```

---

# 6.4. `VisitRequestV2EditOps.cs`

File:

```text
backend/PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs
```

Hiện:

```text
StageReplaceMembers(...)
LinkMembers(...)
```

Refactor để tránh nhiều caller tự triển khai sai thứ tự.

Có thể thêm:

```csharp
RelinkExistingOperationalContact(...)
```

hoặc:

```csharp
LinkMembers(
    ...,
    preserveExistingContactIdentity: true)
```

Khuyến nghị **không nhét auto-sync implicit vào `LinkMembers` chung** nếu Create cũng gọi nó.

Tốt hơn:

```text
LinkMembers(...)
SyncLinkedContactSnapshot(...)
```

được gọi explicit tại:

- Pending Edit;
- Amendment Apply.

Nhìn code sẽ biết mutation xảy ra ở đâu.

---

# 6.5. `VisitAmendmentService.cs`

File:

```text
backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs
```

## Submit

Giữ nguyên nguyên tắc:

```text
proposal chỉ lưu proposal
active state chưa thay
```

Nếu member đang là Operational Contact bị remove:

```text
reject proposal
```

Guard FE hiện có, BE vẫn phải giữ.

---

## Approve

Khi amendment sửa member list:

```text
StageReplaceMembers
→ LinkMembers
→ find linked contact member
→ SyncSnapshotFromLinkedMember
→ canonical recompute
→ revision snapshot
→ commit
```

Như vậy:

```text
Proposal:
Kim Director
→ Kim Senior Director

Before approve:
active member = Director
contact       = Director

After approve:
active member = Senior Director
contact       = Senior Director
```

Không tồn tại trạng thái:

```text
member Senior Director
contact Director
```

sau khi amendment đã được duyệt.

---

## Không sync khi contact unlinked

Nếu:

```text
OperationalContactGuestMemberId = null
```

không động vào contact snapshot.

---

# 6.6. `VisitSafeEditService.cs`

File:

```text
backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs
```

## Xóa rule “effective relation luôn phải fingerprint-equal trên mọi contact edit”

Thay bằng validation theo intent.

### Case A — relation không đổi, chỉ Phone đổi

```text
ALLOW
```

Dù legacy snapshot/member đang mismatch.

Không được để một mismatch cũ chặn sửa số điện thoại.

### Case B — relation không đổi, shared profile không đổi

```text
ALLOW
```

### Case C — contact linked và user muốn đổi FullName/Org/JobTitle tùy ý

```text
REJECT
```

Message:

```text
Đầu mối hiện đang liên kết với một thành viên trong đoàn.
Hãy cập nhật thông tin của thành viên qua Sửa đơn/Đề xuất thay đổi,
hoặc bỏ liên kết nếu đầu mối không còn nằm trong đoàn.
```

Stable code đề xuất:

```text
OPERATIONAL_CONTACT_LINKED_PROFILE_REQUIRES_MEMBER_UPDATE
```

### Case D — user bấm “Đồng bộ theo thành viên”

Payload gửi:

```text
FullName = member.FullName
JobTitle = member.JobTitle
Organization = member.Organization
```

relation giữ nguyên.

Backend cho phép.

### Case E — explicit unlink

```text
MemberLink != null
GuestMemberId = null
```

Sau unlink, contact profile được phép độc lập.

Nếu cùng một request vừa:

```text
unlink
+
change contact profile
```

thì validation phải dùng:

```text
effectiveRelationId = null
```

và cho phép atomic save.

### Case F — link từ null → member X

Chỉ cho nếu:

```text
PersonIdentity.Key(contact snapshot)
==
PersonIdentity.Key(member X)
```

Mismatch:

```text
OPERATIONAL_CONTACT_RELATION_PROFILE_MISMATCH
```

### Case G — relation A → B

Không dùng để đổi người.

Nếu B khác identity:

```text
reject mismatch
```

Nếu thật sự muốn B:

```text
Transfer
```

---

# 6.7. `UpdateOperationalContactProfileCommandHandler.cs`

File:

```text
backend/PEMS.Application/Delegations/Commands/OperationalContact/
UpdateOperationalContactProfileCommandHandler.cs
```

Đây là standalone API khác của same-person profile update.

Phải enforce **cùng contract với Safe Edit**.

Nếu không, UI Safe Edit bị chặn nhưng client gọi endpoint trực tiếp vẫn có thể tạo mismatch.

## Required behavior

Nếu `OperationalContactGuestMemberId != null`:

- phone-only change → allow;
- shared profile unchanged → allow;
- shared profile changed và bằng linked member → allow;
- shared profile changed và khác linked member → reject.

Không đổi:

```text
email
OperationalContactUserId
confirmation
status
```

---

# 6.8. `OperationalContactProfileMutation.cs`

File:

```text
backend/PEMS.Application/Delegations/Common/OperationalContactProfileMutation.cs
```

Không cần biến helper này thành class biết mọi workflow.

Nó nên tiếp tục là primitive:

```text
Normalize
AddProfileChanges
Apply
RefreshPendingInvitationSnapshot
```

Thêm helper nhẹ nếu cần:

```csharp
HasSharedIdentityChanges(...)
```

nhưng policy:

```text
linked/unlinked
allowed/refused
```

nên nằm trong `OperationalContactLink` hoặc dedicated policy/helper, không nhét vào mutation primitive.

---

# 6.9. Stable error codes

File:

```text
backend/PEMS.Domain/Constants/VisitFormV2Constants.cs
```

Giữ:

```text
OPERATIONAL_CONTACT_MEMBER_NOT_FOUND
OPERATIONAL_CONTACT_MEMBER_NOT_ELIGIBLE
OPERATIONAL_CONTACT_RELATION_PROFILE_MISMATCH
```

Đề xuất thêm:

```text
OPERATIONAL_CONTACT_RELATION_NOT_EDITABLE_IN_PENDING_EDIT
OPERATIONAL_CONTACT_LINKED_PROFILE_REQUIRES_MEMBER_UPDATE
```

Không parse message phía frontend.

---

# 7. Frontend implementation plan

# 7.1. `VisitRequestV2DetailView.tsx` — fix nút Sửa nhanh

File:

```text
frontend/pems-react/src/features/visit-request/components/v2/
VisitRequestV2DetailView.tsx
```

Hiện cần sửa:

```ts
const editableCampusCount = data.campusVisits.filter(c =>
  hasAction(c.allowedActions, VisitV2Action.SubmitSafeEdit)
).length;
```

thành logic:

```ts
const editableCampusCount = data.campusVisits.filter(c =>
  hasAction(c.allowedActions, VisitV2Action.SubmitSafeEdit) ||
  hasAction(c.allowedActions, VisitV2Action.UpdateContactProfile)
).length;
```

`canOpenSafeEdit`:

```text
request-level safe edit
OR
any campus generic safe edit
OR
any campus contact-profile edit
```

Acceptance:

```text
WAITING_REQUEST_APPROVAL
+ UPDATE_OPERATIONAL_CONTACT_PROFILE
→ thấy nút Sửa nhanh
```

---

# 7.2. `CampusVisitCard.tsx` — existing campus

File:

```text
frontend/pems-react/src/features/visit-request/components/v2/CampusVisitCard.tsx
```

## Khi `contactReadOnly = true`

Không render editable relation picker nữa.

Thay bằng read-only summary:

```text
Đầu mối của đoàn

Kim Min Jae
Director of Global Programs
SeoulTech
Email: ...
Phone: ...

Liên kết với danh sách đoàn:
Kim Min Jae — Khách
```

Nếu relation null:

```text
Không nằm trong danh sách đoàn
```

Helper:

```text
Muốn thay đổi liên kết, sử dụng "Sửa nhanh".
Muốn đổi sang người phụ trách khác, sử dụng "Chuyển đầu mối".
```

---

## Không để user chọn:

```text
Kim contact
↓
Moon member
```

trong Pending Edit.

Xóa UI:

```text
Đầu mối hiện tại có nằm trong danh sách đoàn không?
[dropdown]
```

khỏi pending edit existing campus.

---

# 7.3. Badge member đang là contact

Ở danh sách Guest / Support:

```text
Kim Min Jae
[ĐẦU MỐI]
```

Giữ hoặc làm nổi badge hiện tại.

Mục tiêu:

người dùng biết tại sao nút Delete không được phép.

---

# 7.4. Edit member đang là contact

Khi user bắt đầu sửa row đang có badge `ĐẦU MỐI`:

Hiển thị one-time notice:

```text
Người này hiện là đầu mối của cơ sở.
Khi lưu, Họ tên / Chức vụ / Đơn vị của đầu mối sẽ được cập nhật theo thành viên này.
Email và số điện thoại đầu mối không thay đổi.
```

Không popup mỗi keystroke.

Không tự đổi email.

---

# 7.5. Direct Delete

Giữ guard hiện có:

```text
memberHoldsContactRole(...)
```

Message nên rõ:

```text
Người này đang là đầu mối của đoàn.
Hãy bỏ liên kết trong "Sửa nhanh" hoặc chuyển đầu mối trước khi xóa.
```

---

# 7.6. Excel Replace / Replace Both / Apply To All

Đây là phần phải harden.

## Existing campus

Trước khi apply replacement:

```text
current contact member key
```

phải còn tồn tại trong result set.

Nếu không:

```text
BLOCK WHOLE REPLACE
```

Không:

```text
clear contact key rồi tiếp tục
```

Message:

```text
Danh sách mới không còn thành viên đang là đầu mối.
Vui lòng giữ người này trong danh sách hoặc bỏ liên kết/chuyển đầu mối trước khi thay thế.
```

Zero mutation.

---

## Create draft

Có thể mềm hơn vì chưa có persisted identity:

```text
replace làm mất contact member
→ clear draft selection
→ warning
→ yêu cầu chọn lại trước Submit
```

Không áp dụng behavior mềm này cho existing persisted campus.

---

# 7.7. `VisitSafeEditModal.tsx`

File:

```text
frontend/pems-react/src/features/visit-request/components/VisitSafeEditModal.tsx
```

## Contact unlinked

Render:

```text
Họ tên            editable
Đơn vị            editable
Chức vụ           editable
Số điện thoại     editable
Email             locked
Relation          dropdown
```

---

## Contact linked

Render:

```text
Họ tên            read-only
Đơn vị            read-only
Chức vụ           read-only
Số điện thoại     editable
Email             locked
```

Relation section:

```text
Liên kết hiện tại:
Kim Min Jae — Director — SeoulTech
```

Actions:

```text
[Không còn nằm trong danh sách đoàn]
```

Nếu legacy mismatch:

```text
[Đồng bộ theo thành viên]
```

Không cho nhập tùy ý shared fields khi vẫn linked.

---

# 7.8. Relation candidate filtering

Nếu relation đang null và user muốn link contact hiện tại với member:

Chỉ show member có:

```text
PersonIdentity.Key(member)
==
PersonIdentity.Key(contact)
```

Ví dụ contact Kim thì dropdown không nên liệt kê Moon/Emily như một lựa chọn hợp lệ.

Có thể hiển thị:

```text
— Không nằm trong danh sách đoàn —
Kim Min Jae — Director — SeoulTech
```

Nếu không có exact candidate:

```text
Không tìm thấy thành viên phù hợp trong danh sách đoàn.
```

Backend vẫn validate lại.

Frontend filtering chỉ là UX.

---

# 7.9. Legacy mismatch UX

Nếu persisted data đang:

```text
relation = Kim member
contact snapshot != Kim member
```

Không bắt phone edit thất bại.

Hiển thị warning:

```text
Thông tin đầu mối hiện tại chưa đồng bộ với thành viên đã liên kết.
```

Actions:

```text
[Đồng bộ theo thành viên]
[Bỏ liên kết]
```

Không tự sửa khi mở modal.

---

# 7.10. `VisitAmendmentSubmitModal.tsx`

File:

```text
frontend/pems-react/src/features/visit-request/components/
VisitAmendmentSubmitModal.tsx
```

Giữ nguyên việc không render Operational Contact editor.

Giữ hidden relation tracking để backend relink sau copy-on-write.

Nếu row đang là contact:

```text
badge ĐẦU MỐI
```

Delete:

```text
blocked
```

Có thể thêm helper:

```text
Thay đổi thông tin của thành viên này sẽ cập nhật snapshot đầu mối sau khi đề xuất được duyệt.
```

Không sync active contact trước approve.

---

# 7.11. `ContactIdentityActions.tsx`

File:

```text
frontend/pems-react/src/features/visit-request/components/
ContactIdentityActions.tsx
```

Giữ đúng nhiệm vụ:

```text
DIFFERENT PERSON ONLY
```

Form chuyển đầu mối:

```text
blank
```

Không prefill từ current contact.

Same email:

```text
reject
→ use Safe Edit
```

Sau transfer:

```text
relation = null
```

Nếu người mới cũng nằm trong danh sách đoàn, user có thể link lại sau khi identity change hoàn tất bằng Safe Edit và exact-match validation.

Không auto-guess member.

---

# 8. DTO / API contract

# 8.1. Pending Edit

Có thể giữ các field relation trong DTO để phục vụ **technical preservation / COW relink**, nhưng frontend không cho user chỉnh.

Backend phải phân biệt:

```text
echoed relation used to preserve identity
!=
business request to change relation
```

Nếu payload cố đổi persisted relation ngoài allowed preservation semantics:

```text
reject
```

---

# 8.2. Safe Edit relation tri-state

Giữ mô hình:

```text
MemberLink == null
→ không đụng relation

MemberLink != null && GuestMemberId == null
→ explicit unlink

MemberLink.GuestMemberId = X
→ explicit link to X
```

Không quay lại một `ulong?` đơn làm mất ý nghĩa:

```text
not supplied
vs
set null
```

---

# 9. Transaction rules

# 9.1. Pending Edit linked member update

```text
BEGIN

lock request/instance
authorize
validate lifecycle
validate contact immutable payload
validate member set
validate current linked member survives

capture baseline

replace member rows
SAVE #1   // obtain new guest_member_id

relink same logical contact member
sync 3 shared fields member -> contact
refresh pending invitation snapshot if applicable
audit

revision snapshot
canonical recompute

SAVE
COMMIT
```

Nếu bất kỳ bước nào fail:

```text
ROLLBACK ALL
```

---

# 9.2. Amendment Approve

```text
BEGIN

lock amendment/request/instance
validate base revisions
validate approval window

capture baseline

apply approved member content
relink contact
sync member -> contact shared fields
audit/revision/canonical

COMMIT
```

Không sync trước approval.

---

# 9.3. Safe Edit

```text
BEGIN

lock request
lock instances in deterministic order

validate profile
resolve effective relation

if linked:
    reject arbitrary shared-profile divergence
    allow phone
    allow exact sync
    allow explicit unlink

if relation changes:
    validate exact identity

apply contact patch
audit
refresh pending invitation snapshot when relevant

COMMIT
```

---

# 10. Revision semantics

## Pending Edit

Member content đổi:

```text
FormRevision +1
```

Contact sync là consequence của cùng edit:

```text
không +1 lần nữa
```

Một user action:

```text
exactly one FormRevision bump
```

---

## Amendment Approve

Member proposal approved:

```text
FormRevision +1
ApprovalRevision theo rule hiện tại
```

Contact sync:

```text
không tạo extra revision
```

---

## Safe Edit contact-only

Phone / relation / repair snapshot:

```text
FormRevision không tăng
ApprovalRevision không tăng
detail.RowVersion +1
instance.RowVersion +1
audit có
```

Giữ semantics hiện tại của Contact metadata.

---

# 11. Audit requirements

Mọi derived sync phải truy vết được.

Ví dụ:

```text
Action:
OPERATIONAL_CONTACT_SYNCED_FROM_MEMBER
```

Hoặc ghi vào cùng audit event với source:

```text
PENDING_EDIT
AMENDMENT_APPLIED
```

Field-level:

```text
operational_contact_full_name
operational_contact_job_title
operational_contact_organization
```

Không log email full nếu policy hiện tại mask email.

Relation audit dùng human-readable name nếu có thể.

---

# 12. Không thay đổi các identity/authority field ngoài đúng workflow

Các fix này tuyệt đối không được tự thay:

```text
VisitRequestCampus.OperationalContactUserId
OperationalContactConfirmedAt
OperationalContactConfirmationSource
VisitRequestIdentityChange
EmailActionToken
campus status
request status
```

trừ Replace/Transfer/Confirmation workflow hiện có.

---

# 13. Data repair cho dữ liệu đã tồn tại

Sau deploy có thể đã tồn tại:

```text
OperationalContactGuestMemberId != null
nhưng
PersonIdentity.Key(contact) != PersonIdentity.Key(member)
```

Không auto-update toàn DB một cách mù quáng.

## Bước 1 — audit query/report

Liệt kê:

```text
VisitRequestId
VisitInstanceId
CampusId
OperationalContactGuestMemberId
contact FullName/JobTitle/Organization
member FullName/JobTitle/Organization
status
```

Phân loại:

```text
A. Not started + obvious same-person stale snapshot
B. Started/completed historical data
C. dangling member id
D. wrong-campus / invalid relation
```

## Bước 2 — repair policy

### Not-started

Có thể repair qua domain command:

```text
sync contact from member
```

có AuditLog.

### Started / completed

Không rewrite history tự động.

Chỉ report/manual review.

### Dangling relation

Không guess.

Set null chỉ qua controlled repair script có audit hoặc manual decision.

---

# 14. Frontend test plan

# 14.1. `EditPendingCampusV2Page.test.tsx`

Thêm test:

### PEND-FE-01

Existing campus không render relation picker.

Expected:

```text
query relation combobox = null
```

### PEND-FE-02

Linked member có badge `ĐẦU MỐI`.

### PEND-FE-03

Delete linked member blocked.

### PEND-FE-04

Delete non-contact member allowed.

### PEND-FE-05

Edit linked member shows one-time sync notice.

### PEND-FE-06

Excel Replace removing linked contact is blocked atomically.

### PEND-FE-07

Excel Replace retaining linked contact succeeds.

---

# 14.2. `VisitRequestV2DetailView.test.tsx`

### DETAIL-FE-01

Campus chỉ có:

```text
UPDATE_OPERATIONAL_CONTACT_PROFILE
```

vẫn thấy:

```text
Sửa nhanh
```

### DETAIL-FE-02

Không có cả SafeEdit lẫn UpdateContactProfile:

```text
không render action
```

---

# 14.3. `VisitV2Modals.test.tsx`

## Safe Edit

### SAFE-FE-01

Unlinked contact:

```text
name/org/title/phone editable
```

### SAFE-FE-02

Linked contact:

```text
name/org/title read-only
phone editable
```

### SAFE-FE-03

Legacy mismatch:

```text
show warning
show Sync From Member
```

### SAFE-FE-04

Phone-only save vẫn gửi request dù mismatch legacy.

### SAFE-FE-05

Link dropdown không chứa Moon khi contact là Kim.

### SAFE-FE-06

Explicit unlink:

```text
relation null
```

### SAFE-FE-07

Sau unlink, profile fields có thể editable.

---

## Amendment

Giữ regression:

```text
no contact editor
remove linked contact blocked
relation preserved hidden
```

Thêm badge/helper test nếu UI bổ sung.

---

# 15. Backend integration test plan

# 15.1. Pending Edit

File:

```text
tests/PEMS.IntegrationTests/VisitRequests/
UpdatePendingVisitInstanceV2ServiceTests.cs
```

## PEND-BE-01

Linked member job title changed.

Before:

```text
Member: Kim / Director
Contact: Kim / Director
relation = Kim
```

Edit:

```text
Member: Kim / Senior Director
```

After:

```text
Member: Kim / Senior Director
Contact: Kim / Senior Director
relation points new COW member id
email unchanged
userId unchanged
confirmation unchanged
```

## PEND-BE-02

Linked member full name correction syncs contact.

## PEND-BE-03

Linked member org correction syncs contact.

## PEND-BE-04

Phone contact unchanged.

## PEND-BE-05

Deleting linked member is rejected.

## PEND-BE-06

Handcrafted relation change in Pending Edit is rejected.

## PEND-BE-07

Non-contact member changes do not modify contact.

## PEND-BE-08

Pending invitation snapshot refreshed after contact sync.

## PEND-BE-09

Transfer invitation to different email is not modified by sync.

## PEND-BE-10

One edit increments FormRevision exactly once.

---

# 15.2. Safe Edit

File:

```text
tests/PEMS.IntegrationTests/VisitRequests/VisitSafeEditV2Tests.cs
```

## SAFE-BE-01

Linked + phone-only:

```text
succeeds
```

even when legacy shared fields mismatch.

## SAFE-BE-02

Linked + arbitrary name change:

```text
reject
OPERATIONAL_CONTACT_LINKED_PROFILE_REQUIRES_MEMBER_UPDATE
```

## SAFE-BE-03

Linked + sync exact member profile:

```text
succeeds
```

## SAFE-BE-04

Unlinked + profile edit:

```text
succeeds
```

## SAFE-BE-05

Explicit unlink + profile edit same request:

```text
succeeds atomically
```

## SAFE-BE-06

Null relation → exact matching member:

```text
succeeds
```

## SAFE-BE-07

Kim → Moon relation:

```text
reject RelationProfileMismatch
```

## SAFE-BE-08

Sibling campus member id:

```text
reject MemberNotFound
```

## SAFE-BE-09

Contact-only edit does not bump FormRevision.

---

# 15.3. Operational Contact command

File:

```text
tests/PEMS.IntegrationTests/VisitRequests/
OperationalContactManagementTests.cs
```

## CONTACT-BE-01

Standalone phone update linked contact succeeds.

## CONTACT-BE-02

Standalone arbitrary linked name/title/org change rejected.

## CONTACT-BE-03

Unlinked contact metadata correction succeeds.

## CONTACT-BE-04

Email change still rejected / routed to identity change.

---

# 15.4. Amendment

File:

```text
tests/PEMS.IntegrationTests/VisitRequests/VisitAmendmentV2Tests.cs
```

## AMD-BE-01

Submit member change:

```text
active contact unchanged before approval
```

## AMD-BE-02

Approve linked member change:

```text
member + contact shared fields both updated
```

## AMD-BE-03

Reject amendment:

```text
member + contact unchanged
```

## AMD-BE-04

Remove linked member:

```text
proposal rejected
```

## AMD-BE-05

Non-contact member change does not modify contact.

## AMD-BE-06

Sync does not alter:

```text
email
OperationalContactUserId
confirmation
```

---

# 15.5. Minutes regression

Thêm test nơi tạo biên bản:

Before:

```text
relation points Kim
```

Expected:

```text
Kim appears once
Kim gets "Đầu mối" badge
no duplicate contact row
```

After unlink:

```text
Kim member row remains ordinary member
contact snapshot may be appended separately if applicable
```

Không để wrong member được đánh dấu đầu mối.

---

# 16. Concurrency tests

Phải test:

## CON-01

Safe Edit phone và Pending Edit member cùng instance cùng lúc.

Expected:

```text
one wins
other gets row-version conflict
```

Không last-write-wins.

## CON-02

Pending Edit và Contact Transfer cùng lúc.

Expected:

```text
no mixed identity/profile state
```

## CON-03

Amendment approval và Safe Edit cùng lúc.

Expected:

```text
locking/version check prevents lost update
```

---

# 17. Multi-campus tests

Relation và sync luôn scope theo:

```text
VisitInstanceId
```

Test:

```text
Campus Hà Nội:
Kim contact

Campus HCM:
Kim-like member
```

Sửa Hà Nội:

```text
HCM untouched
```

Không lookup sibling campus.

Không match by name across request.

---

# 18. Security / authorization regression

Không dùng frontend role để quyết định quyền.

UI render dựa trên:

```text
allowedActions / capabilities
```

Backend vẫn authorize từng command.

Test:

```text
Registrant
Current Operational Contact
Staff Leader
Host
HO
unrelated Visitor
```

không được tăng quyền ngoài contract hiện có.

---

# 19. Migration / database

## Schema migration

Dự kiến:

```text
KHÔNG cần schema migration
```

vì đã có:

```text
OperationalContactGuestMemberId
OperationalContact* snapshot
row versions
audit
revision history
```

Chỉ cần data audit/repair nếu DB hiện có mismatch.

Nếu trong quá trình implementation phát hiện FK không enforce đúng relation scope, không tự thêm migration ngay; audit canonical SQL trước.

---

# 20. File impact dự kiến

## Backend

```text
backend/PEMS.Application/Delegations/Common/
  OperationalContactLink.cs
  OperationalContactProfileMutation.cs

backend/PEMS.Application/Delegations/Commands/OperationalContact/
  UpdateOperationalContactProfileCommandHandler.cs

backend/PEMS.Infrastructure/Services/
  VisitRequestV2EditService.cs
  VisitRequestV2EditOps.cs
  VisitSafeEditService.cs
  VisitAmendmentService.cs

backend/PEMS.Domain/Constants/
  VisitFormV2Constants.cs
```

Có thể chạm DTO/validators tùy implementation:

```text
backend/PEMS.Application/Common/DTOs/...
backend/PEMS.Application/Delegations/...
```

---

## Frontend

```text
frontend/pems-react/src/features/visit-request/components/v2/
  CampusVisitCard.tsx
  VisitRequestV2DetailView.tsx

frontend/pems-react/src/features/visit-request/components/
  VisitSafeEditModal.tsx
  VisitAmendmentSubmitModal.tsx
  ContactIdentityActions.tsx

frontend/pems-react/src/features/visit-request/utils/
  visitV2Actions.ts
  personIdentity related helper if needed

frontend/pems-react/src/shared/i18n/locales/vi/
  visitRequestV2.json

frontend/pems-react/src/shared/i18n/locales/en/
  visitRequestV2.json
```

---

## Tests

```text
frontend/pems-react/src/features/visit-request/__tests__/
  EditPendingCampusV2Page.test.tsx
  VisitV2Modals.test.tsx
  VisitRequestV2DetailView.test.tsx
  ContactIdentityActions.test.tsx

tests/PEMS.IntegrationTests/VisitRequests/
  UpdatePendingVisitInstanceV2ServiceTests.cs
  VisitSafeEditV2Tests.cs
  VisitAmendmentV2Tests.cs
  OperationalContactManagementTests.cs
```

---

# 21. Thứ tự triển khai đề xuất

# Commit 1 — Domain invariant

```text
feat(contact): centralize linked member invariants
```

- helper resolve linked member;
- helper exact-match relation;
- helper sync snapshot from member;
- error codes;
- unit/integration foundation tests.

Không đổi UI.

---

# Commit 2 — Pending Edit hardening

```text
fix(visit): preserve contact identity during pending member edits
```

- remove backend relation-only mutation;
- reject client relation changes;
- relink same member after COW;
- sync member → contact;
- refresh pending invitation snapshot;
- direct/bulk removal backend guard;
- tests.

---

# Commit 3 — Amendment apply sync

```text
fix(amendment): sync linked contact after approved member changes
```

- no active sync on submit;
- sync after approve;
- revision/audit tests.

---

# Commit 4 — Safe Edit contract

```text
fix(contact): separate linked profile correction from relation changes
```

- phone-only allowed;
- arbitrary linked shared-profile edit blocked;
- exact sync allowed;
- unlink atomic;
- exact-match link;
- standalone handler aligned;
- backend tests.

---

# Commit 5 — Pending Edit UI cleanup

```text
fix(visit-ui): remove contact relation editing from pending form
```

- remove relation picker from existing campus pending edit;
- read-only relation summary;
- linked member badge;
- improved delete message;
- edit sync notice.

---

# Commit 6 — Bulk import guard

```text
fix(visit-ui): block member replacement that removes linked contact
```

- Excel Replace;
- Replace Both;
- Apply To All;
- tests.

---

# Commit 7 — Safe Edit UI + capability bug

```text
fix(contact-ui): expose contact edit capability and enforce linked profile UX
```

- DetailView `UpdateContactProfile` included in open condition;
- linked profile fields read-only;
- phone editable;
- exact-match candidate filtering;
- Sync From Member;
- explicit unlink;
- i18n;
- tests.

---

# Commit 8 — Data audit + regression closure

```text
test(contact): close operational contact member consistency regressions
```

- data mismatch query/report;
- concurrency tests;
- multi-campus tests;
- minutes regression;
- full suite.

---

# 22. Acceptance criteria cuối cùng

## Create

- [ ] Chọn member → contact 3 shared fields khớp member.
- [ ] Không thể submit key của người A với snapshot người B.
- [ ] Outside delegation → relation null.

## Waiting approval

- [ ] Sửa linked member → contact shared snapshot cập nhật theo khi save.
- [ ] Email/phone không bị member edit ghi đè.
- [ ] Direct delete linked member bị chặn.
- [ ] Excel Replace làm mất linked member bị chặn.
- [ ] Pending Edit không còn relation picker.
- [ ] Sửa relation đi qua Safe Edit.
- [ ] Backend không cho handcrafted relation change từ Pending Edit.

## Safe Edit

- [ ] `WAITING_REQUEST_APPROVAL` có capability profile update thì mở được modal.
- [ ] Linked contact sửa phone được.
- [ ] Linked contact không được nhập shared fields tùy ý.
- [ ] Legacy mismatch không chặn phone.
- [ ] Có Sync From Member.
- [ ] Có explicit unlink.
- [ ] Link mới chỉ được chọn exact identity member.
- [ ] Moon không thể được chọn làm relation cho snapshot Kim.
- [ ] Đổi người phải Transfer.

## Amendment

- [ ] Submit không thay active member/contact.
- [ ] Approve linked-member change sync contact trong cùng transaction.
- [ ] Reject không thay gì.
- [ ] Không xóa linked member.

## Transfer

- [ ] Current holder giữ quyền cho tới khi new holder accept.
- [ ] Transfer không tự guess relation.
- [ ] Relation cũ được clear khi identity đổi.
- [ ] Email identity không đổi bằng Safe/Pending/Amendment.

## Data integrity

- [ ] Không dangling `OperationalContactGuestMemberId`.
- [ ] Không wrong-campus relation.
- [ ] Không auto-repoint theo name.
- [ ] Không bypass Amendment bằng Safe Edit.
- [ ] FormRevision không tăng thừa.
- [ ] Audit đủ old/new.
- [ ] Multi-campus siblings không bị ảnh hưởng.
- [ ] Minutes không gắn badge đầu mối cho sai member.

---

# 23. Những thay đổi KHÔNG nên làm

Không triển khai:

```text
1. Safe Edit contact → tự sửa member approved mà không Amendment.
2. Pending Edit relation dropdown cho chọn bất kỳ member.
3. Xóa linked member → tự relation=null.
4. Xóa linked member → tự chọn người giống tên nhất.
5. Transfer → auto-link member theo fuzzy/name match.
6. Relation validation chỉ ở frontend.
7. Parse backend error message thay vì stable error code.
8. Dùng array index làm member identity.
9. Lookup member từ sibling campus.
10. Auto repair historical completed visit không audit.
```

---

# 24. Root cause cần ghi trong bug report

Root cause không phải chỉ là “dropdown cho chọn sai người”.

Root cause thực sự:

```text
PEMS đang có nhiều mutation path cùng tác động vào:
- delegation member content;
- contact snapshot;
- contact-member relation;

nhưng mỗi path đang dùng invariant khác nhau.
```

Cụ thể:

```text
Create:
member và contact được sync.

Pending Edit:
member có thể đổi nhưng contact snapshot cố định,
relation lại có cửa chỉnh riêng.

Safe Edit:
lại yêu cầu contact/member fingerprint phải luôn bằng nhau.

Amendment:
preserve relation nhưng contact UI bị ẩn.

Detail capability:
backend cho contact profile edit nhưng frontend có thể không mở modal.
```

Fix phải đưa toàn bộ về **một contract duy nhất**, không vá riêng dropdown.

---

# 25. Definition of Done

Chỉ coi fix hoàn tất khi:

```text
dotnet test
```

pass toàn bộ backend tests liên quan;

```text
frontend test suite
```

pass;

và manual regression cover đủ:

```text
Create
Pending Edit
Excel Replace
Approve
Safe Edit
Amendment Submit
Amendment Approve/Reject
Transfer
Minutes
Multi-campus
```

Không chốt chỉ vì happy path “Kim → Kim” chạy được.

Phải test cả:

```text
Kim → Moon
delete Kim
bulk replace loses Kim
legacy mismatch
phone-only edit
concurrent update
sibling campus
pending invitation
transfer pending
```

---

# 26. Kết quả mong đợi sau fix

Người dùng chỉ cần hiểu 3 câu:

> **Sửa người trong đoàn** → dùng Sửa đơn / Đề xuất thay đổi.

> **Sửa thông tin liên hệ hoặc liên kết của cùng đầu mối** → dùng Sửa nhanh.

> **Đổi sang một người khác làm đầu mối** → dùng Chuyển đầu mối.

Hệ thống chịu trách nhiệm giữ relation, snapshot, member, approval và confirmation nhất quán phía sau.

Đó là boundary nghiệp vụ cuối cùng nên dùng cho PEMS.
