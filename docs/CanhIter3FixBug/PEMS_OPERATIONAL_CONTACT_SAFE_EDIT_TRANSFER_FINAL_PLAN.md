# PEMS — Operational Contact Quick Edit / Transfer / Delegation Link Final Implementation Plan

## 1. Mục tiêu cuối cùng

Thiết kế lại toàn bộ phần quản lý **Operational Contact** theo hai nghiệp vụ rõ ràng:

### A. Sửa nhanh
Dùng khi vẫn là **cùng một đầu mối hiện tại**.

Được phép cập nhật:

- Họ và tên.
- Đơn vị công tác.
- Chức vụ.
- Số điện thoại.
- Quan hệ giữa đầu mối hiện tại và một thành viên trong danh sách đoàn.

Không được sửa:

- Email đầu mối.
- `OperationalContactUserId`.
- Người thực sự đang giữ vai trò đầu mối.

### B. Chuyển đầu mối
Dùng khi cần chuyển sang **một người khác**.

- Mở form trắng.
- Nhập lại họ tên, đơn vị, chức vụ, số điện thoại, email.
- Validate tương đương luồng tạo đơn.
- Sau khi gửi, vẫn dùng workflow Replace / Transfer / Invitation / Confirmation hiện có.
- Người hiện tại vẫn giữ quyền cho đến khi người mới xác nhận thành công.

---

# 2. Quy ước UI mới

## 2.1. Không để các đoạn hướng dẫn dài nằm trực tiếp dưới field

Các thông tin mang tính giải thích phải được đặt sau icon `ⓘ` cạnh tiêu đề field/section và chỉ hiển thị khi hover/click.

Ví dụ:

```text
THÔNG TIN ĐẦU MỐI ⓘ
```

hoặc:

```text
Đầu mối hiện tại có nằm trong danh sách đoàn không? ⓘ
```

### Phân biệt bắt buộc

**Tooltip / popover `ⓘ`:**

- giải thích khái niệm;
- giải thích phạm vi chỉnh sửa;
- giải thích lý do email bị khóa;
- giải thích nghiệp vụ dùng chung nhiều cơ sở.

**Validation / warning thực tế:**

- phải hiển thị trực tiếp;
- không được giấu sau `ⓘ`;
- phải nằm gần field gây lỗi;
- phải chặn save nếu lỗi có thể gây bất nhất dữ liệu.

## 2.2. Xóa helper text dài dưới relation picker

Không render paragraph mô tả dài ngay dưới dropdown relation.

Relation picker chỉ nên có:

```text
Đầu mối hiện tại có nằm trong danh sách đoàn không? ⓘ
[ member picker ]
```

Chỉ khi có lỗi dữ liệu thực tế mới render cảnh báo bên dưới.

## 2.3. Xóa thông báo cũ nói rằng đầu mối không sửa được ở Sửa nhanh

Theo thiết kế mới, Operational Contact metadata sẽ được đưa vào Safe Edit.

Vì vậy tất cả text cũ có ý nghĩa:

```text
Operational Contact phải được chỉnh ở một màn khác và không sửa được trong Sửa nhanh
```

phải được xóa khỏi UI và i18n.

---

# 3. UI trang Detail sau khi hoàn thiện

## 3.1. Header

Giữ nút:

```text
[Sửa nhanh]
```

Đây là entry point để chỉnh các field SAFE của request/campus, bao gồm Operational Contact metadata theo thiết kế mới.

## 3.2. Khu vực Operational Contact

Hiển thị thông tin hiện tại dạng read-only:

```text
ĐẦU MỐI ĐOÀN KHÁCH PHỐI HỢP TẠI CƠ SỞ

Họ và tên
Đơn vị công tác
Chức vụ
Số điện thoại
Email
Trạng thái xác nhận
Nguồn xác nhận
Thời điểm xác nhận
```

## 3.3. Quản lý đầu mối

Chỉ còn action identity:

```text
QUẢN LÝ ĐẦU MỐI

[Chuyển đầu mối]
```

Đổi label cũ:

```text
Chỉnh sửa đầu mối
```

thành:

```text
Chuyển đầu mối
```

## 3.4. Xóa section relation đứng riêng trên Detail

Xóa hoàn toàn:

```text
LIÊN KẾT VỚI DANH SÁCH ĐOÀN
[dropdown]
[Lưu liên kết]
```

Relation sẽ được chỉnh bên trong **Sửa nhanh**.

---

# 4. Sửa nhanh — UI cuối cùng

