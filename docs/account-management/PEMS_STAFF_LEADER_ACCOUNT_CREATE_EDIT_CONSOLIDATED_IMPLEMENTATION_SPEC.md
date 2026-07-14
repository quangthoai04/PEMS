# PEMS — ĐẶC TẢ HỢP NHẤT TẠO VÀ CHỈNH SỬA TÀI KHOẢN CHO STAFF LEADER

## 0. Trạng thái và phạm vi tài liệu

- Dự án: `quangthoai04/PEMS`
- Module: Quản lý tài khoản
- Người thao tác: `role_code = STAFF`, `sub_role = LEADER`
- Phạm vi:
  1. Chỉnh sửa thông tin và vai trò của tài khoản thuộc campus STAFF LEADER.
  2. Bổ sung ô nhập MSSV khi tạo tài khoản `STUDENT`.
- Database tham chiếu: SQL v10 hiện tại của dự án.
- Tài liệu này **thay thế toàn bộ các yêu cầu cũ mâu thuẫn** liên quan đến:
  - Họ tên và Email luôn bị disable trong modal chỉnh sửa.
  - Modal chỉ được hiểu là “Chỉnh sửa vai trò”.
  - Form tạo STUDENT không có ô MSSV.

Agent phải đọc source code hiện tại trước khi sửa và chỉ bổ sung phần còn thiếu. Không được viết lại hoặc phá vỡ các flow đã hoạt động.

---

# 1. Bối cảnh chức năng hiện tại

## 1.1. Chức năng tạo tài khoản đã hoàn thành

STAFF LEADER hiện đã tạo được:

| Loại tài khoản | `role_code` | `sub_role` |
|---|---|---|
| Nhân sự IC | `STAFF` | `STAFF` |
| Trưởng phòng ban | `DEPARTMENT` | `LEADER` |
| Sinh viên | `STUDENT` | `NULL` |

Yêu cầu mới **không thay đổi quyền tạo tài khoản** và không thay đổi các loại tài khoản được phép tạo.

Phần cần bổ sung duy nhất trong luồng tạo tài khoản:

```text
Khi chọn STUDENT
→ Hiển thị ô MSSV
→ Bắt buộc nhập MSSV
→ Gửi studentCode trong request
→ Backend lưu vào users.student_code
```

## 1.2. Chức năng chỉnh sửa vai trò đã hoàn thành

Flow hiện tại đã hỗ trợ thay đổi tài khoản giữa:

```text
STAFF + STAFF
DEPARTMENT + LEADER
STUDENT + NULL
```

Các hành vi đã hoàn thành phải được giữ nguyên:

- Giới tính null/rỗng hiển thị `-`, không tự chọn `Nam`.
- Render form theo role đang chọn, không render theo role cũ.
- `STAFF` tự thuộc phòng IC đang hoạt động cùng campus.
- `DEPARTMENT` chọn phòng `GENERAL`, `ACTIVE`, cùng campus.
- Đồng bộ `departments.head_user_id`.
- `STUDENT` có MSSV.
- Chuyển khỏi STUDENT phải xóa `student_code`.
- Backend kiểm tra campus, department, trưởng phòng và MSSV.
- Không dùng chung state giữa modal tạo và modal chỉnh sửa.

Phần mới cần bổ sung trong luồng chỉnh sửa:

```text
STAFF LEADER được sửa thêm Họ và tên, Email
cho đúng các loại target được quy định trong tài liệu này.
```

---

# 2. Role/Sub-role canonical bắt buộc

Chỉ dùng các giá trị runtime sau:

```text
ADMIN
HO
STAFF
DEPARTMENT
STUDENT
VISITOR
```

Sub-role:

```text
LEADER
STAFF
NULL
```

Mapping áp dụng trong tài liệu:

| Effective role | `role_code` | `sub_role` |
|---|---|---|
| STAFF LEADER | `STAFF` | `LEADER` |
| Nhân sự IC | `STAFF` | `STAFF` |
| Trưởng phòng ban | `DEPARTMENT` | `LEADER` |
| Nhân sự phòng ban | `DEPARTMENT` | `STAFF` |
| Sinh viên | `STUDENT` | `NULL` |

Không dùng:

```text
STAFF_LEADER
DEPARTMENT_LEADER
DEPT_LEADER
DEPT
STAFF_L
STAFF_P
DEPT_L
DEPT_P
```

---

# 3. Quy tắc phân quyền chung

## 3.1. Caller hợp lệ

Người thao tác phải thỏa:

```text
role_code = STAFF
sub_role = LEADER
status = ACTIVE
```

Backend phải kiểm tra độc lập với frontend.

## 3.2. Scope target khi chỉnh sửa

Target phải:

- Thuộc cùng campus với STAFF LEADER.
- Không phải chính tài khoản STAFF LEADER đang đăng nhập.
- Không thuộc ngoài phạm vi quản lý hiện tại.
- Không ở trạng thái `LOCKED` nếu flow hiện tại đang chặn tài khoản LOCKED.
- Thỏa các business rule hiện có của module Account Management.

Nếu frontend bị bypass, backend vẫn phải trả `403`, `404`, `409` hoặc business error theo pattern hiện có.

## 3.3. Không thay đổi quyền tạo tài khoản

STAFF LEADER tiếp tục tạo được:

```text
STAFF + STAFF
DEPARTMENT + LEADER
STUDENT + NULL
```

Không mở thêm quyền tạo:

```text
ADMIN
HO
STAFF + LEADER
DEPARTMENT + STAFF
VISITOR
```

trừ khi source code hiện tại đã có policy khác được chốt rõ.

---

# 4. PHẦN A — CHỈNH SỬA TÀI KHOẢN

# 4.1. Đổi phạm vi tên chức năng

