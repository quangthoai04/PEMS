# PEMS — Đặc tả thay đổi luồng tạo tài khoản có bước xác nhận

## 1. Mục tiêu

Bổ sung một bước **xác nhận thông tin trước khi tạo tài khoản thật** trong chức năng **Quản lý tài khoản** do hai nhóm người dùng quản lý:

- **Head Office (HO)**
- **Staff Leader** (`role_code = STAFF`, `sub_role = LEADER`)

Mục tiêu chính:

- Giảm nguy cơ nhập nhầm email.
- Cho người tạo tài khoản kiểm tra lại vai trò, cơ sở và phòng ban trước khi gửi.
- Chỉ gọi API tạo tài khoản sau khi người dùng xác nhận lần cuối.
- Hiển thị rõ kết quả tạo tài khoản và kết quả gửi email thông báo.

Thay đổi này chỉ bổ sung một bước xác nhận ở frontend, không tạo thêm quy trình OTP hoặc xác nhận tài khoản phức tạp.

---

## 2. Phạm vi áp dụng

### 2.1. HO quản lý

HO có thể tạo các loại tài khoản theo nghiệp vụ hiện tại:

- Head Office.
- Staff Leader.

### 2.2. Staff Leader quản lý

Staff Leader có thể tạo các loại tài khoản theo nghiệp vụ hiện tại:

- IC Staff.
- Department Leader.
- Student.

Không thay đổi phạm vi quyền hiện có của HO hoặc Staff Leader.

---

## 3. Luồng hiện tại

```text
Mở form tạo tài khoản
→ Nhập thông tin
→ Bấm tạo
→ Frontend gọi API
→ Backend tạo tài khoản ACTIVE
→ Backend gửi email
→ Frontend hiển thị kết quả
```

### Vấn đề

Người quản lý có thể bấm tạo ngay sau khi nhập dữ liệu mà chưa có bước kiểm tra lại email.

Nếu email đúng định dạng, thuộc tên miền hợp lệ và chưa tồn tại trong hệ thống nhưng bị nhập nhầm sang địa chỉ của người khác, backend vẫn có thể tạo tài khoản và gửi thông báo đến sai người.

---

## 4. Luồng mới sau khi thay đổi

```text
Mở form tạo tài khoản
→ Chọn vai trò và nhập thông tin
→ Bấm “Tiếp tục”
→ Frontend kiểm tra toàn bộ dữ liệu
→ Hiển thị màn xác nhận
→ HO/Staff Leader kiểm tra lại thông tin
→ Bấm “Xác nhận tạo tài khoản”
→ Frontend mới gọi API
→ Backend tạo tài khoản ACTIVE
→ Backend gửi email
→ Frontend hiển thị riêng kết quả tạo tài khoản và kết quả gửi email
```

### Nguyên tắc quan trọng

- Bấm **“Tiếp tục”** chưa tạo tài khoản.
- Bấm **“Tiếp tục”** chưa gửi email.
- API tạo tài khoản chỉ được gọi sau khi bấm **“Xác nhận tạo tài khoản”**.
- Dữ liệu hiển thị trong màn xác nhận phải chính là dữ liệu được gửi tới backend.

---

## 5. Thay đổi tại form tạo tài khoản

### 5.1. Đổi tên nút

Nút hiện tại:

```text
Xác nhận tạo
```

Đổi thành:

```text
Tiếp tục
```

### 5.2. Hành vi khi bấm “Tiếp tục”

Frontend phải chạy lại toàn bộ validation hiện có, tối thiểu gồm:

- Đã chọn vai trò.
- Họ và tên hợp lệ.
- Email hợp lệ.
- Email thuộc tên miền cho phép.
- Email không vượt giới hạn độ dài.
- Đã chọn cơ sở khi nghiệp vụ yêu cầu.
- Campus còn hoạt động.
- Campus chưa có HO khi tạo HO.
- Campus chưa có Staff Leader khi tạo Staff Leader.
- Có IC department phù hợp khi tạo Staff Leader hoặc IC Staff.
- Đã chọn phòng ban khi tạo Department Leader.
- Phòng ban còn hoạt động.
- Phòng ban chưa có Department Leader phù hợp.
- Có MSSV khi tạo Student.
- MSSV hợp lệ.
- Các validation hiện có khác của form.

