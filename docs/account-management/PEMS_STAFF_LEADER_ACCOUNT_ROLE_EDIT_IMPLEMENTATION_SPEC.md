# PEMS — Đặc tả triển khai chỉnh sửa vai trò tài khoản cho STAFF LEADER

## 0. Thông tin tài liệu

- Dự án: `quangthoai04/PEMS`
- Nhánh gốc: `Dev`
- Nhánh/PR đang chứa bản sửa giai đoạn đầu: `fix/account-role-edit-preserve-details`
- Commit hiện tại của bản sửa giai đoạn đầu: `ae8f4114c9a6e42cbdebe8da4117b9e238550412`
- Pull request: `#1`
- Phạm vi người thao tác: tài khoản có `role = STAFF` và `sub_role = LEADER`
- Màn hình: Quản lý tài khoản → Xem chi tiết tài khoản → Chỉnh sửa vai trò
- Database tham chiếu: `pems_full_v10_TTS_Gallery_FULL_UPDATED_NOTIFICATIONS_FIXED.sql`

Tài liệu này là đặc tả triển khai end-to-end. Agent thực hiện phải sửa đồng bộ frontend, backend, validation, API contract và test. Không chỉ sửa phần hiển thị.

## 1. Mục tiêu

Khi STAFF LEADER mở chi tiết một tài khoản và bấm **Chỉnh sửa vai trò**:

1. Dữ liệu nhận dạng ban đầu của tài khoản phải được giữ nguyên.
2. Các trường không thuộc phạm vi đổi vai trò phải bị disable.
3. Trường có giá trị `null` hoặc rỗng phải hiển thị `-`, không được tự chọn giá trị mặc định.
4. Form phải thay đổi động theo vai trò đang chọn: `STAFF`, `DEPARTMENT` hoặc `STUDENT`.
5. Dữ liệu gửi lên backend phải đúng với cấu trúc nghiệp vụ của vai trò mới.
6. Backend tiếp tục là nguồn xác thực cuối cùng cho campus, phòng ban, trưởng phòng và MSSV.

## 2. Vấn đề hiện tại

### 2.1. Giới tính rỗng tự hiển thị thành Nam

Frontend hiện truyền `value=""` vào một `<select>` giới tính nhưng dropdown không có option rỗng. Trình duyệt hiển thị option đầu tiên là `Nam` dù dữ liệu thật là `null`.

### 2.2. Bố cục vẫn dựa vào vai trò cũ

Phần form chi tiết đang render các trường tổ chức bằng `selectedAccount.role`. Vì vậy đổi giá trị dropdown vai trò không làm bố cục chuyển đúng sang STAFF, DEPARTMENT hoặc STUDENT.

### 2.3. Form đổi vai trò chưa có state phụ thuộc theo vai trò

State chỉnh sửa hiện chỉ chứa vai trò. Chưa có state riêng cho:

- Phòng ban được chọn khi chuyển sang DEPARTMENT.
- MSSV khi chuyển sang STUDENT.
- Dữ liệu phòng IC mặc định khi chuyển sang STAFF.

State `selectedDept` hiện còn được dùng cho modal tạo tài khoản; không được tái sử dụng cho modal chỉnh sửa vai trò.

### 2.4. API đổi vai trò chưa hỗ trợ MSSV

`UpdateAccountRoleCommand` và request frontend hiện chưa có `studentCode`. Handler cũng chưa cập nhật hoặc xóa `users.student_code` khi thay đổi vai trò.

### 2.5. API chi tiết chưa trả MSSV

`ViewAccountDetailsDto` chưa có `StudentCode`. Dữ liệu MSSV đang phụ thuộc vào item của API danh sách, không phải response chi tiết.

## 3. Hành vi UI bắt buộc

### 3.1. Các trường chung

Trong chế độ chỉnh sửa vai trò, các trường sau luôn giữ dữ liệu từ snapshot chi tiết ban đầu và bị disable:

- Họ và tên.
- Email.
- Giới tính.
- Số điện thoại.
- Cơ sở trực thuộc.

Quy tắc hiển thị:

- `null`, `undefined` hoặc chuỗi rỗng hiển thị `-`.
- Không dùng dropdown cho giới tính trong chế độ chỉnh sửa vì giới tính không được phép sửa trong flow này.
- Không được dùng giá trị fallback như `Nam` cho dữ liệu rỗng.
- Thẻ thông tin bên trái modal tiếp tục hiển thị snapshot ban đầu trong lúc chỉnh sửa; chỉ cập nhật sau khi lưu thành công và refetch.

### 3.2. Ma trận hành vi theo vai trò đang chọn

| Vai trò đang chọn | Chức vụ | Phòng ban | MSSV | Trường được phép sửa |
| --- | --- | --- | --- | --- |
| `STAFF` | `Nhân viên`, disable | Phòng IC của campus, mặc định là `Phòng Hợp tác Quốc tế`, disable | Không hiển thị | Chỉ Vai trò |
| `DEPARTMENT` | `Trưởng phòng`, disable | Dropdown phòng ban GENERAL đang hoạt động | Không hiển thị | Vai trò và Phòng ban |
| `STUDENT` | Không render | Không render | Input MSSV | Vai trò và MSSV |

Việc render phải dựa trên `roleEditForm.roleCode`, không dựa trên `selectedAccount.role`.

### 3.3. Khi chọn STAFF

1. Hiển thị chức vụ `Nhân viên` và disable.
2. Hiển thị phòng ban IC đang hoạt động của campus STAFF LEADER và disable.
3. Tên phòng ban phải lấy từ backend, không hard-code ở frontend.
4. Với database hiện tại tên phòng IC là `Phòng Hợp tác Quốc tế`.
5. Ẩn MSSV.
6. Xóa `departmentId` do người dùng từng chọn trong state DEPARTMENT.
7. Xóa `studentCode` tạm trong state STUDENT.
8. Khi submit, frontend không tự quyết định department; backend tự resolve phòng IC của campus người thao tác.
9. Nếu campus không có phòng IC trạng thái ACTIVE, hiển thị lỗi và disable nút Cập nhật.

### 3.4. Khi chọn DEPARTMENT

1. Hiển thị chức vụ `Trưởng phòng` và disable.
2. Hiển thị dropdown Phòng ban và cho phép chọn.
3. Chỉ hiển thị phòng ban thỏa tất cả điều kiện:
   - Thuộc campus của STAFF LEADER đang đăng nhập.
   - `department_type = GENERAL`.
   - `status = ACTIVE`.
4. Placeholder: `-- Chọn phòng ban --`.
5. Option chưa có trưởng phòng hiển thị tên bình thường và cho phép chọn.
6. Option đã có trưởng phòng khác hiển thị dạng `Tên phòng ban — Đã có trưởng phòng` và bị disable.
7. Nếu phòng ban hiện tại đang do chính tài khoản được chỉnh sửa làm trưởng phòng, option đó được giữ selectable/preselected và có thể ghi chú `Phòng ban hiện tại`.
8. Không được chỉ dựa vào `hasHead`; API options phải phân biệt trưởng phòng hiện tại có phải chính target account hay không.
9. Nếu không có phòng phù hợp, hiển thị thông báo và không cho submit vai trò DEPARTMENT.
10. Nút Cập nhật bị disable cho tới khi có `departmentId` hợp lệ.

### 3.5. Khi chọn STUDENT

1. Không render trường Chức vụ.
2. Không render trường Phòng ban.
3. Giữ trường Cơ sở trực thuộc và disable.
4. Render input `Mã số sinh viên (MSSV)`.
5. Khi chuyển từ role khác sang STUDENT, input MSSV khởi tạo trống.
6. Nếu tài khoản ban đầu đã là STUDENT và người dùng vừa mở chế độ chỉnh sửa, giữ MSSV ban đầu.
7. Nếu người dùng rời STUDENT rồi chọn lại STUDENT trong cùng phiên chỉnh sửa, dùng chuỗi trống, trừ khi state chưa bị reset do không thực sự đổi role.
8. MSSV là bắt buộc trước khi submit STUDENT.
9. MSSV được trim đầu/cuối, tối đa 30 ký tự và không được trùng tài khoản khác.
10. Không tự thêm regex hoặc tự thay đổi hoa/thường nếu dự án chưa có quy ước MSSV chính thức.