Modal không còn chỉ chỉnh role, vì vậy UI nên đổi:

```text
Chỉnh sửa vai trò
→ Chỉnh sửa tài khoản
```

Tiêu đề đề xuất:

```text
Chỉnh sửa thông tin tài khoản
```

Nút submit:

```text
Cập nhật
```

Không bắt buộc đổi route hoặc tên backend command nếu việc đổi tên gây refactor lớn. Có thể giữ `UpdateAccountRole` để đảm bảo compatibility, nhưng hành vi phải hỗ trợ cập nhật thông tin tài khoản theo đặc tả này.

---

# 4.2. Target được sửa Họ tên và Email

STAFF LEADER được sửa **Họ và tên** và **Email** khi role/sub-role gốc của target trong database là:

| Target gốc | Họ tên | Email |
|---|---:|---:|
| `STAFF + STAFF` | Editable | Editable |
| `DEPARTMENT + LEADER` | Editable | Editable |
| `STUDENT + NULL` | Editable | Editable |

Các target sau không được sửa Họ tên và Email trong flow này:

| Target gốc | Kết quả |
|---|---|
| `STAFF + LEADER` | Disable / backend từ chối thay đổi |
| `DEPARTMENT + STAFF` | Disable / backend từ chối thay đổi |
| `ADMIN` | Disable |
| `HO` | Disable |
| `VISITOR` | Disable |
| Ngoài campus | Không cho thao tác |
| Chính caller | Không cho thao tác |
| `LOCKED` | Không cho thao tác theo rule hiện tại |

## 4.2.1. Quy tắc chống mở quyền sai

Quyền sửa Họ tên và Email phải được xác định từ:

```text
role_code + sub_role gốc của target lúc load từ database
```

Không được xác định từ:

```text
newRoleCode
role đang chọn trong dropdown
state đã bị người dùng thay đổi
```

Ví dụ bắt buộc:

1. Target ban đầu là `STAFF + STAFF`, chọn role mới `STUDENT`:
   - Họ tên và Email vẫn editable.

2. Target ban đầu là `DEPARTMENT + STAFF`, chọn role mới `STUDENT`:
   - Họ tên và Email vẫn disable.

3. Target ban đầu là `STUDENT`, chọn role mới `DEPARTMENT`:
   - Họ tên và Email vẫn editable.

Frontend tính quyền từ snapshot ban đầu. Backend phải tự tính lại từ bản ghi database.

---

# 4.3. Ma trận trường trong modal chỉnh sửa

| Trường | Target được sửa identity | Target không được sửa identity |
|---|---:|---:|
| Họ và tên | Editable | Disable |
| Email | Editable | Disable |
| Giới tính | Disable | Disable |
| Số điện thoại | Disable | Disable |
| Cơ sở trực thuộc | Disable | Disable |
| Vai trò | Theo flow hiện tại | Theo flow hiện tại |
| Chức vụ | Theo role đang chọn | Theo role đang chọn |
| Phòng ban | Theo role đang chọn | Theo role đang chọn |
| MSSV | Theo role đang chọn | Theo role đang chọn |

Quy tắc hiển thị chung:

- `null`, `undefined` hoặc chuỗi rỗng hiển thị `-`.
- Giới tính không dùng fallback `Nam`.
- Không mutate `selectedAccount` khi người dùng nhập form.
- Card/thẻ thông tin bên trái giữ snapshot ban đầu.
- Card chỉ cập nhật sau khi save thành công và refetch.

---

# 4.4. Họ và tên

Khi editable:

- Dùng input text.
- Khởi tạo từ dữ liệu chi tiết target.
- Bắt buộc nhập.
- Trim khoảng trắng đầu/cuối.
- Không chấp nhận chuỗi chỉ có khoảng trắng.
- Tối đa 150 ký tự theo schema hiện tại.
- Không tự thay đổi hoa/thường.
- Không tự chuẩn hóa tên vượt ngoài rule hiện tại.
- Backend lỗi thì giữ nguyên nội dung người dùng đã nhập.

Khi không editable:

- Disable input.
- Hiển thị snapshot hiện tại.
- Nếu null/rỗng hiển thị `-`.

Thông báo lỗi đề xuất:

```text
Vui lòng nhập họ và tên.
Họ và tên không được vượt quá 150 ký tự.
```

---

# 4.5. Email

Khi editable:

- Dùng `input type="email"`.
- Khởi tạo từ email hiện tại.
- Bắt buộc nhập.
- Trim khoảng trắng đầu/cuối.
- Đúng định dạng email.
- Tối đa 150 ký tự.
- Không trùng email của user khác.
- Khi kiểm tra trùng phải loại trừ chính target.
- Dùng cùng quy tắc normalize email hiện có của dự án.
- Backend lỗi thì giữ modal mở và giữ dữ liệu.

Khi không editable:

- Disable input.
- Hiển thị snapshot hiện tại.
- Nếu null/rỗng hiển thị `-`.

Thông báo lỗi đề xuất:

```text
Vui lòng nhập địa chỉ email.
Địa chỉ email không hợp lệ.
Email không được vượt quá 150 ký tự.
Email này đã được sử dụng bởi tài khoản khác.
Bạn không có quyền chỉnh sửa họ tên hoặc email của tài khoản này.
```

Không được sửa trong flow này:

```text
password_hash
fe_id
provider subject
provider secret
provider token
SSO credential
```

Nếu Email thay đổi, giữ cơ chế revoke session hiện tại sau khi transaction thành công. Không revoke session trước khi database commit.

---

# 4.6. Trạng thái disable phải có màu xám rõ ràng

Áp dụng cho:

- Họ tên khi không được sửa.
- Email khi không được sửa.
- Giới tính.
- Số điện thoại.
- Cơ sở trực thuộc.
- Chức vụ.
- Phòng IC của role STAFF.
- Field bị disable trong lúc loading/submitting.

Class Tailwind đề xuất:

```tsx
disabled:bg-slate-100
disabled:text-slate-500
disabled:border-slate-200
disabled:cursor-not-allowed
disabled:opacity-100
```

Ví dụ:

```tsx
className="
  w-full rounded-xl border border-slate-300 bg-white px-4 py-3
  text-sm text-slate-800
  focus:border-[#004c91]
  focus:outline-none
  focus:ring-1
  focus:ring-[#004c91]
  disabled:bg-slate-100
  disabled:text-slate-500
  disabled:border-slate-200
  disabled:cursor-not-allowed
  disabled:opacity-100
"
```

Không dùng opacity thấp cho toàn field vì làm dữ liệu khó đọc.

---

# 4.7. Hành vi theo role đang chọn trong modal chỉnh sửa

Việc render phải dựa trên:

```text
accountEditForm.roleCode
```

Không dựa trên:

```text
selectedAccount.role
```

## 4.7.1. Chọn `STAFF`

UI:

- Chức vụ: `Nhân viên`, disable.
- Phòng ban: phòng IC `ACTIVE` của campus caller, disable.
- Không hiển thị MSSV.
- Xóa `departmentId` đã chọn ở role DEPARTMENT.
- Xóa `studentCode` tạm ở role STUDENT.
- Nếu không có phòng IC hợp lệ, hiển thị lỗi và disable Cập nhật.

Backend lưu:

```text
role_code = STAFF
sub_role = STAFF
primary_campus_id = campus caller
department_id = phòng IC ACTIVE của campus caller
student_code = NULL
```

Không tin `departmentId` từ client cho role STAFF.

## 4.7.2. Chọn `DEPARTMENT`

UI:

- Chức vụ: `Trưởng phòng`, disable.
- Hiển thị dropdown Phòng ban.
- Chỉ hiển thị phòng:
  - Cùng campus caller.
  - `department_type = GENERAL`.
  - `status = ACTIVE`.
- Placeholder: `-- Chọn phòng ban --`.
- Phòng chưa có head: selectable.
- Phòng có head khác: disable, nhãn `— Đã có trưởng phòng`.
- Phòng hiện tại do chính target làm head: selectable/preselected.
- Không hiển thị MSSV.
- Chưa chọn phòng hợp lệ thì disable Cập nhật.

Backend lưu:

```text
role_code = DEPARTMENT
sub_role = LEADER
primary_campus_id = campus caller
department_id = selected GENERAL department
student_code = NULL
```

Backend phải kiểm tra lại department tại thời điểm submit để xử lý race condition.

## 4.7.3. Chọn `STUDENT`

UI:

- Không render Chức vụ.
- Không render Phòng ban.
- Giữ Cơ sở trực thuộc và disable.
- Hiển thị `Mã số sinh viên (MSSV)`.
- MSSV bắt buộc.
- Trim đầu/cuối.
- Tối đa 30 ký tự.
- Không tự áp regex nếu dự án chưa có chuẩn MSSV.
- Không tự đổi hoa/thường.

Khởi tạo:

- Target ban đầu là STUDENT: giữ MSSV hiện tại khi vừa mở modal.
- Target ban đầu không phải STUDENT: MSSV trống khi chuyển sang STUDENT.
- Rời STUDENT: clear state MSSV.
- Quay lại STUDENT sau khi đã rời: MSSV trống.

Backend lưu:

```text
role_code = STUDENT
sub_role = NULL
primary_campus_id = campus caller
department_id = NULL
student_code = studentCode đã trim
```

MSSV phải không trùng user khác, loại trừ chính target khi cập nhật.

---

# 4.8. Đồng bộ trưởng phòng

Giữ và hoàn thiện logic hiện tại:

1. Load department cũ.
2. Nếu target rời phòng cũ và:
   ```text
   oldDepartment.head_user_id == targetUserId
   ```
   thì clear `head_user_id`.

3. Nếu role mới là DEPARTMENT:
   - Load phòng mới trong transaction.
   - Kiểm tra `ACTIVE`.
   - Kiểm tra `GENERAL`.
   - Kiểm tra cùng campus.
   - Kiểm tra chưa có head khác.
   - Gán:
     ```text
     newDepartment.head_user_id = targetUserId
     ```

4. Update user và department trong cùng transaction/SaveChanges phù hợp với convention hiện tại.

Không được thay thế một trưởng phòng khác thông qua dropdown này nếu business rule hiện tại không cho phép.

---

# 4.9. State frontend cho modal chỉnh sửa

Dùng state riêng:

```ts
interface AccountEditForm {
  fullName: string;
  email: string;
  roleCode: 'STAFF' | 'DEPARTMENT' | 'STUDENT';
  departmentId: string;
  studentCode: string;
}
```

Ví dụ khởi tạo:

```ts
const initialAccountEditForm: AccountEditForm = {
  fullName: selectedAccount.fullName ?? '',
  email: selectedAccount.email ?? '',
  roleCode: selectedAccount.role,
  departmentId:
    selectedAccount.role === 'DEPARTMENT'
      ? selectedAccount.departmentId ?? ''
      : '',
  studentCode:
    selectedAccount.role === 'STUDENT'
      ? selectedAccount.studentCode ?? ''
      : '',
};
```

Quyền editable:

```ts
const canEditIdentityFields =
  (selectedAccount.role === 'STAFF' &&
    selectedAccount.subRole === 'STAFF') ||
  (selectedAccount.role === 'DEPARTMENT' &&
    selectedAccount.subRole === 'LEADER') ||
  selectedAccount.role === 'STUDENT';
```