### 5.3. Khi dữ liệu không hợp lệ

Frontend phải:

- Không mở màn xác nhận.
- Không gọi API.
- Không tạo tài khoản.
- Không gửi email.
- Giữ nguyên dữ liệu đã nhập.
- Hiển thị lỗi đúng tại trường tương ứng.
- Không reset role, campus, department hoặc email.

### 5.4. Khi dữ liệu hợp lệ

Frontend phải:

- Chuẩn hóa dữ liệu theo logic hiện có.
- Tạo một snapshot dữ liệu chờ gửi.
- Tạo summary để hiển thị.
- Mở màn xác nhận.
- Không gọi API tại bước này.

---

## 6. Màn xác nhận thông tin tài khoản

### 6.1. Tiêu đề

```text
XÁC NHẬN THÔNG TIN TÀI KHOẢN
```

### 6.2. Nội dung mẫu

```text
Họ và tên: Nguyễn Văn A

Email đăng nhập:
nguyenvana@fpt.edu.vn

Vai trò: Staff Leader — Trưởng phòng IC
Cơ sở: FPT University TP.HCM
Phòng ban: Phòng Hợp tác Quốc tế
```

Thông báo bên dưới:

> Thông báo tài khoản và quyền đăng nhập sẽ được gửi tới địa chỉ email trên.  
> Vui lòng kiểm tra kỹ email trước khi xác nhận tạo tài khoản.

### 6.3. Hai nút bắt buộc

```text
Quay lại chỉnh sửa
Xác nhận tạo tài khoản
```

Không thêm:

- Ô nhập lại email.
- Checkbox xác nhận.
- OTP.
- Invitation token.
- Bước duyệt lần hai.

---

## 7. Cách làm nổi bật email

Email là thông tin quan trọng nhất và dễ nhập nhầm nhất.

Không hiển thị email như một dòng thông tin bình thường.

### 7.1. Bố cục đề xuất

```text
┌─────────────────────────────────────────────┐
│ EMAIL ĐĂNG NHẬP                             │
│                                             │
│ nguyenvana@fpt.edu.vn                       │
│                                             │
│ Hãy kiểm tra kỹ địa chỉ email này.          │
│ Thông báo và quyền đăng nhập sẽ được gửi    │
│ tới địa chỉ trên.                           │
└─────────────────────────────────────────────┘
```

### 7.2. Yêu cầu UI

- Nền vàng nhạt hoặc cam nhạt.
- Viền cảnh báo nhẹ.
- Email dùng chữ đậm.
- Email không bị cắt bằng dấu `...`.
- Email dài phải có thể xuống dòng an toàn.
- Không dùng nền đỏ vì đây chưa phải lỗi.
- Không chỉ dựa vào màu sắc; phải có nội dung nhắc kiểm tra email.

### 7.3. Gợi ý màu theo PEMS

- Background: `amber-50` hoặc `orange-50`.
- Border: `amber-200` hoặc `orange-200`.
- Email: `slate-900` hoặc `#004c91`.
- Text phụ: `slate-600`.

---

## 8. Thông tin hiển thị theo từng loại tài khoản

### 8.1. HO tạo Head Office

Hiển thị:

- Họ và tên.
- Email đăng nhập.
- Vai trò: **Head Office**.
- Cơ sở.

Không hiển thị phòng ban.

### 8.2. HO tạo Staff Leader

Hiển thị:

- Họ và tên.
- Email đăng nhập.
- Vai trò: **Staff Leader — Trưởng phòng IC**.
- Cơ sở.
- Phòng ban: tên phòng IC thực tế của campus.

Không hiển thị ID kỹ thuật của campus hoặc department.

### 8.3. Staff Leader tạo IC Staff

Hiển thị:

- Họ và tên.
- Email đăng nhập.
- Vai trò: **IC Staff**.
- Cơ sở của Staff Leader đang đăng nhập.
- Phòng ban IC của Staff Leader.

Campus và phòng ban phải lấy từ dữ liệu hiện tại của hệ thống, không lấy từ một giá trị client tùy ý.

### 8.4. Staff Leader tạo Department Leader

Hiển thị:

- Họ và tên.
- Email đăng nhập.
- Vai trò: **Department Leader — Trưởng phòng ban**.
- Cơ sở.
- Phòng ban đã chọn.

Tên phòng ban phải là label hiển thị, không phải `departmentId`.

### 8.5. Staff Leader tạo Student

Hiển thị:

- Họ và tên.
- Email đăng nhập.
- Vai trò: **Student**.
- Cơ sở.
- Mã số sinh viên.

Không hiển thị phòng ban nếu Student không có phòng ban trong nghiệp vụ hiện tại.

### 8.6. Trường tùy chọn

Có thể hiển thị số điện thoại nếu trường có giá trị và cần người quản lý kiểm tra.

Không hiển thị:

- `null`.
- `undefined`.
- Chuỗi rỗng.
- Role code thô như `STAFF`, `DEPARTMENT`, `LEADER`.
- ID kỹ thuật của role, campus hoặc department.
- Các trường không áp dụng cho loại tài khoản đang tạo.

---

## 9. Tên vai trò hiển thị

Dùng tên thân thiện thay vì giá trị kỹ thuật.

| Role/SubRole | Tên hiển thị |
|---|---|
| `HO` | Head Office |
| `STAFF + LEADER` | Staff Leader — Trưởng phòng IC |
| `STAFF + STAFF` | IC Staff |
| `DEPARTMENT + LEADER` | Department Leader — Trưởng phòng ban |
| `STUDENT` | Student |

Nếu codebase đã có helper hiển thị role, phải tái sử dụng helper hiện có.

Không suy luận chỉ từ `roleCode` nếu cùng một role code có ý nghĩa khác tùy actor và luồng tạo.

---

## 10. Snapshot dữ liệu chờ tạo

Khi bấm **“Tiếp tục”**, frontend cần tạo một snapshot của dữ liệu sẽ gửi.

### 10.1. Payload chờ gửi

Ví dụ:

```text
roleCode
subRole
fullName
email
phone
gender
primaryCampusId
departmentId
studentCode
```

Tùy theo API hiện tại, chỉ giữ các trường đang thực sự được gửi.

### 10.2. Summary dùng để hiển thị

Ví dụ:

```text
fullName
email
roleDisplayName
campusDisplayName
departmentDisplayName
studentCode
phone
```

### 10.3. Nguyên tắc đồng nhất

```text
Thông tin hiển thị trong màn xác nhận
=
Thông tin được gửi tới API
```

Không được xảy ra trường hợp màn xác nhận hiển thị email A nhưng API lại gửi email B.

Khi quay lại chỉnh sửa, snapshot cũ nên bị xóa. Lần bấm **“Tiếp tục”** tiếp theo phải tạo snapshot mới từ dữ liệu mới nhất.

---

## 11. Hành vi của nút “Quay lại chỉnh sửa”

Khi bấm:

```text
Đóng màn xác nhận
→ Quay lại form tạo tài khoản
→ Giữ nguyên toàn bộ dữ liệu đã nhập
```

Phải giữ nguyên:

- Vai trò.
- Cơ sở.
- Phòng ban.
- Họ và tên.
- Email.
- Số điện thoại.
- MSSV.
- Các trường khác hiện có.

Không được:

- Reset form.
- Gọi API.
- Mất dữ liệu người dùng đã nhập.

Ví dụ:

```text
Email sai: nguyenvanaa@fpt.edu.vn
→ Quay lại chỉnh sửa
→ Sửa thành: nguyenvana@fpt.edu.vn
→ Bấm “Tiếp tục”
→ Màn xác nhận phải hiển thị email mới
```

---

## 12. Hành vi của nút “Xác nhận tạo tài khoản”

Khi bấm:

```text
Khóa các nút
→ Hiển thị “Đang tạo...”
→ Gọi API create account hiện tại
→ Chờ phản hồi backend
```

### 12.1. Trong lúc request đang chạy

Phải:

- Disable nút xác nhận.
- Disable nút quay lại chỉnh sửa.
- Không cho bấm nút X để đóng.
- Không cho click overlay để đóng.
- Không cho gửi request lần hai.
- Không tạo hai tài khoản khi double-click.

Nhãn nút:

```text
Xác nhận tạo tài khoản
```

đổi thành:

```text
Đang tạo...
```