### 3.6. Save, cancel và no-op

- Bấm Hủy: đóng chế độ chỉnh sửa, reset `roleEditForm`, lỗi, loading và options tạm; không thay đổi `selectedAccount`.
- Đóng modal/overlay: thực hiện cùng cơ chế reset như Hủy.
- Bấm Cập nhật: validate frontend trước, gọi API, hiển thị lỗi backend nếu có.
- Thành công: đóng modal, refetch danh sách/thống kê; lần mở tiếp theo phải lấy dữ liệu mới từ server.
- Không gửi request nếu không có thay đổi thực sự. Nút Cập nhật nên disable khi `isDirty = false` để tránh revoke session và gửi email không cần thiết.

## 4. Thiết kế state frontend

Không mutate `selectedAccount` trong lúc chỉnh sửa. Dùng một state riêng có kiểu rõ ràng:

```ts
interface RoleEditForm {
  roleCode: 'STAFF' | 'DEPARTMENT' | 'STUDENT';
  departmentId: string;
  studentCode: string;
}
```

State liên quan:

```ts
const [roleEditForm, setRoleEditForm] = useState<RoleEditForm | null>(null);
const [roleOptions, setRoleOptions] = useState<RoleAssignmentOptions | null>(null);
const [roleOptionsLoading, setRoleOptionsLoading] = useState(false);
const [roleOptionsError, setRoleOptionsError] = useState<string | null>(null);
const [roleSaving, setRoleSaving] = useState(false);
const [roleError, setRoleError] = useState<string | null>(null);
```

Khởi tạo khi bấm Chỉnh sửa vai trò:

```ts
{
  roleCode: selectedAccount.role,
  departmentId: selectedAccount.role === 'DEPARTMENT'
    ? selectedAccount.departmentId ?? ''
    : '',
  studentCode: selectedAccount.role === 'STUDENT'
    ? selectedAccount.studentCode ?? ''
    : ''
}
```

Không dùng `selectedDept` của modal tạo tài khoản. Nếu cần, đổi tên state cũ thành `createDepartmentId` để tránh nhầm phạm vi.

## 5. API lấy lựa chọn gán vai trò

### 5.1. Endpoint đề xuất

Thêm endpoint dành riêng cho flow chỉnh sửa vai trò:

```http
GET /api/accounts/role-assignment-options?targetUserId={userId}
```

Campus phải được lấy từ authenticated STAFF LEADER, không nhận campus từ client.

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

Quy tắc backend của endpoint:

1. Chỉ STAFF/LEADER được sử dụng flow này.
2. Xác minh target thuộc campus của caller và target không phải chính caller.
3. `icDepartment` là phòng `IC`, `ACTIVE`, thuộc campus caller.
4. `generalDepartments` chỉ gồm phòng `GENERAL`, `ACTIVE`, thuộc campus caller.
5. `hasHead = head_user_id IS NOT NULL`.
6. `isCurrentTargetHead = head_user_id == targetUserId`.
7. `selectable = !hasHead || isCurrentTargetHead`.
8. Sắp xếp phòng ban theo tên.

Endpoint hiện có `GET /api/accounts/campus-departments` đã xử lý phần lớn danh sách GENERAL và `hasHead`. Có thể tái sử dụng logic/query chung, nhưng không được làm hỏng modal tạo tài khoản hiện tại.

### 5.2. Trạng thái tải options

- Tải options khi mở chế độ chỉnh sửa hoặc lần đầu chọn DEPARTMENT/STAFF.
- Trong lúc tải: disable dropdown và nút Cập nhật, hiển thị `Đang tải...`.
- Nếu API lỗi: hiển thị lỗi trong modal, không fallback sang danh sách hard-code.

## 6. Thay đổi API chi tiết tài khoản

Bổ sung `StudentCode` vào:

- `ViewAccountDetailsDto`.
- Projection trong `ViewAccountDetailsQueryHandler`.
- `AccountDetails` ở frontend.
- Mapping từ response chi tiết sang `selectedAccount`.