Không dùng chung state với modal tạo:

```text
createForm.studentCode
createDepartmentId
accountEditForm.studentCode
```

phải là các state tách biệt.

---

# 4.10. `isDirty` và trạng thái nút Cập nhật

`isDirty` phải so sánh dữ liệu normalized:

- FullName sau trim.
- Email sau trim/normalize.
- Role.
- Department.
- MSSV sau trim.

Nút Cập nhật disable khi:

- Không có thay đổi thực tế.
- Form không hợp lệ.
- Role DEPARTMENT chưa có department hợp lệ.
- Role STUDENT chưa có MSSV.
- Đang tải options.
- Đang submit.
- Không tìm thấy phòng IC hợp lệ.
- Có lỗi options không thể tiếp tục.

Không gửi request no-op để tránh:

- Revoke session không cần thiết.
- Gửi email không cần thiết.
- Tạo audit không có thay đổi.

---

# 4.11. API chi tiết tài khoản

Response chi tiết phải chứa:

```text
studentCode
departmentId
role
subRole
fullName
email
gender
phone
campus
status
```

Nếu `ViewAccountDetailsDto` chưa có `StudentCode`, bổ sung:

```csharp
public string? StudentCode { get; init; }
```

Response chi tiết là nguồn chính cho modal. Không phụ thuộc vào item danh sách để lấy MSSV.

Không trả:

```text
password
token
provider secret
refresh token
```

---

# 4.12. API lấy role assignment options

Nếu source hiện tại đã có endpoint/options tương đương, tái sử dụng và không tạo endpoint trùng.

Nếu chưa có, endpoint đề xuất:

```http
GET /api/accounts/role-assignment-options?targetUserId={userId}
```

Campus lấy từ authenticated caller, không lấy từ query/body do client cung cấp.

Response đề xuất:

```ts
interface RoleAssignmentOptions {
  campusId: string;
  campusName: string;
  icDepartment: {
    departmentId: string;
    name: string;
  } | null;
  generalDepartments: Array<{
    departmentId: string;
    name: string;
    hasHead: boolean;
    isCurrentTargetHead: boolean;
    selectable: boolean;
  }>;
}
```

Backend:

- Chỉ STAFF LEADER được gọi.
- Target cùng campus.
- Target không phải caller.
- `icDepartment`: IC + ACTIVE + cùng campus.
- `generalDepartments`: GENERAL + ACTIVE + cùng campus.
- `isCurrentTargetHead = head_user_id == targetUserId`.
- `selectable = !hasHead || isCurrentTargetHead`.
- Sắp xếp theo tên.

Không làm hỏng endpoint danh sách department đang dùng cho modal tạo tài khoản.

---

# 4.13. Request chỉnh sửa tài khoản

Mở rộng contract hiện tại:

```ts
interface UpdateAccountRequest {
  userId: string;
  fullName?: string | null;
  email?: string | null;
  newRoleCode: 'STAFF' | 'DEPARTMENT' | 'STUDENT';
  departmentId?: string | null;
  studentCode?: string | null;
}
```

Ví dụ DEPARTMENT:

```json
{
  "userId": "target-user-id",
  "fullName": "Nguyễn Văn An",
  "email": "an.nguyen@fpt.edu.vn",
  "newRoleCode": "DEPARTMENT",
  "departmentId": "department-id",
  "studentCode": null
}
```

Ví dụ STUDENT:

```json
{
  "userId": "target-user-id",
  "fullName": "Trần Văn C",
  "email": "tranvanc@fpt.edu.vn",
  "newRoleCode": "STUDENT",
  "departmentId": null,
  "studentCode": "SE123456"
}
```

Có thể giữ `fullName` và `email` optional để tương thích request cũ.

Backend không được coi việc client không gửi field là authorization.

Nếu client gửi giá trị FullName/Email khác hiện tại cho target không được phép sửa, backend phải từ chối. Không âm thầm bỏ qua.

---

# 4.14. Backend command và validator chỉnh sửa

Command bổ sung tối thiểu:

```csharp
public string? FullName { get; init; }
public string? Email { get; init; }
```

Giữ các field hiện có:

```text
UserId
NewRoleCode
DepartmentId
StudentCode
```

Validator cấu trúc:

## FullName

- Nếu field được gửi:
  - Not empty sau trim.
  - Tối đa 150 ký tự.

## Email

- Nếu field được gửi:
  - Not empty sau trim.
  - Email hợp lệ.
  - Tối đa 150 ký tự.

## StudentCode

- Nếu `NewRoleCode = STUDENT`:
  - Bắt buộc.
  - Tối đa 30 ký tự.

Database validation nằm trong Handler:

- Email duplicate.
- StudentCode duplicate.
- Department rule.
- Campus scope.
- Target policy.
- Head conflict.

---

# 4.15. Trình tự xử lý Handler chỉnh sửa

Thực hiện theo thứ tự:

1. Xác thực caller là STAFF LEADER ACTIVE.
2. Load target trực tiếp từ DB.
3. Load role/sub-role/campus/department/studentCode/status hiện tại.
4. Chặn target là caller.
5. Chặn target ngoài campus.
6. Chặn target LOCKED theo rule hiện tại.
7. Tính `canEditIdentityFields` từ role/sub-role gốc.
8. So sánh FullName/Email request với dữ liệu hiện tại.
9. Nếu target không có quyền identity nhưng request cố thay đổi:
   - Trả lỗi rõ ràng.
