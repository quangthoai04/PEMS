# PEMS — Kế hoạch sửa Import Excel, đầu mối đoàn, chống trùng và biên bản

**Ngày lập:** 15/08/2026  
**Phạm vi code:** nhánh `Dev` mới nhất tại thời điểm triển khai  
**Mục tiêu:** sửa toàn bộ các lỗi còn lại quanh danh sách khách, nhân sự hỗ trợ, đầu mối, đồng bộ biên bản và Partner picker mà không làm lùi stable identity đã triển khai.

---

## 1. Phạm vi và nguyên tắc

Kế hoạch này bao phủ năm khu vực:

1. Import Excel cho danh sách khách và nhân sự hỗ trợ.
2. Nhận diện một người xuyên suốt form tạo đơn và database.
3. Ngăn dữ liệu trùng giữa `GUEST` và `EXTERNAL_SUPPORT`.
4. Minute autofill, đồng bộ người tham gia và chỉnh sửa biên bản.
5. Lọc Partner được phép chọn trong trường “Đơn vị công tác”.

Nguyên tắc bắt buộc:

- Code đang chạy là nguồn sự thật; phải đọc đủ FE → API → handler/service → entity/DB → Minute autofill trước khi sửa.
- Không xác định hai người là một chỉ bằng họ tên.
- Cùng stable ID thì chắc chắn là cùng người.
- Khác stable ID nhưng chuỗi giống nhau chỉ là candidate trùng, phải cảnh báo hoặc yêu cầu xác nhận.
- Ngăn lỗi ở dữ liệu nguồn; Minute autofill chỉ là lớp phòng vệ thứ hai.
- Frontend không được hiển thị capability mà backend không hỗ trợ.
- Không tự động xóa hoặc gộp dữ liệu legacy khi chưa có quyết định của người dùng.
- Không làm thay đổi quyền xem Partner trong module Quản lý đối tác; chỉ siết Partner picker của form đăng ký.

---

## 2. Những phần đã đúng và phải giữ nguyên

Theo bản triển khai stable identity hiện tại, các phần sau đã có và không được rollback:

- Member mới trong form có `clientMemberKey` ổn định.
- Không còn dùng array index làm identity chính của đầu mối.
- Backend ánh xạ `clientMemberKey → GuestMemberId` trong transaction.
- `operational_contact_guest_member_id` là nullable FK, `ON DELETE SET NULL`.
- Dropdown đầu mối có thể lấy cả `GUEST` và `EXTERNAL_SUPPORT` hợp lệ.
- Trước submit, đầu mối được đồng bộ reactive theo member đã chọn.
- Họ tên, chức vụ và đơn vị của đầu mối liên kết được lấy từ member, không cho nhập thành một người khác.
- Điện thoại và email đầu mối vẫn là dữ liệu nhập riêng.
- Backend dựng snapshot từ member trước khi lưu.
- Đổi Partner của member không làm mất stable person identity.
- Snapshot đầu mối sau khi đơn đã được tạo vẫn tuân theo lifecycle lock hiện tại.

Kế hoạch này bổ sung các lớp còn thiếu, không thay thế những cơ chế trên.

---

## 3. Policy nghiệp vụ chốt

### 3.1. Phân loại thành viên

```text
GUEST             = khách chính thức trong đoàn
EXTERNAL_SUPPORT  = trợ lý/phiên dịch/điều phối viên bên ngoài đi cùng đoàn
INTERNAL          = host hoặc người tham gia nội bộ FPTU
MANUAL            = người được thêm trực tiếp vào biên bản
```

Một member nguồn chỉ có một `member_type` chính tại một thời điểm. Nếu một người bị phân loại nhầm thì phải chuyển loại trên cùng stable ID, không tạo member thứ hai.

### 3.2. Ai được làm đầu mối

Được phép:

```text
GUEST
EXTERNAL_SUPPORT
```

Không được phép:

```text
Host
Internal participant
Member thuộc request/instance/cơ sở khác
Member đã bị xóa
```

“Đầu mối” là vai trò bổ sung, không thay thế loại thành viên.

Ví dụ hiển thị:

```text
Khách · Đầu mối
Nhân sự hỗ trợ đoàn · Đầu mối
```

### 3.3. Partner được chọn trong form đăng ký

Mọi người tạo đơn, kể cả Staff và Staff Leader, chỉ được chọn Partner thỏa đồng thời:

```text
ProfileStatus     = APPROVED
Visibility        = PUBLIC
CooperationStatus = ACTIVE
```

Không hiển thị hoặc cho chọn:

```text
DRAFT
PENDING_APPROVAL
REJECTED
INTERNAL
INACTIVE
```

Hồ sơ pending/rejected vẫn có thể xuất hiện trong matcher chống tạo trùng của module Partner, nhưng không được xuất hiện như option chọn “Đơn vị công tác” trong form đăng ký.

---

## 4. Danh sách lỗi hiện tại

| Mã | Lỗi | Mức độ |
|---|---|---|
| `IMP-01` | Cảnh báo thay thế Excel xuất hiện sai vị trí | P1 |
| `IMP-02` | Import khách và hỗ trợ dùng chung pending state, không rõ đang thay danh sách nào | P1 |
| `IMP-03` | Không có thao tác thay thế đồng thời hai danh sách | P1 |
| `IMP-04` | Thông báo “Nhập thành công” xuất hiện trước khi dữ liệu thực sự được áp dụng | P2 |
| `ID-01` | Đầu mối không chọn member nhưng nhập giống member phía trên vẫn là identity riêng | P0 |
| `ID-02` | Không kiểm tra trùng chéo giữa khách và nhân sự hỗ trợ | P0 |
| `MIN-01` | Minute autofill tạo hai dòng khi cùng người đã có hai `GuestMemberId` | P0 |
| `MIN-02` | `EXTERNAL_SUPPORT` bị hiển thị thành “Khách” trong biên bản | P1 |
| `MIN-03` | Xóa người nguồn khỏi biên bản rồi đồng bộ lại thì người đó quay lại | P1 |
| `MIN-04` | UI cho sửa tên/chức vụ/đơn vị người nguồn nhưng backend không lưu | P0 |
| `UX-01` | Hướng dẫn đầu mối quá dài, đặt trực tiếp dưới dropdown | P2 |
| `PART-09` | Form Staff Leader đề xuất cả Partner đang chờ duyệt/internal | P0 |

---

## 5. IMP-01, IMP-02 — Confirmation import sai vị trí và mất ngữ cảnh

### Hiện tượng

Import file cho danh sách khách nhưng khối:

```text
Thay thế toàn bộ danh sách?
```

lại được render sau phần Nhân sự hỗ trợ. Nếu cả hai section đều có file chờ, state cuối có thể ghi đè state trước và người dùng không biết confirmation thuộc danh sách nào.

### Nguyên nhân cần xác minh trong code

- Một state `pendingReplacement` dùng chung cho hai section; hoặc
- Confirmation được render một lần sau cả hai bảng; hoặc
- State không mang discriminator `target: GUEST | EXTERNAL_SUPPORT`.

### Cách sửa frontend

Tách state:

```ts
type ImportTarget = 'GUEST' | 'EXTERNAL_SUPPORT';

type ImportPreviewState = {
  target: ImportTarget;
  fileName: string;
  parsedRows: MemberDraft[];
  importedCount: number;
  duplicateCount: number;
  errors: ImportRowError[];
};

const [guestImportState, setGuestImportState] = useState<ImportPreviewState | null>(null);
const [supportImportState, setSupportImportState] = useState<ImportPreviewState | null>(null);
```

Render ngay trong section tương ứng:

```text
Danh sách khách
  Kết quả đọc file khách
  Xác nhận thay thế danh sách khách
  Bảng khách

Nhân sự hỗ trợ
  Kết quả đọc file hỗ trợ
  Xác nhận thay thế danh sách hỗ trợ
  Bảng hỗ trợ
```

Nhãn action phải cụ thể:

```text
Thay thế danh sách khách bằng file này
Thay thế danh sách nhân sự hỗ trợ bằng file này
```

Không được dùng một action chung nhưng không ghi rõ target.

### Acceptance criteria

- Import khách chỉ tạo preview/confirmation trong section khách.
- Import hỗ trợ chỉ tạo preview/confirmation trong section hỗ trợ.
- Hai preview có thể tồn tại đồng thời, không ghi đè nhau.
- Đóng preview của section này không làm mất preview section kia.
- Scroll/focus tự đưa người dùng tới đúng confirmation vừa tạo.

---