Trong Safe Edit, với mỗi campus có quyền chỉnh sửa, thêm block:

```text
THÔNG TIN ĐẦU MỐI ⓘ

Họ và tên *
[...........................]

Đơn vị công tác *
[...........................]

Chức vụ *
[...........................]

Số điện thoại
[...........................]

Email
[email hiện tại] 🔒

Đầu mối hiện tại có nằm trong danh sách đoàn không? ⓘ
[ member picker ]
```

## 4.1. Email

Email:

- luôn read-only trong Sửa nhanh;
- không render như editable textbox;
- không cho client gửi email mới để cố đổi identity;
- backend vẫn phải kiểm tra email không thay đổi.

Tooltip `ⓘ` của phần Operational Contact có thể giải thích ngắn:

```text
Sửa nhanh chỉ cập nhật thông tin của đầu mối hiện tại.
Nếu cần chuyển sang người khác, sử dụng chức năng "Chuyển đầu mối".
```

Không render đoạn này thường trực dưới form.

## 4.2. Relation picker

Các lựa chọn:

```text
— Không nằm trong danh sách đoàn —
Member A
Member B
Member C
...
```

Chỉ bao gồm member hợp lệ của đúng campus:

- `GUEST`
- `EXTERNAL_SUPPORT`

Không được chứa member của sibling campus.

---

# 5. Invariant quan trọng: relation và snapshot phải nhất quán

Đây là yêu cầu bắt buộc để tránh sai dữ liệu khi tạo biên bản.

Nếu:

```text
OperationalContactGuestMemberId != null
```

thì member được chọn phải nhất quán với Operational Contact snapshot theo các field mà cả hai bên cùng có:

- FullName
- JobTitle
- Organization

Dùng cùng normalization hiện tại của `PersonIdentity.Key()`:

- trim;
- lowercase;
- collapse whitespace;
- không strip accent;
- không dùng name-only.

## 5.1. Trường hợp khớp

Ví dụ:

```text
Operational Contact:
Kim Min Jae
Director of Global Programs
SeoulTech Global Engagement Center

Selected member:
Kim Min Jae
Director of Global Programs
SeoulTech Global Engagement Center
```

→ cho phép Save.

## 5.2. Trường hợp không khớp

Ví dụ:

```text
Operational Contact:
Nguyen Van Thang Canh
International Partnerships Manager
Organization A

Selected member:
Yoon Soo Jin
Programme Coordinator
Organization B
```

→ không được Save.

Hiển thị validation trực tiếp:

```text
Thông tin thành viên được chọn không khớp với đầu mối hiện tại.
Hãy đồng bộ thông tin nếu đây là cùng một người, hoặc chọn lại nếu đây là người khác.
```

Không chỉ warning rồi vẫn cho lưu.

## 5.3. Action đồng bộ

Có thể cung cấp action:

```text
[Đồng bộ theo thành viên đã chọn]
```

Action này chỉ được copy:

- FullName
- JobTitle
- Organization

Không tự đổi:

- Phone
- Email

Sau khi đồng bộ và dữ liệu hợp lệ thì mới cho Save.

---

# 6. Trường hợp "Không nằm trong danh sách đoàn"

Đây là một trạng thái hợp lệ.

Nếu user chọn:

```text
— Không nằm trong danh sách đoàn —
```

thì:

```text
OperationalContactGuestMemberId = null
```

Không yêu cầu Operational Contact phải khớp bất kỳ member nào.

Minute generation phải dùng contact snapshot riêng nếu contact không thuộc delegation roster.

---

# 7. Trường hợp có một member trông giống hệt nhưng user chọn "Không nằm trong đoàn"

Không được tự động link.

Nếu có đúng một candidate có cùng fingerprint:

```text
FullName + JobTitle + Organization
```

thì có thể hỏi xác nhận:

```text
Thông tin đầu mối trùng với một thành viên trong danh sách đoàn.
Đây có phải cùng một người không?
```

- `Cùng một người` → link chính xác member đó.
- `Hai người khác nhau` → giữ relation = null.
- `Xem lại` → quay lại form.

Không tự đoán identity.

---

# 8. Backend Safe Edit

## 8.1. Khôi phục Safe Contact Patch

`SafeContactPatchDto` hiện đang ở trạng thái retired.

Cần phục hồi theo contract mới.

Conceptual:

```csharp
public sealed record SafeContactPatchDto(
    string FullName,
    string? Organization,
    string JobTitle,
    string? Phone,
    string Email,
    SafeContactMemberLinkPatchDto? MemberLink);
```