---

## 13. Kết quả sau khi tạo tài khoản

### 13.1. Tạo tài khoản và gửi email thành công

Hiển thị:

> Đã tạo tài khoản **{email}** thành công.  
> Email thông báo đã được gửi tới địa chỉ này.

Sau đó:

- Đóng màn xác nhận.
- Đóng form tạo tài khoản.
- Reset dữ liệu form.
- Xóa snapshot chờ tạo.
- Tải lại danh sách tài khoản.
- Cập nhật lại thống kê nếu trang hiện tại có thống kê.

### 13.2. Tạo tài khoản thành công nhưng gửi email thất bại

Hiển thị:

> Tài khoản **{email}** đã được tạo thành công, nhưng hệ thống chưa gửi được email thông báo.

Sau đó vẫn phải:

- Đóng màn xác nhận.
- Đóng form tạo tài khoản.
- Reset dữ liệu form.
- Xóa snapshot chờ tạo.
- Tải lại danh sách tài khoản.
- Cập nhật lại thống kê.

Không được:

- Hiển thị rằng tạo tài khoản thất bại.
- Gọi lại API tạo tài khoản.
- Tự động tạo lần hai.

Lý do: tài khoản đã tồn tại trong database.

### 13.3. Backend từ chối tạo tài khoản

Các trường hợp có thể xảy ra:

- Email vừa được người khác sử dụng.
- MSSV vừa được sử dụng.
- Campus vừa có HO.
- Campus vừa có Staff Leader.
- Department vừa có Department Leader.
- Campus hoặc department bị vô hiệu hóa.
- Dữ liệu availability thay đổi trong thời gian màn xác nhận đang mở.

Khi backend trả lỗi:

- Không reset form.
- Không xóa dữ liệu.
- Không đóng form tạo tài khoản hoàn toàn.
- Đóng màn xác nhận và quay lại form.
- Hiển thị lỗi tại trường phù hợp.
- Xóa snapshot bị lỗi.
- Cho phép người dùng sửa rồi bấm “Tiếp tục” lại.

Mapping lỗi:

- Trùng email → trường email.
- Trùng MSSV → trường MSSV.
- Lỗi campus → trường campus hoặc thông báo chung.
- Lỗi department → trường department hoặc thông báo chung.
- Lỗi availability → thông báo rõ ngay tại form.

Không tự động gọi API lần hai.

---

## 14. Phân biệt kết quả tạo tài khoản và gửi email

Frontend không được chỉ hiển thị một thông báo chung.

Cần phân biệt:

### Trường hợp A

```text
Tạo tài khoản: Thành công
Gửi email: Thành công
```

### Trường hợp B

```text
Tạo tài khoản: Thành công
Gửi email: Thất bại
```

### Trường hợp C

```text
Tạo tài khoản: Thất bại
Gửi email: Không thực hiện
```

Frontend phải dựa trên response hiện tại của API, bao gồm trạng thái gửi email nếu backend đã trả về.

---

## 15. Quản lý state frontend

Nên có các state hoặc cấu trúc tương đương:

```text
isCreateConfirmOpen
pendingCreatePayload
pendingCreateSummary
isCreating
```

Tên có thể thay đổi theo convention hiện tại.

### 15.1. Khi mở màn xác nhận

- `pendingCreatePayload` chứa dữ liệu sẽ gửi.
- `pendingCreateSummary` chứa label hiển thị.
- `isCreateConfirmOpen = true`.

### 15.2. Khi quay lại chỉnh sửa

- `isCreateConfirmOpen = false`.
- Xóa snapshot chờ tạo.
- Giữ nguyên state form.

### 15.3. Khi tạo thành công

- Đóng cả hai modal.
- Xóa payload chờ tạo.
- Xóa summary.
- Reset form.
- Refetch danh sách và thống kê.

### 15.4. Khi backend trả lỗi

- Đóng màn xác nhận.
- Xóa snapshot lỗi.
- Giữ nguyên form.
- Map lỗi về trường phù hợp.

---

## 16. Tách trách nhiệm xử lý

Không nên để một hàm vừa validate, vừa mở modal, vừa gọi API.

### 16.1. Bước chuẩn bị xác nhận

Một hàm tương đương:

```text
handleContinueCreateAccount
```

Trách nhiệm:

1. Xóa hoặc chuẩn hóa lỗi cũ phù hợp.
2. Chạy validation.
3. Resolve role thực tế.
4. Resolve campus.
5. Resolve department.
6. Chuẩn hóa họ tên và email.
7. Tạo payload chờ gửi.
8. Tạo summary hiển thị.
9. Mở màn xác nhận.
10. Không gọi API.

### 16.2. Bước tạo tài khoản thật

Một hàm tương đương:

```text
confirmCreateAccount
```

Trách nhiệm:

1. Kiểm tra snapshot tồn tại.
2. Đặt trạng thái loading.
3. Gọi API bằng snapshot.
4. Xử lý kết quả tạo.
5. Xử lý trạng thái gửi email.
6. Reset state khi thành công.
7. Giữ form khi backend từ chối.
8. Chống double-submit.

---

## 17. Modal lồng trên form tạo tài khoản

Nếu form tạo tài khoản hiện tại đã là modal, màn xác nhận sẽ nằm phía trên modal đó.

Yêu cầu:

- Z-index cao hơn form tạo.
- Không click xuyên xuống form dưới.
- Không làm mất state form cha.
- Body modal có thể scroll nếu nội dung dài.
- Footer nút luôn dễ truy cập.
- Không vượt quá chiều cao màn hình.
- Responsive trên mobile.
- Không cho đóng trong lúc đang tạo tài khoản.

### Gợi ý thiết kế PEMS

- Primary: `#004c91`.
- Accent: `#F37021`.
- Border: `slate-200`.
- Text: `slate-700` hoặc `slate-900`.
- Warning block: `amber-50` hoặc `orange-50`.
- Không dùng gradient mạnh.
- Không dùng shadow quá nặng.
- Không thêm animation phức tạp.

---

## 18. Accessibility và UX

Nên bảo đảm:

- Modal có `role="dialog"`.
- Có `aria-modal="true"`.
- Tiêu đề được liên kết bằng `aria-labelledby`.
- Nút có `type="button"` nếu nằm trong form.
- Nút đóng có `aria-label`.
- Email dài dùng `break-words` hoặc cách xử lý tương đương.
- Không chỉ dùng màu để truyền đạt cảnh báo.
- Khi mở modal, focus chuyển vào modal.
- Khi quay lại, focus trở về nút “Tiếp tục” hoặc trường email nếu phù hợp.
- Escape chỉ đóng modal khi chưa gửi request.
- Escape không đóng modal trong lúc `isCreating = true`.

Không cần thêm thư viện focus trap mới nếu codebase chưa dùng.

---

## 19. Những phần không thay đổi

### 19.1. Backend

Không thay đổi:

- AccountsController.
- CreateAccountCommand.
- CreateAccountCommandValidator.
- CreateAccountCommandHandler.
- CreateAccountResponse.
- AccountProvisioningRules.
- Logic tạo tài khoản ACTIVE.
- Logic kiểm tra role/campus/department.
- Logic audit.
- Logic gửi email.
- Logic trả trạng thái gửi email.

### 19.2. Database

Không thay đổi:

- Schema.
- Table.
- Column.
- Constraint.
- Trigger.
- Seed.
- Migration.

### 19.3. Xác thực

Không thêm:

- OTP.
- Invitation token.
- Email confirmation token.
- Trạng thái pending.
- Workflow accept tài khoản.

---

## 20. Những thay đổi không nằm trong phạm vi

Không triển khai:

- Nhập email hai lần.
- Checkbox “Tôi xác nhận email đúng”.
- Người nhận email phải bấm xác nhận.
- Trạng thái `PENDING_EMAIL_CONFIRMATION`.
- Quy trình duyệt tài khoản lần hai.
- Tự động xóa tài khoản khi gửi email thất bại.
- Tự động retry tạo tài khoản.
- Thay đổi nội dung email.
- Thay đổi chức năng sửa email.
- Thay đổi quyền của HO hoặc Staff Leader.

---

## 21. Các test bắt buộc

### 21.1. Validation

- Dữ liệu không hợp lệ.
- Bấm “Tiếp tục”.
- Không mở màn xác nhận.
- Không gọi API.
- Dữ liệu form không bị mất.