## 6. IMP-03 — Thay thế đồng thời hai danh sách

Khi cả `guestImportState` và `supportImportState` đều có pending rows, hiển thị thêm toolbar tổng hợp:

```text
Có 2 danh sách đang chờ thay thế
[Thay thế cả hai] [Xử lý từng danh sách]
```

### Quy tắc “Thay thế cả hai”

Thao tác phải được tính như một mutation form nguyên tử:

1. Tạo `nextVisitors` từ preview khách.
2. Tạo `nextSupportMembers` từ preview hỗ trợ.
3. Mint/remap `clientMemberKey` đúng quy tắc.
4. Kiểm tra trùng trên hai danh sách hợp nhất.
5. Kiểm tra đầu mối hiện tại còn tồn tại hay không.
6. Chỉ apply hai mảng nếu không có conflict chặn.
7. Nếu apply thành công, clear cả hai pending state.
8. Nếu một bước thất bại, giữ nguyên cả hai danh sách cũ.

Nếu đầu mối bị loại khỏi dữ liệu mới:

```text
Đầu mối hiện tại không còn trong danh sách sau khi thay thế.
Vui lòng chọn lại đầu mối trước khi tiếp tục.
```

Không tự chọn một member mới chỉ vì tên giống nhau.

### Acceptance criteria

- Không có trạng thái “khách đã thay nhưng hỗ trợ chưa thay” do lỗi giữa thao tác.
- Conflict được hiển thị trước khi mất dữ liệu nhập tay.
- Không làm đầu mối trỏ nhầm sau remap key.

---

## 7. IMP-04 — Wording import gây hiểu nhầm

Trước khi người dùng chọn append/replace/keep, không hiển thị:

```text
Nhập Excel thành công
```

Thay bằng:

```text
Đã đọc file Excel cho danh sách khách
Đã đọc file Excel cho danh sách nhân sự hỗ trợ
```

Sau khi thực sự apply mới dùng:

```text
Đã cập nhật danh sách khách từ file Excel
Đã thay thế danh sách nhân sự hỗ trợ từ file Excel
```

Thông báo phải luôn có:

- Target.
- Tên file.
- Tổng số dòng.
- Số dòng hợp lệ.
- Số dòng trùng/cảnh báo.
- Kích thước danh sách sau thao tác.

---

## 8. ID-01 — Đầu mối nhập riêng trùng member nhưng không có stable link

### Hiện tượng

Người dùng chọn:

```text
— Không nằm trong danh sách đoàn —
```

sau đó nhập snapshot giống một member phía trên. Vì contact không có `clientMemberKey/GuestMemberId`, hệ thống vẫn coi đây là identity riêng.

### Cách sửa

Trước submit và khi snapshot identity thay đổi, chạy duplicate candidate detection trên danh sách hợp nhất.

Nếu khớp duy nhất, hiển thị:

```text
Thông tin đầu mối có thể trùng với Nguyễn Văn A trong danh sách đoàn.
Đây có phải cùng một người không?

[Cùng một người — liên kết]
[Là người khác]
[Xem lại]
```

Nếu chọn liên kết:

```text
operationalContactClientMemberKey = matchedMember.clientMemberKey
```

Backend ánh xạ key thành `operational_contact_guest_member_id`.

Nếu chọn “Là người khác”:

- Giữ contact key là `NULL`.
- Lưu decision trong state/payload nếu cần để không cảnh báo lặp trong cùng phiên.
- Yêu cầu có ít nhất một thuộc tính phân biệt mạnh khi mọi fingerprint hiện tại giống hoàn toàn.

### Không được tự động làm

- Không tự liên kết chỉ bằng họ tên.
- Không tự liên kết chỉ bằng tên + tổ chức nếu có nhiều candidate.
- Không để backend âm thầm chọn candidate đầu tiên.

---

## 9. ID-02 — Không kiểm tra trùng chéo giữa khách và hỗ trợ

### Hiện tượng

Cùng một người có thể nằm đồng thời trong:

```text
visitors[]
supportMembers[]
```

Import hiện chỉ bỏ trùng trong từng mảng. Kết quả là database tạo hai `GuestMemberId` khác nhau.

### Dịch vụ nhận diện dùng chung

Không viết nhiều thuật toán rời rạc trong import, form và Minute. Tạo một policy/service dùng chung ở mức phù hợp:

```text
PersonIdentityPolicy
MemberDuplicateDetector
```

Frontend dùng cùng rule để phản hồi sớm; backend là nơi quyết định cuối cùng.

### Thứ tự đối chiếu

1. Cùng `GuestMemberId` hoặc `clientMemberKey` → chắc chắn cùng người.
2. Cùng account/person ID nếu có → chắc chắn cùng người.
3. Cùng email chuẩn hóa hoặc số điện thoại chuẩn hóa duy nhất → match mạnh, cần confirmation theo policy.
4. Cùng `organizationPartnerId` + họ tên + chức vụ + quốc tịch chuẩn hóa → candidate trùng.
5. Không có Partner ID: tổ chức snapshot + họ tên + chức vụ + quốc tịch chuẩn hóa → candidate trùng.
6. Chỉ cùng họ tên → không kết luận.

Chuẩn hóa:

- Trim.
- Lowercase theo invariant/collation phù hợp.
- Gộp whitespace.
- Chuẩn hóa email và điện thoại.
- Không tự ý bỏ dấu tiếng Việt cho person identity.

### UI xử lý conflict chéo

```text
Prof. Liam O'Connor có thể đã tồn tại trong Danh sách khách.

[Không thêm — giữ ở Khách]
[Chuyển người này sang Hỗ trợ]
[Đây là hai người khác nhau]
```

Ý nghĩa:

- **Không thêm — giữ ở Khách:** bỏ incoming support row, giữ member hiện tại.
- **Chuyển sang Hỗ trợ:** đổi `member_type` trên cùng stable member; không tạo ID mới.
- **Hai người khác nhau:** giữ hai member và yêu cầu thông tin phân biệt.

Áp dụng khi:

- Thêm thủ công.
- Import append.
- Import replace.
- Replace both.
- Clone sang cơ sở khác.
- Chuyển loại member.
- Submit create/edit/resubmit.

### Backend bắt buộc

- Validate trên danh sách hợp nhất của đúng campus form.
- Không tin kết quả dedupe từ frontend.
- Conflict phải trả mã lỗi có cấu trúc và candidate liên quan.
- Không tự xóa hoặc gộp hai member đã lưu nếu chưa có explicit command.

---

## 10. MIN-01 — Minute autofill vẫn tạo người trùng

### Nguyên nhân

Nếu dữ liệu nguồn đã có:

```text
GuestMemberId = 101, memberType = GUEST
GuestMemberId = 205, memberType = EXTERNAL_SUPPORT
```

thì ID-first dedupe coi đây là hai người. Stable identity đang hoạt động đúng nhưng nguồn đã tạo hai identity khác nhau.

### Sửa tại nguồn

ID-02 là fix chính, ngăn tạo duplicate mới.

### Lớp phòng vệ Minute

Minute sync/autofill phải:

1. Upsert source-linked participant theo stable source key.
2. Cùng `GuestMemberId` chỉ xuất hiện một lần trong một Minute.
3. Operational contact có cùng `GuestMemberId` với guest/support không được thêm lần hai.
4. Host/internal participant dedupe theo internal person/user ID.
5. Manual participant phải được kiểm tra với source participants trước khi thêm.
6. Dữ liệu legacy có khác ID nhưng fingerprint giống mạnh phải tạo conflict, không âm thầm thêm hai dòng.

### Database protection

Nếu schema cho phép, bổ sung unique index có điều kiện/ngữ nghĩa tương đương:

```text
(minute_id, source_guest_member_id)
```

Do MySQL cho phép nhiều `NULL` trong unique index, manual participant vẫn có thể tồn tại khi `source_guest_member_id` là `NULL`.

Tên cột/index phải theo convention hiện tại; không tạo mô hình song song nếu entity đã có source reference tương đương.

### Legacy conflict

Không tự gộp hai ID cũ chỉ bằng text. Sync nên trả conflict:

```text
Phát hiện hai thành viên có thể là cùng người:
- Liam O'Connor — Khách
- Liam O'Connor — Nhân sự hỗ trợ
```

Người dùng chọn giữ/chuyển/xác nhận hai người. Quyết định phải sửa/lưu ở dữ liệu nguồn hoặc canonical relation, không chỉ ẩn một dòng trên UI Minute.

---

## 11. MIN-02 — Mất loại nguồn trong biên bản

### Hiện tượng