Relation cần tri-state.

Ví dụ:

```csharp
public sealed record SafeContactMemberLinkPatchDto(
    ulong? GuestMemberId);
```

Ý nghĩa:

```text
MemberLink == null
→ không chỉnh relation.

MemberLink != null && GuestMemberId == null
→ explicit unlink.

MemberLink.GuestMemberId == A
→ explicit link to A.
```

Không dùng một `ulong?` duy nhất nếu làm mất khả năng phân biệt "không gửi" với "set null".

## 8.2. Không duplicate Profile Update logic

Logic đúng đã có trong:

```text
UpdateOperationalContactProfileCommandHandler
```

Cần extract phần mutation chung để reuse giữa:

- `UpdateOperationalContactProfileCommandHandler`;
- `VisitSafeEditService`.

Shared logic phải giữ:

- email normalize + invariant email unchanged;
- FullName / Organization / JobTitle validation;
- phone normalization;
- field-level audit;
- `OperationalContactUserId` unchanged;
- confirmation unchanged;
- request/campus status unchanged;
- no invitation;
- no email;
- no amendment;
- refresh PENDING invitation snapshot nếu invitation đang nói tới đúng email hiện tại.

Không copy/paste hai implementation độc lập.

---

# 9. Relation update phải nằm cùng transaction với Safe Edit

Không làm:

```text
SafeEdit API
→ success

Relation API
→ fail
```

Safe Edit phải atomically xử lý:

```text
other safe fields
+
contact metadata
+
contact/member relation
```

Flow:

```text
BEGIN TRANSACTION

authorize
concurrency
validate lifecycle

validate general safe fields
validate contact metadata
validate email unchanged
validate target relation
validate contact/member consistency

apply all changes

audit
row versions
revision history where applicable

SAVE
COMMIT
```

Nếu bất kỳ validation nào fail:

```text
ROLLBACK ALL
```

---

# 10. Relation là direct metadata update, không phải Amendment

Tuyệt đối không tạo:

- `VisitInstanceAmendment`;
- pending amendment;
- approval notification;
- Staff Leader approval;
- `AmendmentAlreadyPending`.

Relation update không được chiếm "one pending amendment per instance" slot.

Có thể tồn tại đồng thời:

```text
Pending Amendment:
MEETING → WORKSHOP

và

Safe Edit:
Contact relation A → B
```

Hai việc không được chặn nhau.

---

# 11. Validation backend cho relation

Nếu `GuestMemberId != null`:

backend phải prove:

1. member tồn tại;
2. member thuộc đúng VisitRequest;
3. member link vào đúng VisitInstance;
4. member thuộc đúng campus;
5. member type eligible;
6. không deleted/unusable nếu entity có state;
7. snapshot contact và selected member nhất quán theo identity invariant.

Không:

- match name-only;
- fuzzy-match;
- array index;
- sibling-campus lookup;
- silently coerce invalid id to null.

Invalid → stable business error + zero mutation.

---

# 12. Revision / RowVersion

Operational Contact metadata hiện được thiết kế là metadata correction, không phải revision của visit content.

Do đó phải tách:

### CONTACT METADATA ONLY

Ví dụ:

- name;
- organization;
- job title;
- phone;
- member relation.

Expected:

```text
FormRevision        không tăng
ApprovalRevision    không tăng
detail.RowVersion   +1
instance.RowVersion +1
audit               có
```

### FORM SAFE FIELD CHANGED

Ví dụ Notes hoặc TransportationNote:

```text
FormRevision +1
```

Nếu một Safe Edit chứa:

```text
Notes
+
Phone
+
Relation
```

thì:

```text
FormRevision chỉ tăng đúng 1 lần
```

do Notes.

Không tăng thêm vì contact metadata.

---

# 13. Pending Amendment coexistence

Đây là regression bắt buộc.

Scenario:

```text
Amendment #1:
MEETING → WORKSHOP
PENDING_APPROVAL
```

Sau đó user Safe Edit:

```text
Phone A → B
Relation null → Member A
```

Expected:

- Safe Edit thành công.
- Amendment #1 vẫn Pending.
- Sau đó Amendment #1 vẫn approve được.
- Không `AmendmentAlreadyPending`.
- Không false `AmendmentBaseRevisionConflict` do metadata-only update.
- Không data loss.

---

# 14. Concurrency

Safe Edit phải giữ optimistic concurrency.

Scenario:

```text
RowVersion = 10

Writer A:
relation null → A

Writer B:
relation null → B
```