Response chi tiết phải là nguồn dữ liệu chính cho MSSV, không phụ thuộc item danh sách.

Không thay đổi quy tắc không trả dữ liệu nhạy cảm như password, token hoặc provider secret.

## 7. Thay đổi API cập nhật vai trò

### 7.1. Request contract

Bổ sung `studentCode` vào cả frontend type và backend command:

```ts
interface UpdateAccountRoleRequest {
  userId: string;
  newRoleCode: 'STAFF' | 'DEPARTMENT' | 'STUDENT';
  departmentId?: string | null;
  studentCode?: string | null;
}
```

Các field `subRole` và `primaryCampusId` không được tin cậy trong flow STAFF LEADER. Backend phải tự derive theo role và campus caller như hiện tại.

### 7.2. Payload theo role

STAFF:

```json
{
  "userId": "target-id",
  "newRoleCode": "STAFF",
  "departmentId": null,
  "studentCode": null
}
```

DEPARTMENT:

```json
{
  "userId": "target-id",
  "newRoleCode": "DEPARTMENT",
  "departmentId": "selected-general-department-id",
  "studentCode": null
}
```

STUDENT:

```json
{
  "userId": "target-id",
  "newRoleCode": "STUDENT",
  "departmentId": null,
  "studentCode": "entered-student-code"
}
```

### 7.3. Quy tắc backend bắt buộc

#### STAFF

- Role code: `STAFF`.
- `sub_role = STAFF`.
- Campus = campus của STAFF LEADER caller.
- Department = phòng `IC`, `ACTIVE` của campus caller.
- `student_code = NULL`.
- Không tin `departmentId` do client gửi.

#### DEPARTMENT

- Role code: `DEPARTMENT`.
- `sub_role = LEADER`.
- Campus = campus của STAFF LEADER caller.
- Bắt buộc có `departmentId`.
- Department phải tồn tại, `ACTIVE`, loại `GENERAL`, cùng campus caller.
- Department không được có trưởng phòng khác.
- `student_code = NULL`.
- Đồng bộ `departments.head_user_id` với target user.

#### STUDENT

- Role code: `STUDENT`.
- `sub_role = NULL`.
- `department_id = NULL`.
- Campus = campus của STAFF LEADER caller.
- Bắt buộc có `studentCode` sau khi trim.
- `studentCode.Length <= 30`.
- MSSV không được trùng user khác; khi kiểm tra phải loại trừ chính target user.
- Lưu `users.student_code`.

#### Chuyển khỏi STUDENT

- Khi role mới không phải STUDENT, đặt `users.student_code = NULL` để không giữ MSSV ẩn trên tài khoản STAFF/DEPARTMENT.

### 7.4. Đồng bộ trưởng phòng

Giữ và hoàn thiện logic hiện có trong `UpdateAccountRoleCommandHandler`:

1. Nếu target rời phòng cũ và `oldDepartment.head_user_id == targetUserId`, xóa head của phòng cũ.
2. Nếu target được gán DEPARTMENT, kiểm tra phòng mới chưa có head khác.
3. Gán `newDepartment.head_user_id = targetUserId`.
4. Cập nhật user role/sub-role/campus/department/studentCode trong cùng một lần SaveChanges/transaction.
5. Backend phải kiểm tra lại ngay lúc submit để xử lý race condition sau khi frontend đã tải options.

### 7.5. Audit và notification

- Audit log `UPDATE_ACCOUNT_ROLE` phải lưu cả giá trị cũ/mới của:
  - Role.
  - Sub-role.
  - Campus.
  - Department.
  - Student code.
- Giữ cơ chế revoke toàn bộ active sessions sau khi cập nhật thành công.
- Giữ email thông báo đổi vai trò.
- Nếu role mới là STUDENT, email có thể hiển thị MSSV mới; không được làm thất bại transaction chỉ vì gửi email lỗi.

## 8. Validation và thông báo lỗi

### 8.1. Frontend