10. Normalize FullName/Email.
11. Validate Email duplicate, loại trừ target.
12. Validate role mới.
13. Resolve department theo role.
14. Validate StudentCode nếu role mới là STUDENT.
15. Validate StudentCode duplicate, loại trừ target.
16. Đồng bộ department head.
17. Update user.
18. Ghi audit trong cùng business transaction phù hợp convention.
19. Commit.
20. Revoke active sessions sau commit.
21. Gửi notification/email theo cơ chế hiện tại.
22. Trả response thành công.

Lỗi gửi email không được rollback database update.

---

# 4.16. Audit chỉnh sửa

Audit phải chứa old/new value của:

```text
FullName
Email
Role
SubRole
Campus
Department
StudentCode
```

Có thể giữ action:

```text
UPDATE_ACCOUNT_ROLE
```

nhưng metadata phải phản ánh cả thay đổi thông tin.

Nếu đổi tên action được mà không phá convention:

```text
UPDATE_ACCOUNT_INFORMATION_AND_ROLE
```

Không ghi:

```text
Password
AccessToken
RefreshToken
ProviderSecret
SessionToken
```

---

# 4.17. Reset modal chỉnh sửa

Bấm Hủy, nút X hoặc đóng overlay:

- Reset `accountEditForm`.
- Reset lỗi FullName.
- Reset lỗi Email.
- Reset lỗi Role/Department/MSSV.
- Reset options/loading tạm.
- Không mutate snapshot.
- Không gọi API.

Save thành công:

```text
Đóng modal
→ Refetch account details
→ Refetch account list/statistics nếu cần
→ Render dữ liệu mới từ server
```

Backend lỗi:

- Giữ modal mở.
- Giữ dữ liệu đã nhập.
- Map lỗi vào field phù hợp khi có thể.

---

# 5. PHẦN B — BỔ SUNG MSSV KHI TẠO ACCOUNT STUDENT

# 5.1. Phạm vi thay đổi tối thiểu

Flow tạo tài khoản hiện tại đã hoạt động cho:

```text
STAFF + STAFF
DEPARTMENT + LEADER
STUDENT + NULL
```

Không viết lại flow tạo account.

Chỉ bổ sung cho role STUDENT:

```text
Thêm input MSSV
Thêm state studentCode
Thêm validation
Thêm studentCode vào payload
Nối lỗi backend vào field MSSV
```

Agent phải kiểm tra backend hiện tại:

- Nếu backend đã nhận/lưu `StudentCode`, chỉ nối frontend.
- Nếu backend chưa hỗ trợ, bổ sung tối thiểu vào command/validator/handler.
- Không thay đổi authorization tạo account.

---

# 5.2. UI modal tạo tài khoản

Khi role đang chọn:

```text
STUDENT
```

hiển thị thêm:

```text
Mã số sinh viên (MSSV) *
```

Bố cục đề xuất:

```text
[Họ và tên]          [Email]

[Mã số sinh viên (MSSV)                 ]
```

MSSV có thể chiếm toàn bộ chiều rộng hàng dưới.

Khi role là:

```text
STAFF
DEPARTMENT
```

không render MSSV.

Ví dụ JSX:

```tsx
{createForm.roleCode === 'STUDENT' && (
  <div className="col-span-full">
    <label className="mb-1 block text-sm font-semibold text-slate-700">
      Mã số sinh viên (MSSV)
      <span className="ml-1 text-red-500">*</span>
    </label>

    <input
      type="text"
      value={createForm.studentCode}
      maxLength={30}
      placeholder="Ví dụ: SE123456"
      disabled={isSubmitting}
      onChange={(event) =>
        setCreateForm((previous) => ({
          ...previous,
          studentCode: event.target.value,
        }))
      }
      className="
        w-full rounded-xl border border-slate-300 bg-white px-4 py-3
        text-sm text-slate-800 placeholder:text-slate-400
        focus:border-[#004c91]
        focus:outline-none
        focus:ring-1
        focus:ring-[#004c91]
        disabled:bg-slate-100
        disabled:text-slate-500
        disabled:border-slate-200
        disabled:cursor-not-allowed
        disabled:opacity-100
      "
    />

    {createErrors.studentCode && (
      <p className="mt-1 text-sm text-red-600">
        {createErrors.studentCode}
      </p>
    )}
  </div>
)}
```

Code thực tế phải theo component/style convention hiện có, không bắt buộc copy nguyên mẫu.

---

# 5.3. State modal tạo

Bổ sung vào state hiện tại:

```ts
interface CreateAccountForm {
  roleCode: string;
  fullName: string;
  email: string;
  studentCode: string;

  // Giữ nguyên các field hiện có
  departmentId?: string;
  subRole?: string;
}
```

Giá trị khởi tạo:

```ts
studentCode: ''
```

Không dùng chung với:

```text
accountEditForm.studentCode
selectedAccount.studentCode
roleEditForm.studentCode
```

---

# 5.4. Hành vi đổi role trong modal tạo

Khi chọn STUDENT:

- Hiển thị MSSV.
- Không tự sinh MSSV.
- Không dùng email làm MSSV.
- Không lấy dữ liệu từ modal chỉnh sửa.

Khi chuyển khỏi STUDENT:

```ts
studentCode = ''
```

Đồng thời clear error:

```ts
studentCodeError = ''
```

Ví dụ:

```ts
const handleCreateRoleChange = (roleCode: string) => {
  setCreateForm((previous) => ({
    ...previous,
    roleCode,
    studentCode:
      roleCode === 'STUDENT'
        ? previous.studentCode
        : '',
  }));

  setCreateErrors((previous) => ({
    ...previous,
    studentCode: '',
  }));
};
```

Sau khi đã chuyển khỏi STUDENT rồi quay lại STUDENT, MSSV nên trống để không giữ dữ liệu ẩn ngoài ý muốn.

---

# 5.5. Validation MSSV khi tạo STUDENT

Chỉ validate khi:

```text
createForm.roleCode === 'STUDENT'
```

Rule:

- Bắt buộc nhập.
- Trim đầu/cuối.
- Không chấp nhận chỉ có khoảng trắng.
- Tối đa 30 ký tự.
- Không tự áp regex khi chưa có chuẩn chính thức.
- Không tự đổi hoa/thường.
- Không trùng user khác.

Thông báo:

```text
Vui lòng nhập mã số sinh viên.
Mã số sinh viên không được vượt quá 30 ký tự.
Mã số sinh viên này đã được sử dụng bởi tài khoản khác.
```

Submit handler phải validate lại. Không chỉ dựa vào button disabled.

---

# 5.6. Payload tạo tài khoản

Khi STUDENT:

```ts
const payload = {
  // Giữ nguyên toàn bộ field hiện tại
  roleCode: createForm.roleCode,
  fullName: createForm.fullName.trim(),
  email: createForm.email.trim(),
  studentCode: createForm.studentCode.trim(),
};
```

Ví dụ:

```json
{
  "roleCode": "STUDENT",
  "fullName": "Trần Văn C",
  "email": "tranvanc@fpt.edu.vn",
  "studentCode": "SE123456"
}
```

Khi role khác:

```ts
studentCode: null
```

Không gửi chuỗi rỗng nếu API convention hiện tại hỗ trợ `null`/omit:

```json
{
  "studentCode": ""
}
```

Không thay đổi logic hiện tại:

```text
STUDENT:
sub_role = NULL
department_id = NULL
primary_campus_id = campus caller
```

Frontend không được tự quyết định campus.

---

# 5.7. Backend tạo account — kiểm tra trước khi sửa

## Trường hợp backend đã hỗ trợ StudentCode

Nếu đã có:

```text
CreateAccountCommand.StudentCode
CreateAccountCommandValidator
CreateAccountCommandHandler lưu users.student_code
unique validation
```

thì không viết lại backend.

Chỉ:

- Nối frontend input.
- Gửi payload.
- Map lỗi duplicate.
- Bổ sung test.

## Trường hợp backend chưa hỗ trợ StudentCode

Bổ sung tối thiểu:

```csharp
public string? StudentCode { get; init; }
```

Validator:

```text
Nếu RoleCode = STUDENT:
- StudentCode bắt buộc.
- Trim.
- Tối đa 30 ký tự.

Nếu RoleCode != STUDENT:
- Không lưu StudentCode.
```

Handler:

1. Giữ nguyên authorization hiện tại.
2. Giữ nguyên role creation policy.
3. Nếu STUDENT:
   - Normalize StudentCode.
   - Kiểm tra duplicate.
   - Lưu `users.student_code`.
   - `sub_role = NULL`.
   - `department_id = NULL`.
   - Campus theo caller.
4. Nếu role khác:
   - `student_code = NULL`.

Không sửa logic STAFF/DEPARTMENT chỉ để thêm MSSV.

---

# 5.8. Reset modal tạo

Khi tạo thành công, bấm Hủy hoặc nút X:

```text
studentCode = ''
studentCodeError = ''
```

Đóng rồi mở lại modal:

- Không còn MSSV cũ.
- Không còn lỗi MSSV cũ.

Backend trả duplicate MSSV:

- Giữ modal mở.
- Giữ Họ tên.
- Giữ Email.
- Giữ MSSV.
- Hiển thị lỗi ngay dưới input MSSV.

---

# 6. DATABASE

Schema hiện tại đã có hoặc phải được kiểm tra có:

```sql
full_name VARCHAR(150) NOT NULL,
email VARCHAR(150) NOT NULL,
student_code VARCHAR(30) NULL,

UNIQUE KEY uq_users_email (email),
UNIQUE KEY uq_users_student_code (student_code)
```

Nếu schema thực tế đúng như trên:

- Không thêm bảng.
- Không thêm cột.
- Không thêm index.
- Không tạo migration.
- Không sửa SQL.

Application vẫn phải validate sớm để không trả raw MySQL duplicate-key error.

Race condition duplicate Email/MSSV phải được map về business error thân thiện.

---

# 7. THÔNG BÁO LỖI TỔNG HỢP

## 7.1. Chỉnh sửa account

| Trường hợp | Thông báo |
|---|---|
| Họ tên rỗng | `Vui lòng nhập họ và tên.` |
| Họ tên quá dài | `Họ và tên không được vượt quá 150 ký tự.` |
| Email rỗng | `Vui lòng nhập địa chỉ email.` |
| Email sai format | `Địa chỉ email không hợp lệ.` |
| Email quá dài | `Email không được vượt quá 150 ký tự.` |
| Email trùng | `Email này đã được sử dụng bởi tài khoản khác.` |
| Không có quyền sửa identity | `Bạn không có quyền chỉnh sửa họ tên hoặc email của tài khoản này.` |
| DEPARTMENT chưa chọn phòng | `Vui lòng chọn phòng ban cho vai trò Trưởng phòng ban.` |
| Không có IC ACTIVE | `Không tìm thấy Phòng Hợp tác Quốc tế đang hoạt động cho cơ sở của bạn.` |
| MSSV rỗng | `Vui lòng nhập mã số sinh viên.` |
| MSSV quá dài | `Mã số sinh viên không được vượt quá 30 ký tự.` |
| MSSV trùng | `Mã số sinh viên này đã được sử dụng bởi tài khoản khác.` |
| Load options lỗi | `Không thể tải danh sách phòng ban. Vui lòng thử lại.` |

## 7.2. Tạo STUDENT