Cả hai mở từ version 10.

Expected:

- một writer success;
- writer còn lại nhận stable conflict;
- không lost update;
- final relation = winner;
- profile/member/partner links không bị ảnh hưởng ngoài ý muốn.

---

# 15. Audit / History

Contact profile / relation update phải audit rõ ràng.

Các field semantic:

```text
operational_contact_full_name
operational_contact_organization
operational_contact_job_title
operational_contact_phone
operational_contact_relation
```

Không audit email ở Safe Edit vì email không được phép thay.

User-facing history:

- không hiện raw GuestMemberId;
- không hiện ClientMemberKey;
- resolve tên khi có bằng chứng chính xác;
- nếu không resolve được lịch sử thì dùng wording trung thực, không đoán.

Legacy snapshot thiếu relation field phải giữ `BeforeUnknown` / compatibility behavior hiện tại.

---

# 16. Minutes / biên bản

Phải kiểm tra trực tiếp:

- `MinuteAutoFill`;
- `MinuteContactBadge`;
- `SaveMinutesCommandHandler`.

Expected:

### Contact không nằm trong đoàn

```text
OperationalContactGuestMemberId = null
```

→ Minute có một contact snapshot riêng khi cần.

### Contact được link Member A

```text
OperationalContactGuestMemberId = A.Id
```

→ Member A nhận badge `Đầu mối`.

### Contact chuyển A → B

→ badge chuyển sang B.

### Không bao giờ cho phép

```text
snapshot mô tả Nguyen
relation trỏ Yoon
```

được commit.

---

# 17. Chuyển đầu mối

## 17.1. UI

Nút:

```text
[Chuyển đầu mối]
```

mở form trắng hoàn toàn:

```text
Họ và tên *
[ ]

Đơn vị công tác *
[ ]

Chức vụ *
[ ]

Số điện thoại
[ ]

Email *
[ ]

Lý do
[ ]
```

Không prefill Operational Contact hiện tại.

## 17.2. Validation

Reuse validation từ Create:

- required fields;
- email format;
- phone normalization;
- organization rules;
- trim;
- max length;
- partner/org selection policy nếu có.

Email mới phải khác email hiện tại.

Nếu trùng:

```text
Email mới trùng với đầu mối hiện tại.
Nếu chỉ cần cập nhật thông tin, hãy sử dụng Sửa nhanh.
```

## 17.3. Backend

Reuse existing:

- `ReplaceOperationalContactCommand`;
- `InitiateOperationalContactTransferCommand`;
- invitation / confirmation workflow.

Không viết workflow identity mới.

Confirmed holder hiện tại vẫn giữ quyền cho tới khi người mới accept.

Khi identity thực sự đổi:

```text
OperationalContactGuestMemberId
```

phải clear vì relation cũ không còn đáng tin.

Không auto-match người mới với delegation.

---

# 18. Permission / multi-campus

Operational Contact là per-campus.

Trong multi-campus request:

- HN contact chỉ sửa HN.
- DN contact chỉ sửa DN.
- relation picker HN chỉ chứa member HN.
- HN mutation không touch DN/HCM.
- registrant có quyền theo policy hiện tại.
- frontend dựa vào backend capability/allowedActions, không hard-code role.

---

# 19. UI cleanup bắt buộc

Sau implementation:

## Detail

Xóa:

- section relation riêng;
- nút `Lưu liên kết`;
- paragraph helper text dài;
- wording cũ nói contact không sửa được ở Safe Edit.

Giữ:

- display current contact;
- `Chuyển đầu mối`.

## Safe Edit

Thêm:

- contact metadata fields;
- email readonly;
- relation picker;
- `ⓘ` cạnh tiêu đề cần giải thích;
- validation trực tiếp khi mismatch.

---

# 20. Test matrix bắt buộc

## Contact metadata

- name only.
- organization only.
- job title only.
- phone only.
- phone null → value.
- phone value → null.
- email changed → reject.
- no-op → reject/no mutation.

## Relation

- null → A.
- A → null.
- A → B.
- A → A no-op.
- invalid member.
- sibling-campus member.
- ineligible member.
- matching profile/member → success.
- mismatched profile/member → blocked.

## Sync action

- sync selected member copies name/title/org.
- does not copy phone.
- does not copy email.

## Atomicity

- name + relation success together.
- valid profile + invalid relation → nothing saved.
- notes + profile + relation → one transaction.

## Revision