- DEPARTMENT chưa chọn phòng: `Vui lòng chọn phòng ban cho vai trò Trưởng phòng ban.`
- STUDENT chưa nhập MSSV: `Vui lòng nhập mã số sinh viên.`
- Không có phòng IC ACTIVE: `Không tìm thấy Phòng Hợp tác Quốc tế đang hoạt động cho cơ sở của bạn.`
- Options lỗi: `Không thể tải danh sách phòng ban. Vui lòng thử lại.`
- MSSV quá 30 ký tự: thông báo rõ giới hạn.

### 8.2. Backend

Backend không phụ thuộc validation frontend. Cần trả lỗi nghiệp vụ rõ ràng cho các trường hợp:

- Target ngoài campus.
- Target là chính caller.
- Target bị LOCKED.
- Role không thuộc STAFF/DEPARTMENT/STUDENT.
- Department không tồn tại, inactive, sai campus hoặc sai type.
- Department đã có trưởng phòng khác.
- Không có phòng IC ACTIVE.
- MSSV trống, quá dài hoặc đã tồn tại.

Ưu tiên dùng exception/response pattern hiện có của dự án để `getAccountErrorMessage` hiển thị được message backend.

## 9. Database

Database hiện có:

```sql
student_code VARCHAR(30) NULL,
UNIQUE KEY uq_users_student_code (student_code)
```

Vì vậy:

- Không cần migration thêm cột MSSV.
- Không cần thêm unique index.
- Application vẫn phải validate sớm để trả lỗi thân thiện thay vì để lộ raw MySQL duplicate-key error.
- Nhiều giá trị `NULL` được phép; chỉ role STUDENT trong flow này bắt buộc có MSSV ở application layer.

Các giá trị dữ liệu tổ chức liên quan:

- STAFF thường: `role = STAFF`, `sub_role = STAFF`, department type `IC`.
- Trưởng phòng ban: `role = DEPARTMENT`, `sub_role = LEADER`, department type `GENERAL`.
- Sinh viên: `role = STUDENT`, `sub_role = NULL`, `department_id = NULL`.
- Trạng thái phòng ban hợp lệ cho dropdown: `ACTIVE`.

## 10. Các file dự kiến sửa/thêm

### Frontend

- `frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx`
- `frontend/pems-react/src/features/account-management/types/accountManagement.types.ts`
- `frontend/pems-react/src/features/account-management/api/accountManagementApi.ts`
- `frontend/pems-react/src/shared/api/endpoints.ts`
- Test component/hook/API tương ứng theo cấu trúc test hiện có.

### Backend

- `backend/PEMS.Api/Controllers/AccountsController.cs`
- `backend/PEMS.Application/Accounts/Commands/UpdateAccountRole/UpdateAccountRoleCommand.cs`
- `backend/PEMS.Application/Accounts/Commands/UpdateAccountRole/UpdateAccountRoleCommandValidator.cs`
- `backend/PEMS.Application/Accounts/Commands/UpdateAccountRole/UpdateAccountRoleCommandHandler.cs`
- `backend/PEMS.Application/Accounts/Queries/ViewAccountDetails/ViewAccountDetailsDto.cs`
- `backend/PEMS.Application/Accounts/Queries/ViewAccountDetails/ViewAccountDetailsQueryHandler.cs`
- Thêm query/DTO/handler cho `GetRoleAssignmentOptions`.
- Có thể trích xuất/tái sử dụng logic từ `GetCampusDepartmentsQueryHandler` và `AccountProvisioningRules`.
- Test application/API theo cấu trúc test hiện có.

Không sửa SQL schema nếu không phát hiện chênh lệch giữa database thực tế và file SQL nguồn.

## 11. Test cases bắt buộc

### 11.1. Frontend