Member `EXTERNAL_SUPPORT` được hiển thị bằng badge `Khách`.

### Nguyên nhân cần xác minh

- Minute projection đang chuyển mọi external member thành `GUEST`; hoặc
- DTO chỉ có `INTERNAL/GUEST`; hoặc
- Frontend map `EXTERNAL_SUPPORT` vào label `Khách`; hoặc
- Minute participant không lưu/không trả source member type.

### Contract cần có

Minute participant response tối thiểu cần phân biệt:

```text
sourceGuestMemberId
sourceMemberType
isOperationalContact
isManual
```

Mapping UI:

```text
GUEST             → Khách
EXTERNAL_SUPPORT  → Nhân sự hỗ trợ đoàn
INTERNAL          → Nội bộ
MANUAL            → Thêm thủ công
```

Operational contact là badge bổ sung:

```text
GUEST + isOperationalContact
→ Khách · Đầu mối

EXTERNAL_SUPPORT + isOperationalContact
→ Nhân sự hỗ trợ đoàn · Đầu mối
```

### Snapshot lịch sử

Nếu Minute đã khóa cần giữ lịch sử, lưu hoặc bảo đảm có:

```text
source_member_type_snapshot
```

Trước khi Minute khóa, sync có thể cập nhật snapshot loại nguồn theo member. Sau khi khóa, không để việc chuyển loại member làm thay đổi biên bản lịch sử.

---

## 12. MIN-03 — Xóa rồi đồng bộ lại bị thêm trở lại

### Nguyên nhân

Sync hiện tìm source member chưa có trong Minute và thêm. Sau hard delete, member đó lại trở thành “chưa có”.

### Policy khuyến nghị

Không hard delete source-linked participant trong workflow thường.

Thay action:

```text
Xóa
```

bằng:

```text
Loại khỏi biên bản
```

Lưu tombstone/trạng thái:

```text
is_excluded_from_sync = true
```

hoặc enum tương đương:

```text
sync_state = ACTIVE | EXCLUDED
```

Sync lần sau phải bỏ qua source key đã bị exclude.

UI có action:

```text
Khôi phục vào biên bản
```

Quy tắc:

- Source-linked participant: exclude/restore, không hard delete.
- Manual participant: được hard delete nếu Minute chưa khóa.
- Nếu nghiệp vụ không cần loại khỏi biên bản, phương án đơn giản hơn là giữ source participant và đánh dấu `Không tham dự`.

### Migration/backward compatibility

- Cột mới nullable/default false hoặc enum default ACTIVE.
- Dữ liệu cũ mặc định ACTIVE.
- Sync phải xử lý idempotent.
- Minute đã khóa không cho thay đổi exclude state.

---

## 13. MIN-04 — UI cho sửa nhưng Save không persist

### Hiện tượng

Sau sync, frontend render input cho tên, chức vụ và đơn vị của guest/support. Người dùng sửa, bấm lưu nhưng backend bỏ qua hoặc dựng lại từ nguồn.

### Nguyên tắc capability

| Loại participant | Tên/chức vụ/đơn vị | Điểm danh/ghi chú |
|---|---|---|
| Host/internal source | Readonly | Được sửa theo permission |
| Guest source | Readonly | Được sửa theo permission |
| External support source | Readonly | Được sửa theo permission |
| Operational contact liên kết | Readonly | Được sửa theo permission |
| Manual participant | Được sửa | Được sửa |

### Frontend

- Không render identity input cho source-linked participant.
- Hiển thị text + icon/badge nguồn.
- Thêm action `Sửa tại thông tin đoàn` nếu lifecycle/permission cho phép.
- Nếu source đã readonly do stage, giải thích rõ thay vì mở input giả.
- Sau save, hydrate lại bằng response canonical từ backend.

### Backend

- DTO/handler phải từ chối rõ nếu client cố sửa identity của source-linked row.
- Không silently ignore field. Trả validation code, ví dụ:

```text
SOURCE_PARTICIPANT_IDENTITY_READONLY
```

- Manual participant mới được update identity qua Minute save command.

Nếu business muốn snapshot Minute độc lập, phải thiết kế explicit command/API và audit riêng. Không chỉ mở input frontend.

---

## 14. UX-01 — Chuyển hướng dẫn đầu mối vào tooltip

Đặt icon trợ giúp ngay sau label:

```text
Đầu mối là ai trong đoàn? (?)
```

Tooltip/popover:

> Chọn một người trong danh sách khách hoặc nhân sự hỗ trợ để liên kết đầu mối với đúng thành viên. Khi tạo biên bản, người này chỉ xuất hiện một lần. Họ tên, chức vụ và đơn vị lấy từ thành viên; điện thoại và email nhập riêng.

Yêu cầu accessibility:

- Hover bằng chuột.
- Focus bằng bàn phím.
- Click/tap trên mobile.
- Có `aria-describedby` hoặc cơ chế tương đương.
- Không chỉ dùng thuộc tính HTML `title`.

Bên dưới dropdown chỉ giữ microcopy:

```text
Họ tên, chức vụ và đơn vị lấy theo thành viên được chọn.
```

---

## 15. PART-09 — Partner picker làm lộ hồ sơ chờ duyệt/internal

### Hiện tượng

Staff Leader tạo đơn và nhập đơn vị công tác cho guest/support vẫn thấy:

```text
Hồ sơ chờ duyệt · FPT University Hà Nội
```

### Root cause dự kiến cần xác minh

Combobox đang chọn endpoint/search policy theo trạng thái đăng nhập:

```text
Authenticated Staff/Leader
→ internal scoped options
→ gồm PENDING_APPROVAL cùng campus
```

Trong khi policy phải chọn theo use case, không theo việc người dùng đã đăng nhập.

### Tách search context

```ts
type OrganizationSearchMode =
  | 'REQUEST_FORM'
  | 'PARTNER_MANAGEMENT'
  | 'PARTNER_MATCHING';
```

| Mode | Dữ liệu |
|---|---|
| `REQUEST_FORM` | Chỉ ACTIVE + APPROVED + PUBLIC |
| `PARTNER_MANAGEMENT` | Theo quyền/campus, có thể xem pending/internal |
| `PARTNER_MATCHING` | Có thể hiển thị candidate pending/rejected để chống tạo trùng, nhưng action theo status |

Cả combobox của guest và support trong form tạo/edit/resubmit phải dùng `REQUEST_FORM`.

### Backend query

Endpoint selectable option phải filter:

```text
ProfileStatus == APPROVED
Visibility == PUBLIC
CooperationStatus == ACTIVE
```

Không chỉ lọc frontend.

### Backend command validation

Khi payload gửi `organizationPartnerId`, create/edit service phải query Partner và kiểm tra lại policy. Nếu không hợp lệ:

```text
PARTNER_NOT_SELECTABLE
Đối tác này chưa được duyệt và công khai nên không thể sử dụng trong đơn đăng ký.
```

Không tin ID do client gửi.

### Cache

Query key phải bao gồm mode:

```ts
['partner-options', mode, normalizedKeyword]
```

Không để cache internal options được dùng lại cho request form.

### Draft cũ

Nếu draft đã chứa Partner hiện không còn selectable:

- Giữ snapshot tên tổ chức để không mất dữ liệu.
- Không đưa Partner đó vào danh sách option mới.
- Hiển thị cảnh báo không còn khả dụng.
- Yêu cầu chọn Partner hợp lệ hoặc chuyển sang organization text tự do theo policy.
- Không âm thầm thay/xóa Partner ID.

---

## 16. API và DTO cần rà soát

Tên thực tế phải theo code hiện tại; không tạo DTO song song nếu đã có trường tương đương.

### Visit form

```text
Member DTO:
  clientMemberKey
  guestMemberId (edit/hydrate nếu có)
  memberType
  organization
  organizationPartnerId

Campus form DTO:
  operationalContactClientMemberKey (create/draft)
  operationalContactGuestMemberId (read/edit persisted)
```

### Minute participant

```text
minuteParticipantId
sourceGuestMemberId
sourceInternalParticipantId/userId
sourceMemberType
isOperationalContact
isManual
isExcludedFromSync hoặc syncState
```

### Sync response

Nên trả:

```text
addedCount
restoredCount
skippedCount
conflicts[]
participants[]  // canonical state sau sync
```

Conflict cần có stable references, loại nguồn và reason code; không chỉ chuỗi message.

---

## 17. Database/migration

### Giữ nguyên

```text
visit_instance_form_details.operational_contact_guest_member_id
```

nullable FK, `ON DELETE SET NULL`.

Backfill phải xét:

```sql
member_type IN ('GUEST', 'EXTERNAL_SUPPORT')
```

### Đề xuất bổ sung sau khi xác minh schema

1. Unique source member trong một Minute:

```text
UNIQUE (minute_id, source_guest_member_id)
```

2. Trạng thái loại khỏi sync:

```text
is_excluded_from_sync BOOLEAN NOT NULL DEFAULT FALSE
```

hoặc enum hiện có tương đương.

3. Snapshot loại nguồn nếu Minute cần audit lịch sử:

```text
source_member_type_snapshot
```

### Migration safety

- Additive, không xóa cột cũ.
- Idempotent theo convention migration/patch hiện tại.
- Không tự gộp/xóa legacy duplicates.
- Có verify query cho cross-instance link, loại member và duplicate Minute source key.
- Có rollback hoặc hướng phục hồi rõ ràng.

---

## 18. Thứ tự triển khai

### Phase 0 — Audit code path

Đọc và lập mapping chính xác:

```text
CampusVisitCard/import handlers
→ form utilities/schema/hydration
→ request API DTO
→ validator
→ create/edit service
→ visit_guest_members
→ operational contact link
→ MinuteAutoFill/sync/save
→ Minute DTO/UI
→ Partner option endpoints
```

Ghi lại root cause thực tế trước khi sửa.

### Phase 1 — P0 source integrity

1. Tạo/shared duplicate detection policy.
2. Validate hợp nhất guest + support ở FE và backend.
3. Chặn/cảnh báo contact snapshot trùng member nhưng chưa link.
4. Siết Partner picker `REQUEST_FORM` và backend validation.
5. Thêm test create/edit/import/clone/direct API.

### Phase 2 — P0 Minute consistency

1. Upsert/dedupe theo stable source ID.
2. Operational contact cùng ID không thêm lại.
3. Thêm DB unique protection nếu phù hợp.
4. Khóa identity fields của source participants.
5. Backend trả lỗi rõ thay vì silently ignore.
6. Thêm legacy conflict handling.

### Phase 3 — P1 Minute lifecycle và loại nguồn

1. Giữ `sourceMemberType` tới DTO/UI.
2. Hiển thị đúng badge Guest/Support/Internal/Manual.
3. Thêm badge Đầu mối riêng.
4. Thay hard delete source participant bằng exclude/restore.
5. Sync tôn trọng tombstone.

### Phase 4 — P1/P2 Import UX

1. Tách import state theo target.
2. Render preview/confirmation đúng section.
3. Thêm Replace Both atomic.
4. Xử lý đầu mối khi replace.
5. Sửa wording và scroll/focus.
6. Chuyển hướng dẫn đầu mối vào tooltip.

### Phase 5 — Legacy và regression

1. Query/report dữ liệu trùng cũ.
2. Không auto merge.
3. Cung cấp workflow resolve có audit.
4. Chạy full test gate.

---

## 19. Test bắt buộc

### 19.1. Frontend unit/component

- Import khách render preview đúng section.
- Import hỗ trợ render preview đúng section.
- Hai preview cùng tồn tại.
- Replace both thành công nguyên tử.
- Replace both có conflict không thay đổi mảng cũ.
- Replace làm mất đầu mối thì yêu cầu chọn lại.
- Dedupe trong guest.
- Dedupe trong support.
- Dedupe chéo guest/support.
- Chuyển cùng member giữa hai loại giữ stable key.
- Contact snapshot trùng member hiển thị confirmation.
- Tooltip hoạt động mouse/keyboard/mobile.
- Source participant identity readonly.
- Manual participant identity editable.
- Badge Support không bị map thành Guest.
- Request form không hiển thị pending/internal Partner.
- React Query cache tách theo search mode.

### 19.2. Backend unit

- Duplicate policy không gộp chỉ bằng tên.
- Cùng stable ID được nhận là cùng người.
- Exact email/phone match đúng normalization.
- Candidate fingerprint không bị auto merge.
- Campus validator kiểm tra danh sách hợp nhất.
- Operational contact cùng member hợp lệ.
- Partner request selection chỉ chấp nhận ACTIVE+APPROVED+PUBLIC.
- Minute source participant identity readonly.
- Sync bỏ qua participant EXCLUDED.
- Source type mapping đủ bốn loại.

### 19.3. Backend integration