| Trường hợp | Thông báo |
|---|---|
| Chưa nhập MSSV | `Vui lòng nhập mã số sinh viên.` |
| MSSV quá dài | `Mã số sinh viên không được vượt quá 30 ký tự.` |
| MSSV trùng | `Mã số sinh viên này đã được sử dụng bởi tài khoản khác.` |

Ưu tiên hiển thị field error ngay dưới input tương ứng.

---

# 8. FILE/LAYER DỰ KIẾN LIÊN QUAN

Agent phải xác nhận đường dẫn thực tế trước khi sửa.

## 8.1. Frontend

Dự kiến:

```text
frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx
frontend/pems-react/src/features/account-management/types/accountManagement.types.ts
frontend/pems-react/src/features/account-management/api/accountManagementApi.ts
frontend/pems-react/src/shared/api/endpoints.ts
```

Thay đổi:

- Edit state thêm FullName/Email.
- `canEditIdentityFields`.
- Style field disabled màu xám.
- Edit payload thêm FullName/Email.
- Create state thêm StudentCode.
- Render MSSV cho create STUDENT.
- Validation/reset StudentCode.
- Error mapping Email/MSSV.
- Tests.

## 8.2. Backend chỉnh sửa account

Dự kiến:

```text
backend/PEMS.Api/Controllers/AccountsController.cs

backend/PEMS.Application/Accounts/Commands/UpdateAccountRole/
├── UpdateAccountRoleCommand.cs
├── UpdateAccountRoleCommandValidator.cs
├── UpdateAccountRoleCommandHandler.cs
└── UpdateAccountRoleResponse.cs

backend/PEMS.Application/Accounts/Queries/ViewAccountDetails/
├── ViewAccountDetailsDto.cs
├── ViewAccountDetailsQuery.cs
└── ViewAccountDetailsQueryHandler.cs
```

Có thể cần:

```text
GetRoleAssignmentOptions query/DTO/handler
AccountProvisioningRules
AccountErrorCodes
```

## 8.3. Backend tạo account

Chỉ sửa nếu StudentCode chưa được hỗ trợ:

```text
backend/PEMS.Application/Accounts/Commands/CreateAccount/
├── CreateAccountCommand.cs
├── CreateAccountCommandValidator.cs
├── CreateAccountCommandHandler.cs
└── CreateAccountResponse.cs
```

Không refactor ngoài phạm vi.

---

# 9. TEST FRONTEND BẮT BUỘC

## 9.1. Modal chỉnh sửa

1. `STAFF + STAFF`: FullName và Email editable.
2. `DEPARTMENT + LEADER`: FullName và Email editable.
3. `STUDENT`: FullName và Email editable.
4. `DEPARTMENT + STAFF`: FullName và Email disable.
5. `STAFF + LEADER`: FullName và Email disable.
6. Đổi dropdown role không đổi quyền editable identity.
7. Giới tính null hiển thị `-`.
8. Số điện thoại null hiển thị `-`.
9. Giới tính, điện thoại, campus luôn disable.
10. Field disable có nền xám và chữ rõ.
11. FullName rỗng không submit.
12. FullName quá dài không submit.
13. Email rỗng/sai format/quá dài không submit.
14. Chỉ thay FullName làm `isDirty = true`.
15. Chỉ thay Email làm `isDirty = true`.
16. Nhập lại giá trị ban đầu làm `isDirty = false`.
17. STAFF render IC department disable.
18. DEPARTMENT render GENERAL ACTIVE dropdown.
19. Department có head khác disable.
20. Department do target làm head vẫn selectable.
21. DEPARTMENT chưa chọn phòng không submit.
22. STUDENT ẩn Chức vụ/Phòng ban.
23. STUDENT hiển thị MSSV.
24. Target STUDENT giữ MSSV lúc mở modal.
25. Chuyển từ role khác sang STUDENT có MSSV trống.
26. Rời STUDENT clear MSSV.
27. Bấm Hủy không mutate snapshot.
28. Backend duplicate Email/MSSV giữ modal mở.
29. Save thành công đóng modal và refetch.
30. Card snapshot không thay đổi trong lúc nhập.

## 9.2. Modal tạo account

1. STAFF vẫn tạo như trước.
2. DEPARTMENT LEADER vẫn tạo như trước.
3. STUDENT vẫn tạo như trước.
4. Chọn STUDENT hiển thị MSSV.
5. MSSV có dấu `*`.
6. Chọn STAFF ẩn MSSV.
7. Chọn DEPARTMENT ẩn MSSV.
8. Chuyển khỏi STUDENT clear MSSV.
9. Chọn lại STUDENT không còn MSSV cũ.
10. STUDENT thiếu MSSV không submit.
11. MSSV chỉ có khoảng trắng không submit.
12. MSSV quá 30 ký tự không submit.
13. MSSV được trim trước khi gửi.
14. Payload STUDENT có `studentCode`.
15. Payload role khác không gửi MSSV hoặc gửi null.
16. Duplicate MSSV hiển thị dưới input.
17. Duplicate MSSV không đóng modal.
18. Đóng/mở modal reset MSSV.
19. Modal create không dùng chung state với modal edit.
20. Tạo thành công reset form và refetch danh sách.

---

# 10. TEST BACKEND BẮT BUỘC

## 10.1. Chỉnh sửa account