- phone-only → no FormRevision bump.
- relation-only → no FormRevision bump.
- notes-only → +1.
- notes + phone + relation → +1 exactly.

## Amendment coexistence

- pending content amendment exists.
- Safe Edit relation/profile succeeds.
- pending amendment remains pending.
- later approval still succeeds if unrelated.

## Concurrency

- same instance RowVersion, two relation writes → exactly one winner.

## Transfer

- form blank.
- required validation.
- invalid email.
- same current email blocked.
- confirmed holder → transfer.
- no holder → replace.
- old holder keeps rights before accept.
- accept changes holder.
- decline leaves holder unchanged.
- cancel leaves holder unchanged.
- relation cleared only on real identity change.

## Minutes

- unlinked contact appears correctly.
- linked A receives contact badge.
- A → B moves badge.
- mismatch cannot be persisted.

## Multi-campus

- HN edit leaves siblings byte-for-byte unaffected in relevant state.
- sibling member cannot be selected.
- sibling contact cannot mutate another campus.

---

# 21. Triển khai theo phase

## Phase 0 — Working tree audit

```text
git status
git diff --stat
git diff --name-only
git diff
```

Không reset, restore, clean, rebase làm mất các fix trước.

## Phase 1 — UI text cleanup

- xóa helper paragraphs;
- bổ sung tooltip/popover `ⓘ`;
- xóa wording cũ trái với Safe Edit mới.

## Phase 2 — DTO / contract

- revive `SafeContactPatchDto`;
- add tri-state relation patch;
- update API/frontend types.

## Phase 3 — Shared metadata mutation

Extract logic reusable từ `UpdateOperationalContactProfileCommandHandler`.

## Phase 4 — Safe Edit backend

Integrate:

- profile;
- relation;
- consistency invariant;
- atomic transaction.

## Phase 5 — Revision / concurrency / audit

Xác minh:

- no false revision bumps;
- pending amendment coexistence;
- history compatibility;
- no raw id leak.

## Phase 6 — Safe Edit frontend

- add contact block;
- email readonly;
- relation picker;
- mismatch validation;
- optional sync action.

## Phase 7 — Detail cleanup

- remove standalone relation section;
- rename `Chỉnh sửa đầu mối` → `Chuyển đầu mối`.

## Phase 8 — Transfer modal

- blank form;
- Create-equivalent validation;
- existing Replace/Transfer workflow.

## Phase 9 — Minutes / multi-campus regression

Audit all relation consumers.

## Phase 10 — Full regression

Backend:

```text
dotnet build PEMS.slnx
dotnet test PEMS.IntegrationTests
dotnet test PEMS.UnitTests
dotnet test PEMS.ArchitectureTests
```

Frontend:

```text
npm run build
npm run lint
npm run test:unit -- --run
```

Run sequentially if the machine has known concurrent I/O flakiness.

---

# 22. Acceptance criteria cuối cùng

Implementation chỉ hoàn tất khi đồng thời đạt:

1. `Sửa nhanh` chỉnh được name/org/jobTitle/phone của Operational Contact.
2. Email read-only và backend fail-closed nếu client cố đổi.
3. Relation nằm trong Safe Edit, không còn section riêng ở Detail.
4. Không còn helper paragraph dài dưới relation picker.
5. Hướng dẫn nằm trong `ⓘ`.
6. Mismatch giữa contact snapshot và selected member bị block.
7. Không thể tạo biên bản với relation trỏ một người nhưng snapshot mô tả người khác.
8. Relation không tạo Amendment.
9. Existing pending Amendment không chặn relation/profile Safe Edit.
10. Metadata-only edit không làm unrelated Amendment stale.
11. `Chuyển đầu mối` mở form trắng.
12. Chuyển đầu mối vẫn dùng confirmation workflow hiện có.
13. Current holder không mất quyền trước khi người mới accept.
14. Identity change thực sự clear relation cũ.
15. Multi-campus isolation được chứng minh bằng test.
16. Concurrency được chứng minh bằng test.
17. Audit/history không leak technical ids.
18. Full backend/frontend regression xanh.
19. Không commit/push trước khi review final diff.

---

# 23. Nguyên tắc cuối cùng

```text
SỬA NHANH
= cùng một đầu mối
= sửa metadata + relation
= email khóa

CHUYỂN ĐẦU MỐI
= người mới
= form trắng
= email mới
= confirmation workflow

RELATION
= identity assertion giữa contact snapshot và delegation member
= nếu đã link thì dữ liệu phải nhất quán
= không được lưu trạng thái mâu thuẫn
```