1. Gender `null` hiển thị `-` trong detail và edit mode.
2. Phone `null` hiển thị `-` trong edit mode.
3. Họ tên/email/campus không thay đổi và bị disable.
4. Chỉ role được chỉnh sửa khi chọn STAFF.
5. STAFF hiển thị đúng phòng IC từ API và phòng ban bị disable.
6. DEPARTMENT hiển thị dropdown GENERAL ACTIVE cùng campus.
7. Department có trưởng phòng khác bị disable và có nhãn trạng thái.
8. Department do chính target làm trưởng phòng vẫn preselected/selectable.
9. DEPARTMENT không có departmentId thì không submit.
10. STUDENT ẩn Chức vụ và Phòng ban.
11. STUDENT hiện input MSSV trống khi chuyển từ role khác.
12. Tài khoản ban đầu là STUDENT giữ MSSV ban đầu khi vừa mở edit.
13. Chuyển role qua lại reset field phụ thuộc, không gửi department/MSSV cũ sai ngữ cảnh.
14. Bấm Hủy không mutate snapshot.
15. Nút Cập nhật disable khi không có thay đổi hoặc khi form/options không hợp lệ.
16. Thành công đóng modal và refetch.

### 11.2. Backend

1. STAFF tự resolve IC ACTIVE cùng campus, set sub-role STAFF và clear StudentCode.
2. STAFF thất bại nếu campus không có IC ACTIVE.
3. DEPARTMENT yêu cầu departmentId.
4. DEPARTMENT từ chối department inactive, khác campus hoặc type không phải GENERAL.
5. DEPARTMENT từ chối phòng có head khác.
6. DEPARTMENT chấp nhận phòng đang do chính target làm head.
7. Rời DEPARTMENT xóa `head_user_id` cũ nếu head là target.
8. STUDENT set campus caller, clear sub-role/department và lưu StudentCode.
9. STUDENT từ chối MSSV rỗng, quá 30 ký tự hoặc trùng.
10. Cập nhật MSSV cho chính target STUDENT không bị coi là duplicate của chính nó.
11. Chuyển khỏi STUDENT clear StudentCode.
12. Staff Leader không thể đổi role của chính mình.
13. Staff Leader không thể sửa target ngoài campus hoặc target LOCKED.
14. Audit log chứa StudentCode cũ/mới.
15. Session chỉ bị revoke sau khi cập nhật DB thành công.

### 11.3. Kiểm thử tích hợp/race

- Tải options khi phòng chưa có head, sau đó một request khác gán head trước khi submit: request sau phải bị backend từ chối bằng Conflict/BusinessRule rõ ràng.
- MSSV trở thành trùng giữa lúc frontend validate và backend SaveChanges: trả lỗi thân thiện, không raw database error.

## 12. Definition of Done

Chỉ coi là hoàn thành khi:

- UI đúng ma trận vai trò trong mục 3.
- Gender rỗng không còn tự hiển thị Nam.
- Không có dữ liệu từ modal tạo tài khoản rò sang modal đổi vai trò.
- Options phòng ban hoàn toàn từ backend và bị scope theo campus caller.
- Backend lưu đúng department, head_user_id và student_code.
- Không cần thay đổi schema database hiện tại.
- Frontend build/typecheck thành công.
- Backend build thành công.
- Các test liên quan pass.
- Không làm thay đổi hành vi tạo tài khoản hoặc quản lý tài khoản của ADMIN/HO ngoài phạm vi cần thiết.
- PR hiện tại được cập nhật bằng commit có phạm vi rõ ràng, không commit file build/cache hoặc thay đổi ngoài nhiệm vụ.

## 13. Ngoài phạm vi

- Không cho phép sửa họ tên, email, giới tính, điện thoại hoặc campus trong flow này.
- Không thay đổi quyền của ADMIN/HO nếu không cần cho compatibility.
- Không tạo mới/xóa phòng ban.
- Không thay thế trưởng phòng của một phòng đã có head thông qua dropdown này; muốn thay thế phải dùng flow nghiệp vụ riêng.
- Không sửa mật khẩu, provider đăng nhập hoặc thông tin SSO.

## 14. Thứ tự triển khai khuyến nghị

1. Bổ sung StudentCode vào detail DTO/handler và frontend type.
2. Thêm API role-assignment-options và test scope/options.
3. Mở rộng UpdateAccountRole request, validation và handler.
4. Viết backend tests cho role shape, department head và MSSV.
5. Refactor state của modal frontend.
6. Implement UI động theo role và null display.
7. Viết frontend tests.
8. Chạy build/typecheck/test toàn bộ phần liên quan.
9. Kiểm tra diff để bảo đảm không có thay đổi ngoài phạm vi.