### 21.2. HO tạo Head Office

- Hiển thị đúng họ tên.
- Hiển thị email.
- Hiển thị Head Office.
- Hiển thị campus.
- Không hiển thị department.
- API chưa được gọi khi modal vừa mở.

### 21.3. HO tạo Staff Leader

- Hiển thị Staff Leader — Trưởng phòng IC.
- Hiển thị đúng campus.
- Hiển thị đúng IC department.
- Email được làm nổi bật.
- API chỉ gọi khi bấm xác nhận.

### 21.4. Staff Leader tạo IC Staff

- Hiển thị IC Staff.
- Hiển thị campus của actor.
- Hiển thị phòng IC.
- Không phụ thuộc campus giả từ client.

### 21.5. Staff Leader tạo Department Leader

- Hiển thị đúng department.
- Payload chứa đúng `departmentId`.

### 21.6. Staff Leader tạo Student

- Hiển thị Student.
- Hiển thị campus.
- Hiển thị MSSV.
- Không hiển thị department nếu không có.

### 21.7. Quay lại chỉnh sửa

- Đóng màn xác nhận.
- Form giữ nguyên dữ liệu.
- Sửa email.
- Mở xác nhận lại.
- Email mới được hiển thị.

### 21.8. Double-submit

- Bấm xác nhận nhiều lần.
- API chỉ được gọi một lần.
- Nút bị disable trong lúc gửi.

### 21.9. Gửi email thành công

- Tạo tài khoản thành công.
- Trạng thái gửi email thành công.
- Toast hiển thị đúng.
- Form reset.
- Danh sách được tải lại.

### 21.10. Gửi email thất bại

- Tài khoản vẫn được coi là tạo thành công.
- Hiển thị cảnh báo gửi email thất bại.
- Không gọi create account lần hai.
- Danh sách vẫn được tải lại.

### 21.11. Backend trả lỗi

- Trùng email.
- Không reset form.
- Lỗi hiển thị tại email.
- Người dùng có thể sửa và tiếp tục lại.

### 21.12. Snapshot đồng nhất

- Email hiển thị trong xác nhận giống email gửi API.
- Role/campus/department hiển thị giống dữ liệu gửi API.
- Không có sai lệch giữa summary và payload.

---

## 22. Acceptance Criteria

Task được coi là hoàn thành khi:

- Nút form đổi thành **“Tiếp tục”**.
- Bấm “Tiếp tục” chưa gọi API.
- Validation hiện tại vẫn hoạt động.
- Chỉ dữ liệu hợp lệ mới mở màn xác nhận.
- Màn xác nhận hiển thị:
  - Họ và tên.
  - Email.
  - Vai trò thân thiện.
  - Cơ sở.
  - Phòng ban nếu có.
  - MSSV nếu là Student.
- Email nằm trong khối nổi bật riêng.
- Có đúng hai nút:
  - Quay lại chỉnh sửa.
  - Xác nhận tạo tài khoản.
- Quay lại chỉnh sửa không làm mất dữ liệu.
- API chỉ gọi khi bấm xác nhận.
- Không thể double-submit.
- Kết quả tạo tài khoản và gửi email được phân biệt.
- Email gửi thất bại không bị hiểu là tạo tài khoản thất bại.
- Backend error không làm mất form.
- Không có thay đổi backend.
- Không có thay đổi database.
- Không thêm nhập lại email.
- Không thêm checkbox.
- Không thêm OTP.
- Frontend build thành công.

---

## 23. Kết quả cuối cùng

Luồng hoàn chỉnh sau khi triển khai:

```text
Nhập thông tin
→ Bấm “Tiếp tục”
→ Validation
→ Xem lại thông tin
→ Email được làm nổi bật
→ Có thể quay lại sửa mà không mất dữ liệu
→ Bấm “Xác nhận tạo tài khoản”
→ Backend tạo tài khoản ACTIVE
→ Backend gửi email
→ Hiển thị rõ kết quả tạo tài khoản
→ Hiển thị rõ kết quả gửi email
```

Thay đổi này không loại bỏ hoàn toàn khả năng nhập nhầm email, nhưng giảm đáng kể rủi ro mà không làm quy trình tạo tài khoản trở nên phức tạp.