- Create request có guest/support trùng bị trả conflict.
- Direct API gửi pending Partner ID bị từ chối.
- Direct API gửi internal/rejected Partner ID bị từ chối.
- Partner management query vẫn xem pending theo quyền.
- Minute sync chạy hai lần không sinh trùng.
- Guest làm đầu mối xuất hiện một lần.
- External support làm đầu mối xuất hiện một lần và đúng badge/type.
- Unique source key chặn race/concurrent duplicate.
- Exclude source participant rồi sync không thêm lại.
- Restore participant hoạt động.
- Manual participant được sửa identity và persist.
- Source participant gửi identity edit bị từ chối rõ.
- Transaction rollback toàn bộ khi conflict/mapping thất bại.

### 19.4. E2E trọng yếu

1. Import riêng guest.
2. Import riêng support.
3. Hai file pending và Replace Both.
4. Conflict cùng người ở hai nhóm.
5. Chuyển Guest → Support giữ một member.
6. Xác nhận hai người khác nhau giữ hai dòng với badge đúng.
7. Chọn contact từ support và tạo đơn.
8. Minute autofill chỉ thêm contact một lần.
9. Loại source participant, sync lại không xuất hiện.
10. Người nguồn readonly; manual row sửa và lưu được.
11. Staff Leader không thấy Partner pending trong organization picker.
12. Matcher Partner vẫn cảnh báo hồ sơ pending để chống tạo trùng.

---

## 20. Definition of Done

Chỉ coi là hoàn thành khi:

```text
Import confirmation nằm đúng section và ghi rõ target
Hai import pending không ghi đè nhau
Có thể Replace Both theo cơ chế atomic
Không báo “Nhập thành công” trước khi apply dữ liệu
Không tạo duplicate chéo Guest/Support mà không có xác nhận
Chuyển loại giữ cùng stable member ID
Contact giống member được hỏi liên kết trước submit
Operational contact cùng member chỉ xuất hiện một lần trong Minute
External support hiển thị đúng “Nhân sự hỗ trợ đoàn”
Đầu mối hiển thị bằng badge bổ sung, không đổi loại nguồn
Source participant bị loại không quay lại sau sync
Không còn input cho sửa nhưng backend không persist
Manual participant vẫn sửa và lưu được
Request form chỉ đề xuất ACTIVE + APPROVED + PUBLIC Partner
Backend từ chối Partner ID không selectable
Dữ liệu legacy được cảnh báo, không tự gộp/xóa
```

Full gate:

```bash
dotnet test
npm run lint
npm run test:unit
npm run build
npm run test:e2e
```

Nếu E2E có baseline failures không liên quan, phải:

- Liệt kê chính xác spec/test lỗi.
- Chứng minh targeted tests của phạm vi này pass.
- Không báo “full gate xanh”.
- Không tự ý sửa unrelated tests trong cùng patch nếu chưa được duyệt scope.

---

## 21. Không được làm

- Không tự merge hai ID chỉ vì tên giống nhau.
- Không dùng array index làm identity.
- Không lọc Partner chỉ ở frontend.
- Không cho Staff Leader thấy pending Partner trong request organization picker chỉ vì đang đăng nhập.
- Không biến `EXTERNAL_SUPPORT` thành label `Khách`.
- Không hard delete source-linked Minute participant rồi kỳ vọng sync nhớ quyết định.
- Không render input editable nếu save handler không persist.
- Không mở khóa snapshot đầu mối sau submit nếu lifecycle hiện tại coi nó là immutable.
- Không tự xóa dữ liệu legacy duplicate bằng migration.
- Không tạo service/DTO/entity mới trùng với abstraction hiện có mà chưa audit code.

---

## 22. Báo cáo sau triển khai

Coding agent phải báo cáo theo tám mục:

1. Root cause thực tế của từng mã lỗi.
2. Danh sách file đã sửa.
3. Migration/schema/index đã thay đổi.
4. API/DTO contract đã thay đổi.
5. Logic frontend đã thay đổi.
6. Validation/backend domain policy đã thêm.
7. Test mới, test cập nhật và kết quả chạy.
8. Rủi ro, dữ liệu legacy, baseline failure và việc chưa hoàn thành.

Không kết luận hoàn thành nếu chỉ sửa giao diện mà chưa xử lý validation backend, persistence, source dedupe và Minute sync.