1. STAFF LEADER sửa FullName của `STAFF + STAFF`.
2. STAFF LEADER sửa Email của `DEPARTMENT + LEADER`.
3. STAFF LEADER sửa FullName và Email của STUDENT.
4. Từ chối identity update cho `DEPARTMENT + STAFF`.
5. Từ chối identity update cho `STAFF + LEADER`.
6. Từ chối target ngoài campus.
7. Từ chối target là caller.
8. Từ chối target LOCKED theo rule hiện tại.
9. Từ chối FullName rỗng/quá dài.
10. Từ chối Email rỗng/sai format/quá dài.
11. Từ chối Email trùng user khác.
12. Email hiện tại của target không bị coi là duplicate.
13. Quyền identity dựa trên role gốc, không dựa trên NewRoleCode.
14. STAFF tự resolve IC ACTIVE.
15. STAFF clear StudentCode.
16. DEPARTMENT yêu cầu DepartmentId.
17. DEPARTMENT từ chối phòng inactive/sai campus/sai type.
18. DEPARTMENT từ chối phòng có head khác.
19. DEPARTMENT chấp nhận phòng target đang làm head.
20. Rời DEPARTMENT clear head cũ khi phù hợp.
21. STUDENT bắt buộc StudentCode.
22. STUDENT từ chối StudentCode trùng.
23. StudentCode hiện tại của target không bị coi là duplicate chính nó.
24. Chuyển khỏi STUDENT clear StudentCode.
25. Audit có old/new FullName, Email, Role, Department, StudentCode.
26. Session chỉ revoke sau DB commit.
27. Request cũ không gửi FullName/Email vẫn tương thích.
28. Race head conflict trả lỗi thân thiện.
29. Race duplicate Email/MSSV trả lỗi thân thiện.
30. Không lộ raw database exception.

## 10.2. Tạo STUDENT

1. STAFF LEADER tạo STUDENT có MSSV thành công.
2. `role_code = STUDENT`.
3. `sub_role = NULL`.
4. `department_id = NULL`.
5. Campus theo caller.
6. `users.student_code` lưu đúng.
7. Thiếu MSSV bị từ chối.
8. MSSV chỉ có khoảng trắng bị từ chối.
9. MSSV quá 30 ký tự bị từ chối.
10. MSSV trùng bị từ chối.
11. Role khác không lưu StudentCode.
12. Không thay đổi policy tạo account hiện tại.
13. Request lỗi không tạo user dở dang.
14. Race duplicate MSSV trả lỗi thân thiện.

---

# 11. DEFINITION OF DONE

Chỉ được báo hoàn thành khi:

- STAFF LEADER sửa được FullName/Email của đúng:
  - `STAFF + STAFF`
  - `DEPARTMENT + LEADER`
  - `STUDENT`
- Target khác vẫn bị khóa cả frontend và backend.
- Quyền sửa identity dựa trên role/sub-role gốc.
- Gender/Phone/Campus vẫn disable.
- Field disable có màu xám rõ.
- Flow đổi role STAFF/DEPARTMENT/STUDENT hiện tại không regression.
- Department options đúng scope.
- Đồng bộ `head_user_id` đúng.
- Email và StudentCode duplicate được xử lý thân thiện.
- Create account STAFF/DEPARTMENT vẫn hoạt động như trước.
- Create STUDENT hiển thị MSSV bắt buộc.
- MSSV được gửi và lưu vào `users.student_code`.
- Role khác không giữ/gửi nhầm MSSV.
- Không thay đổi quyền tạo account.
- Không sửa schema khi không cần.
- Frontend build/typecheck thành công.
- Backend build thành công.
- Test cũ và test mới pass.
- Không commit file build/cache.
- Diff không chứa refactor hoặc thay đổi ngoài phạm vi.

---

# 12. NGOÀI PHẠM VI

Không thực hiện trong task này:

- Sửa Giới tính.
- Sửa Số điện thoại.
- Sửa Campus.
- Sửa mật khẩu.
- Sửa thông tin SSO/provider.
- Tạo hoặc xóa department.
- Thay thế một Department Leader khác qua dropdown nếu phòng đã có head khác.
- Thay đổi quyền của ADMIN/HO.
- Thêm role tạo account mới.
- Tạo migration/schema mới khi cột/index đã tồn tại.
- Refactor toàn bộ Account Management ngoài nhu cầu trực tiếp.

---

# 13. THỨ TỰ TRIỂN KHAI KHUYẾN NGHỊ

1. Đọc code hiện tại và xác nhận các phần đã hoàn thành.
2. Kiểm tra `CreateAccountCommand` đã có StudentCode hay chưa.
3. Kiểm tra `UpdateAccountRoleCommand` đã có StudentCode hay chưa.
4. Bổ sung StudentCode vào detail DTO nếu thiếu.
5. Mở rộng edit contract với FullName/Email.
6. Viết backend validation/authorization identity.
7. Giữ và kiểm thử role/department/head/MSSV logic.
8. Bổ sung frontend edit state và field editable condition.
9. Bổ sung disabled gray style.
10. Bổ sung MSSV trong modal tạo STUDENT.
11. Nối payload/error mapping.
12. Viết test backend.
13. Viết test frontend.
14. Chạy backend build/test.
15. Chạy frontend build/typecheck/test.
16. Kiểm tra diff và regression.
17. Chỉ báo hoàn thành khi Definition of Done đạt đủ.

---

# 14. TÓM TẮT NGẮN CHO AI AGENT

```text
1. Không viết lại chức năng tạo account đã có.
2. STAFF LEADER hiện đã tạo STAFF/STAFF, DEPARTMENT/LEADER, STUDENT.
3. Chỉ thêm ô MSSV bắt buộc khi tạo STUDENT.
4. Khi chỉnh sửa account, cho sửa FullName và Email nếu target gốc là:
   - STAFF + STAFF
   - DEPARTMENT + LEADER
   - STUDENT
5. Quyền edit identity không phụ thuộc role mới đang chọn.
6. Gender, Phone, Campus vẫn disable.
7. Tất cả field disable có nền xám.
8. Giữ nguyên flow đổi role, department head và StudentCode.
9. Backend luôn là lớp kiểm tra cuối.
10. Không sửa database schema nếu cột/index đã tồn tại.
```
