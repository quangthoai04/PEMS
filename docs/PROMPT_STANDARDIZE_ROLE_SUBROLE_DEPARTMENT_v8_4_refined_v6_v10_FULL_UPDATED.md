# PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT_v8_4_refined_v6_FULL_UPDATED

> **Bản FULL-PRESERVED cập nhật theo PEMS v8.4 refined v6 no dynamic permissions.**  
> File này gồm 2 phần:  
> - **PHẦN A — Nội dung chuẩn hiện tại để code/triển khai.**  
> - **PHẦN B — Nội dung gốc/legacy được giữ lại đầy đủ để đối chiếu lịch sử.**  
>
> Khi PHẦN A mâu thuẫn với PHẦN B, **luôn ưu tiên PHẦN A**. PHẦN B không được dùng làm nguồn code trực tiếp nếu có dấu hiệu legacy như `DEPT`, `STAFF_L`, `STAFF_P`, `Staff click nhận đón`, `auto Staff Leader làm host`, `Staff Leader/HO cancel sau APPROVED`, hoặc dynamic permissions.

## 0. Cách đọc file này

```text
1. Đọc PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6.md trước.
2. Đọc PHẦN A của file này để lấy nghiệp vụ/logic hiện hành.
3. Chỉ dùng PHẦN B để hiểu nguồn gốc tài liệu cũ, không dùng để sinh code nếu mâu thuẫn với PHẦN A.
4. Nếu cần code, backend phải kiểm tra lại bằng schema v8.4 refined v6 và seed v7/v6 dynamic time tương ứng.
```


# V10 Addendum — Scope Fields Added Outside Role/SubRole

Role/subRole canonical rules không thay đổi trong SQL v10. Tuy nhiên, một số scope nghiệp vụ mới cần được AI/code agent ghi nhớ khi áp dụng role policy:

```text
1. Partner approval by Staff Leader must use partners.owner_campus_id.
2. Email action token handling may use recipient_user_id, recipient_email and target_type/target_id, not role permission tables.
3. Logistics handover signing uses visit_logistics_item_handovers and still follows participant/logistics assignment scope.
4. FAQ type enum changed, but role/subRole rules are unaffected.
5. No dynamic permissions tables are reintroduced.
```

Không sửa các helper role/subRole chuẩn. Chỉ bổ sung scope check tương ứng trong từng UC.

---

# PHẦN A — NỘI DUNG CHUẨN HIỆN TẠI / UPDATED CANONICAL CONTENT

# PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT_v8_4_refined_v6_UPDATED

## 0. Mục tiêu

Chuẩn hóa toàn bộ Role/SubRole/Department trong PEMS để code và seed không còn lẫn giữa role cũ, label UI và runtime value.

Áp dụng cho:

```text
SQL schema/seed
Backend .NET
Frontend React/TypeScript
Docs
Test scripts
AI Agent prompts
```

---

## 1. Quy ước role/subRole chuẩn

| Nhóm người dùng | role_code | sub_role | Ghi chú |
|---|---|---|---|
| Admin | `ADMIN` | `NULL` | Quản trị kỹ thuật |
| Head Office | `HO` | `NULL` | Multi-campus |
| Staff Leader | `STAFF` | `LEADER` | Trưởng IC campus |
| IC Staff | `STAFF` | `STAFF` | Nhân sự IC thường, có thể làm host |
| Department Leader | `DEPARTMENT` | `LEADER` | Trưởng phòng ban GENERAL |
| Department Staff | `DEPARTMENT` | `STAFF` | Nhân sự phòng ban GENERAL |
| Student | `STUDENT` | `NULL` | Sinh viên hỗ trợ |
| Visitor | `VISITOR` | `NULL` | Khách ngoài |

---

## 2. Giá trị bị cấm

Không dùng trong DB/backend/frontend/docs runtime:

```text
DEPT
STAFF_LEADER
IC_STAFF_LEADER
DEPT_LEADER
DEPARTMENT_LEADER
LEADER as role_code
STAFF_L as role_code
STAFF_P as role_code
DEPT_L as role_code
DEPT_P as role_code
```

Không phân biệt leader bằng email:

```text
email.Contains("leader")
LIKE '%leader%'
```

Không tìm Staff thường bằng phủ định:

```text
subRole != LEADER
```

Phải dùng khẳng định:

```text
role_code = STAFF AND sub_role = STAFF
```

---

## 3. Department type rules

Department có 2 loại:

```text
IC
GENERAL
```

Mapping bắt buộc:

```text
STAFF + LEADER  → department_type = IC
STAFF + STAFF   → department_type = IC
DEPARTMENT + LEADER → department_type = GENERAL
DEPARTMENT + STAFF  → department_type = GENERAL
```

Không cho:

```text
Staff user thuộc GENERAL department
Department user thuộc IC department
Visitor có department_id
Admin/HO/Student dùng sub_role
```

---

## 4. Uniqueness business rules

Backend phải enforce khi tạo/cập nhật account:

```text
1. Mỗi campus chỉ có một Staff Leader ACTIVE.
2. Mỗi GENERAL department chỉ có một Department Leader ACTIVE.
3. Staff Leader phải cùng campus với IC department.
4. Department Leader/Staff phải cùng campus với GENERAL department.
5. Không tạo account vào campus/department INACTIVE.
6. Không xóa cứng user đã có lịch sử nghiệp vụ.
```

---

## 5. SQL/seed rules

Seed user phải set rõ:

```text
role_id/role_code
sub_role
primary_campus_id
department_id
status
created_via
```

Không seed theo pattern email.

Roles table chỉ nên có:

```sql
ADMIN, HO, STAFF, DEPARTMENT, STUDENT, VISITOR
```

Vì schema v8.4 refined v6 đã bỏ dynamic permissions, không seed/query các bảng:

```text
permissions
role_permissions
```

Nếu file legacy còn các bảng đó, đánh dấu legacy/deprecated và không dùng runtime.

---

## 6. Backend constants chuẩn

```csharp
public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string HO = "HO";
    public const string Staff = "STAFF";
    public const string Department = "DEPARTMENT";
    public const string Student = "STUDENT";
    public const string Visitor = "VISITOR";
}

public static class SubRoles
{
    public const string Staff = "STAFF";
    public const string Leader = "LEADER";
}
```

Helper bắt buộc:

```csharp
IsStaffLeader     => role STAFF + subRole LEADER
IsStaffMember     => role STAFF + subRole STAFF
IsDepartmentLeader => role DEPARTMENT + subRole LEADER
IsDepartmentStaff  => role DEPARTMENT + subRole STAFF
```

---

## 7. Frontend constants chuẩn

```ts
export const ROLE_CODES = {
  ADMIN: 'ADMIN',
  HO: 'HO',
  STAFF: 'STAFF',
  DEPARTMENT: 'DEPARTMENT',
  STUDENT: 'STUDENT',
  VISITOR: 'VISITOR',
} as const;

export const SUB_ROLES = {
  STAFF: 'STAFF',
  LEADER: 'LEADER',
} as const;
```

Helper frontend:

```ts
isStaffLeader(user)      // STAFF + LEADER
isStaffMember(user)      // STAFF + STAFF
isDepartmentLeader(user) // DEPARTMENT + LEADER
isDepartmentStaff(user)  // DEPARTMENT + STAFF
```

Không dùng biến `isStaff` để gom cả Department/Student/Visitor.

---

## 8. Host candidate rule

Khi Staff Leader gán host, backend/frontend chỉ hiện:

```text
user.status = ACTIVE
role_code = STAFF
sub_role = STAFF
primary_campus_id = visit_instance.campus_id
department_type = IC
department.status = ACTIVE
```

Không hiện:

```text
STAFF + LEADER
DEPARTMENT + STAFF/LEADER
STUDENT
HO
ADMIN
VISITOR
Inactive/Locked user
User khác campus
```

---

## 9. Account creation rules

### 9.1 HO tạo Staff Leader/IC Staff

```text
role_code = STAFF
sub_role = LEADER hoặc STAFF
campus_id bắt buộc
department_id bắt buộc, department_type = IC
```

Nếu tạo Staff Leader:

```text
campus chưa có Staff Leader ACTIVE
```

### 9.2 Staff Leader tạo IC Staff

Staff Leader chỉ được tạo:

```text
role_code = STAFF
sub_role = STAFF
primary_campus_id = campus của Staff Leader
department_type = IC
```

Không được tạo Staff Leader khác.

### 9.3 HO tạo Department Leader

```text
role_code = DEPARTMENT
sub_role = LEADER
department_type = GENERAL
```

Mỗi GENERAL department chỉ có một Department Leader ACTIVE.

### 9.4 Department Leader tạo Department Staff

```text
role_code = DEPARTMENT
sub_role = STAFF
primary_campus_id = campus của Department Leader
department_id = department của Department Leader
```

Staff Leader không tạo Department Staff.

### 9.5 HO account

Cần policy rõ:

```text
Mỗi campus chỉ một HO ACTIVE hay nhiều HO cùng campus?
```

Nếu chưa chốt, không tự tạo nhiều HO ACTIVE cùng campus.

---

## 10. DB verification queries

### 10.1 Invalid role/subRole

```sql
SELECT u.user_id, u.email, r.role_code, u.sub_role, u.primary_campus_id, u.department_id
FROM users u
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code IN ('DEPT','STAFF_LEADER','IC_STAFF_LEADER','DEPT_LEADER','DEPARTMENT_LEADER')
   OR (r.role_code IN ('STAFF','DEPARTMENT') AND u.sub_role NOT IN ('STAFF','LEADER'))
   OR (r.role_code NOT IN ('STAFF','DEPARTMENT') AND u.sub_role IS NOT NULL);
```

Expected: `0 rows`.

### 10.2 Staff/Department wrong department type

```sql
SELECT u.user_id, u.email, r.role_code, u.sub_role, d.department_type
FROM users u
JOIN roles r ON r.role_id = u.role_id
LEFT JOIN departments d ON d.department_id = u.department_id
WHERE (r.role_code = 'STAFF' AND d.department_type <> 'IC')
   OR (r.role_code = 'DEPARTMENT' AND d.department_type <> 'GENERAL');
```

Expected: `0 rows`.

### 10.3 Multiple active Staff Leaders per campus

```sql
SELECT u.primary_campus_id, COUNT(*) AS active_staff_leader_count
FROM users u
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'STAFF'
  AND u.sub_role = 'LEADER'
  AND u.status = 'ACTIVE'
GROUP BY u.primary_campus_id
HAVING COUNT(*) > 1;
```

Expected: `0 rows`.

### 10.4 Multiple active Department Leaders per department

```sql
SELECT u.department_id, COUNT(*) AS active_department_leader_count
FROM users u
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'DEPARTMENT'
  AND u.sub_role = 'LEADER'
  AND u.status = 'ACTIVE'
GROUP BY u.department_id
HAVING COUNT(*) > 1;
```

Expected: `0 rows`.

---

## 11. Search checklist khi sửa project

Tìm toàn repo:

```text
DEPT
STAFF_LEADER
IC_STAFF_LEADER
DEPT_LEADER
DEPARTMENT_LEADER
STAFF_L
STAFF_P
DEPT_L
DEPT_P
RoleCodes.Dept
isDeptStaff
isDeptLeader
email.Contains("leader")
LIKE '%leader%'
subRole !=
```

Nếu xuất hiện trong legacy docs, ghi rõ legacy/deprecated. Nếu xuất hiện trong runtime code, phải sửa.

---

## 12. Build và báo cáo

Sau khi sửa:

```bash
dotnet build
npm run build
```

Báo cáo phải có:

```text
SQL files changed
Backend files changed
Frontend files changed
Docs updated
Invalid role/subRole query result
Build result
Manual test result cho Staff, Staff Leader, Department Staff, Department Leader, HO, Visitor
```

---

# PHẦN B — NỘI DUNG GỐC / LEGACY PRESERVED CONTENT

> Phần này được giữ nguyên để đối chiếu lịch sử. Không dùng phần này để code nếu mâu thuẫn với PHẦN A hoặc file canonical.

# PROMPT CHO CLAUDE — Chuẩn hóa toàn bộ Role/SubRole PEMS

## 0. Bối cảnh

Bạn đang làm việc trên hệ thống **PEMS — Partnership Engagement Management System**. Hiện tại hệ thống có nhiều chỗ đang dùng không thống nhất giữa `STAFF`, `STAFF_LEADER`, `DEPT`, `DEPARTMENT`, `DEPT_LEADER`, `Department Staff`, `Department Leader`, v.v.

Yêu cầu lần này là **chuẩn hóa toàn bộ hệ thống từ SQL → Backend → Frontend → Docs** theo một quy ước duy nhất để sau này không còn phải lần mò logic role/subRole ở nhiều nơi.

Không chỉ sửa một màn hình. Hãy rà soát toàn bộ project và cập nhật đồng bộ.

---

## 1. Quy ước chuẩn bắt buộc từ nay về sau

Toàn hệ thống PEMS phải hiểu role/subRole theo đúng bảng sau:

| Nhóm người dùng | role_code chuẩn | sub_role chuẩn | Ý nghĩa |
|---|---|---|---|
| Admin | `ADMIN` | `NULL` hoặc `NONE` tùy bảng | Quản trị hệ thống |
| Head Office | `HO` | `NULL` hoặc `NONE` tùy bảng | Cấp Head Office |
| Staff thường | `STAFF` | `STAFF` | Nhân sự IC thường |
| Staff Leader | `STAFF` | `LEADER` | Trưởng IC / người duyệt campus |
| Department Staff | `DEPARTMENT` | `STAFF` | Nhân sự phòng ban |
| Department Leader | `DEPARTMENT` | `LEADER` | Trưởng phòng ban |
| Student | `STUDENT` | `NULL` hoặc `NONE` tùy bảng | Sinh viên hỗ trợ |
| Visitor | `VISITOR` | `NULL` hoặc `NONE` tùy bảng | Khách ngoài |

### Quy tắc quan trọng

1. **Không dùng role `DEPT` nữa.** Role Department phải viết đầy đủ là:

```txt
DEPARTMENT
```

2. **Không tạo role riêng cho Leader.** Không được dùng các role code sau:

```txt
STAFF_LEADER
IC_STAFF_LEADER
DEPT
DEPT_LEADER
DEPARTMENT_LEADER
LEADER
```

3. Staff Leader luôn là:

```txt
role_code = STAFF
sub_role = LEADER
```

4. Staff thường luôn là:

```txt
role_code = STAFF
sub_role = STAFF
```

5. Department Leader luôn là:

```txt
role_code = DEPARTMENT
sub_role = LEADER
```

6. Department Staff luôn là:

```txt
role_code = DEPARTMENT
sub_role = STAFF
```

7. Các role `ADMIN`, `HO`, `STUDENT`, `VISITOR` không dùng `sub_role` trong bảng `users`. Trong bảng permission nếu cần subRole thì dùng `NONE`.

---

## 2. Phạm vi bắt buộc phải rà soát

Hãy quét toàn bộ repository, tối thiểu các nhóm file sau:

### SQL / Database

```txt
database/scripts/*.sql
database/seed/*.sql
manual_fix_role_permissions.sql
roles.sql
permissions.sql
permission_matrix.sql
pems_full.sql
DbSeeder project nếu có
```

### Backend .NET

```txt
RoleCodes.cs
RoleConstants.cs
SubRoles.cs
UserRoles.cs
PermissionConstants.cs
Authorization policies
Auth handlers
Account handlers
Role/Permission handlers
VisitRequest handlers
GetHostCandidatesQueryHandler.cs
ViewGuestDelegationListQueryHandler.cs
CreateUser / UpdateUser command handlers
```

### Frontend React/TypeScript

```txt
AuthContext
useAuth
role helpers
route guards
permission guards
sidebar/menu config
DashboardHome.tsx
SharedDashboardView.tsx
VisitRequestManagement.tsx
AssignHostModal.tsx
Account Management screens
delegations.types.ts
schema validation files
```

### Docs

```txt
DATABASE_SCHEMA.md
PERMISSION_MATRIX.md
USE_CASE_LIST.md
USE_CASE_NOTES.md
PROJECT_STRUCTURE_FULL.md
CLEAN_ARCHITECTURE.md
README.md
Các file markdown mô tả RBAC / Account / Authentication nếu có
```

Nếu tên file thực tế khác thì tìm theo nội dung `role`, `sub_role`, `DEPT`, `department`, `leader`, `staff leader`, `department leader`.

---

## 3. Việc cần làm trong SQL

### 3.1. Đổi role `DEPT` thành `DEPARTMENT`

Tìm toàn bộ SQL đang dùng:

```sql
'DEPT'
```

Đổi thành:

```sql
'DEPARTMENT'
```

Các bảng cần kiểm tra:

```txt
roles
user_roles
role_permissions
permissions nếu có reference role
seed users
seed role permission matrix
```

Nếu file SQL là file tạo DB từ đầu, sửa trực tiếp trong `INSERT INTO roles`.

Ví dụ bảng `roles` chuẩn phải có:

```sql
INSERT INTO roles (role_code, role_name, description, is_system, created_at, updated_at)
VALUES
  ('ADMIN', 'Admin', 'System administrator', 1, NOW(), NOW()),
  ('HO', 'Head Office', 'Head Office user', 1, NOW(), NOW()),
  ('STAFF', 'Staff', 'IC staff group', 1, NOW(), NOW()),
  ('DEPARTMENT', 'Department', 'Department user group', 1, NOW(), NOW()),
  ('STUDENT', 'Student', 'Student supporter', 1, NOW(), NOW()),
  ('VISITOR', 'Visitor', 'External visitor', 1, NOW(), NOW());
```

Nếu đang có update/migration từ DB cũ, thêm migration SQL an toàn:

```sql
UPDATE roles
SET role_code = 'DEPARTMENT',
    role_name = 'Department'
WHERE role_code = 'DEPT';
```

Sau đó cập nhật các reference nếu cần:

```sql
UPDATE role_permissions
SET role_code = 'DEPARTMENT'
WHERE role_code = 'DEPT';
```

Nếu `user_roles` dùng `role_id` thì chỉ cần đổi `roles.role_code`, không cần đổi `user_roles`.

---

### 3.2. Chuẩn hóa `sub_role`

Chọn một chuẩn lưu trong DB. Ưu tiên dùng uppercase để khớp code:

```txt
STAFF
LEADER
```

Nếu schema hiện tại đang là enum `('Leader','Staff')`, cần cân nhắc đổi sang uppercase:

```sql
sub_role ENUM('STAFF','LEADER') NULL
```

Nếu không muốn đổi enum vì sợ ảnh hưởng dữ liệu, thì giữ DB cũ nhưng **backend/frontend bắt buộc normalize uppercase khi so sánh**. Tuy nhiên docs phải ghi rõ DB dùng kiểu nào.

Khuyến nghị chuẩn mới:

```sql
sub_role ENUM('STAFF','LEADER') NULL
```

---

### 3.3. Seed user theo role/subRole chuẩn

Không được dùng email pattern như:

```sql
WHERE email LIKE '%leader%'
```

Không được dùng logic:

```csharp
email.Contains("leader")
```

Phải set rõ ràng theo danh sách tài khoản seed cụ thể.

Ví dụ Staff Leader:

```sql
UPDATE users
SET sub_role = 'LEADER'
WHERE email IN (
  'staffleader.hn@fpt.edu.vn',
  'staffleader.hcm@fpt.edu.vn'
);
```

Staff thường:

```sql
UPDATE users u
JOIN user_roles ur ON ur.user_id = u.user_id
JOIN roles r ON r.role_id = ur.role_id
SET u.sub_role = 'STAFF'
WHERE r.role_code = 'STAFF'
  AND u.sub_role IS NULL;
```

Department Leader:

```sql
UPDATE users
SET sub_role = 'LEADER'
WHERE email IN (
  'departmentleader.hn@fpt.edu.vn',
  'departmentleader.hcm@fpt.edu.vn'
);
```

Department Staff:

```sql
UPDATE users u
JOIN user_roles ur ON ur.user_id = u.user_id
JOIN roles r ON r.role_id = ur.role_id
SET u.sub_role = 'STAFF'
WHERE r.role_code = 'DEPARTMENT'
  AND u.sub_role IS NULL;
```

Các role không dùng subRole:

```sql
UPDATE users u
JOIN user_roles ur ON ur.user_id = u.user_id
JOIN roles r ON r.role_id = ur.role_id
SET u.sub_role = NULL
WHERE r.role_code IN ('ADMIN', 'HO', 'STUDENT', 'VISITOR');
```

---

### 3.4. Chuẩn hóa `role_permissions`

Nếu bảng `role_permissions` có cột `sub_role`, chuẩn phải là:

| role_code | sub_role |
|---|---|
| `ADMIN` | `NONE` |
| `HO` | `NONE` |
| `STAFF` | `STAFF` |
| `STAFF` | `LEADER` |
| `DEPARTMENT` | `STAFF` |
| `DEPARTMENT` | `LEADER` |
| `STUDENT` | `NONE` |
| `VISITOR` | `NONE` |

Không được còn:

```txt
DEPT
DEPT_LEADER
DEPARTMENT_LEADER
STAFF_LEADER
```

Nếu schema hiện tại dùng `Leader`, `Staff`, `NONE`, hãy thống nhất lại hoặc normalize đầy đủ trong code/docs.

---

## 4. Việc cần làm trong Backend

### 4.1. Chuẩn hóa constants

Tìm và sửa các file constants/enums liên quan đến role/subRole.

Constants chuẩn mong muốn:

```csharp
public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string HO = "HO";
    public const string Staff = "STAFF";
    public const string Department = "DEPARTMENT";
    public const string Student = "STUDENT";
    public const string Visitor = "VISITOR";
}

public static class SubRoles
{
    public const string Staff = "STAFF";
    public const string Leader = "LEADER";
    public const string None = "NONE";
}
```

Không còn:

```csharp
RoleCodes.Dept
"DEPT"
"STAFF_LEADER"
"DEPT_LEADER"
"DEPARTMENT_LEADER"
```

---

### 4.2. Tạo helper dùng chung cho role/subRole

Không để logic check role/subRole rải rác ở nhiều nơi.

Tạo hoặc cập nhật helper/service/extension, ví dụ:

```csharp
public static class UserRoleRules
{
    public static bool HasRole(User user, string roleCode)
    {
        return user.UserRoles.Any(ur =>
            string.Equals(ur.Role.RoleCode, roleCode, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasSubRole(User user, string subRole)
    {
        return string.Equals(user.SubRole, subRole, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsStaffMember(User user)
        => HasRole(user, RoleCodes.Staff) && HasSubRole(user, SubRoles.Staff);

    public static bool IsStaffLeader(User user)
        => HasRole(user, RoleCodes.Staff) && HasSubRole(user, SubRoles.Leader);

    public static bool IsDepartmentStaff(User user)
        => HasRole(user, RoleCodes.Department) && HasSubRole(user, SubRoles.Staff);

    public static bool IsDepartmentLeader(User user)
        => HasRole(user, RoleCodes.Department) && HasSubRole(user, SubRoles.Leader);
}
```

Nếu dự án không dùng entity `UserRoles` mà dùng property khác, điều chỉnh theo entity thật.

---

### 4.3. Sửa các check sai

Tìm toàn bộ backend có các pattern sau và sửa:

```txt
DEPT
RoleCodes.Dept
STAFF_LEADER
DEPT_LEADER
DEPARTMENT_LEADER
role == "LEADER"
subRole != "Leader"
subRole != SubRoles.Leader
email.Contains("leader")
LIKE '%leader%'
```

Thay bằng logic chuẩn:

```txt
Staff thường = role STAFF + subRole STAFF
Staff Leader = role STAFF + subRole LEADER
Department Staff = role DEPARTMENT + subRole STAFF
Department Leader = role DEPARTMENT + subRole LEADER
```

Không dùng phủ định kiểu:

```csharp
u.SubRole != SubRoles.Leader
```

Vì Staff thường phải được xác định bằng:

```csharp
u.SubRole == SubRoles.Staff
```

---

### 4.4. Sửa API lấy danh sách host khi duyệt đơn

Khi Staff Leader duyệt đơn và gán host, danh sách host chỉ được hiện Staff thường thuộc cùng campus.

Điều kiện bắt buộc:

```txt
user.status = ACTIVE
user.role = STAFF
user.sub_role = STAFF
user.primary_campus_id = campusId của visit_request_campuses
user.user_id != currentUserId
```

Ví dụ:

```csharp
.Where(u =>
    u.Status == UserStatuses.Active &&
    u.PrimaryCampusId == campusId &&
    u.UserRoles.Any(ur => ur.Role.RoleCode == RoleCodes.Staff) &&
    u.SubRole == SubRoles.Staff &&
    u.UserId != currentUserId
)
```

Không hiện:

```txt
Staff Leader = STAFF + LEADER
Department Staff = DEPARTMENT + STAFF
Department Leader = DEPARTMENT + LEADER
Admin
HO
Student
Visitor
Inactive/Locked user
User khác campus
```

Nếu modal không có kết quả, log debug các giá trị:

```txt
campusId
currentUserId
currentUser role/subRole
count STAFF cùng campus
count STAFF + subRole STAFF cùng campus
```

---

### 4.5. Sửa validation tạo/sửa user

Ở các command/handler tạo hoặc cập nhật user, bắt buộc validate:

```txt
Role STAFF thì subRole bắt buộc là STAFF hoặc LEADER.
Role DEPARTMENT thì subRole bắt buộc là STAFF hoặc LEADER.
Role ADMIN, HO, STUDENT, VISITOR thì subRole phải NULL hoặc bị clear.
Không cho lưu role STAFF mà subRole NULL.
Không cho lưu role DEPARTMENT mà subRole NULL.
Không cho lưu role DEPT.
Không cho lưu role STAFF_LEADER / DEPARTMENT_LEADER.
```

Nếu nhận input từ frontend là label tiếng Việt, phải map về value chuẩn trước khi lưu.

---

## 5. Việc cần làm trong Frontend

### 5.1. Chuẩn hóa role/subRole constants

Tạo hoặc cập nhật constants frontend:

```ts
export const ROLE_CODES = {
  ADMIN: 'ADMIN',
  HO: 'HO',
  STAFF: 'STAFF',
  DEPARTMENT: 'DEPARTMENT',
  STUDENT: 'STUDENT',
  VISITOR: 'VISITOR',
} as const;

export const SUB_ROLES = {
  STAFF: 'STAFF',
  LEADER: 'LEADER',
} as const;
```

Không dùng:

```ts
'DEPT'
'STAFF_LEADER'
'DEPT_LEADER'
'DEPARTMENT_LEADER'
```

---

### 5.2. Tạo helper frontend dùng chung

Tạo helper, ví dụ `roleUtils.ts`:

```ts
export function normalizeRole(value?: string | null) {
  return value?.trim().toUpperCase() ?? '';
}

export function normalizeSubRole(value?: string | null) {
  return value?.trim().toUpperCase() ?? '';
}

export function isStaffMember(user: { role?: string | null; subRole?: string | null }) {
  return normalizeRole(user.role) === 'STAFF' && normalizeSubRole(user.subRole) === 'STAFF';
}

export function isStaffLeader(user: { role?: string | null; subRole?: string | null }) {
  return normalizeRole(user.role) === 'STAFF' && normalizeSubRole(user.subRole) === 'LEADER';
}

export function isDepartmentStaff(user: { role?: string | null; subRole?: string | null }) {
  return normalizeRole(user.role) === 'DEPARTMENT' && normalizeSubRole(user.subRole) === 'STAFF';
}

export function isDepartmentLeader(user: { role?: string | null; subRole?: string | null }) {
  return normalizeRole(user.role) === 'DEPARTMENT' && normalizeSubRole(user.subRole) === 'LEADER';
}
```

Sau đó sửa các màn dùng helper này, tránh tự check thủ công ở nhiều nơi.

---

### 5.3. Sửa `DashboardHome.tsx`

Nếu đang có đoạn kiểu:

```ts
const isStaff = user?.role?.toUpperCase() === 'STAFF' || isStaffLeader || isDeptLeader || isDeptStaff || isStudent || isVisitor;
```

Thì sửa vì tên biến sai nghĩa. `isStaff` không được gom cả Department, Student, Visitor.

Sửa thành:

```ts
const role = user?.role?.toUpperCase();
const subRole = user?.subRole?.toUpperCase();

const isStaffMember = role === 'STAFF' && subRole === 'STAFF';
const isStaffLeader = role === 'STAFF' && subRole === 'LEADER';

const isDepartmentStaff = role === 'DEPARTMENT' && subRole === 'STAFF';
const isDepartmentLeader = role === 'DEPARTMENT' && subRole === 'LEADER';

const isStudent = role === 'STUDENT';
const isVisitor = role === 'VISITOR';
const isAdmin = role === 'ADMIN';
const isHO = role === 'HO';

const shouldUseSharedDashboard =
  isStaffMember ||
  isStaffLeader ||
  isDepartmentStaff ||
  isDepartmentLeader ||
  isStudent ||
  isVisitor;
```

Render logic:

```tsx
{isHO && hasPermission(PERMISSIONS.VIEW_DASHBOARD_STATISTICS) ? (
  <HODashboardView />
) : isAdmin ? (
  <AdminDashboardView />
) : shouldUseSharedDashboard ? (
  <SharedDashboardView
    user={user}
    isDepartmentLeader={isDepartmentLeader}
    isDepartmentStaff={isDepartmentStaff}
    isStudent={isStudent}
    isVisitor={isVisitor}
  />
) : (
  <DefaultDashboard />
)}
```

Nếu `SharedDashboardView` hiện đang nhận props `isDeptLeader`, `isDeptStaff`, có thể giữ props cũ tạm thời nhưng nên đổi tên sang `isDepartmentLeader`, `isDepartmentStaff` để rõ nghĩa.

---

### 5.4. Sửa form tạo/sửa user

Frontend form tạo/sửa tài khoản:

- Chọn role `STAFF` thì hiện dropdown subRole: `STAFF`, `LEADER`.
- Chọn role `DEPARTMENT` thì hiện dropdown subRole: `STAFF`, `LEADER`.
- Chọn role `ADMIN`, `HO`, `STUDENT`, `VISITOR` thì ẩn subRole và clear value.
- Không hiển thị role `DEPT`.
- Không hiển thị role `STAFF_LEADER`.
- Không hiển thị role `DEPARTMENT_LEADER`.

Label hiển thị:

| role | subRole | Label |
|---|---|---|
| `STAFF` | `STAFF` | Staff |
| `STAFF` | `LEADER` | Staff Leader |
| `DEPARTMENT` | `STAFF` | Department Staff |
| `DEPARTMENT` | `LEADER` | Department Leader |

---

## 6. Việc cần làm trong Docs

Cập nhật toàn bộ docs liên quan. Thêm mục sau vào các file mô tả database/RBAC/use case/architecture nếu phù hợp.

### Nội dung bắt buộc trong docs

```md
## Role/SubRole Canonical Rules

PEMS không dùng role riêng cho Staff Leader hoặc Department Leader. Hệ thống dùng role chính kết hợp với subRole.

| Nhóm người dùng | role_code | sub_role | Ghi chú |
|---|---|---|---|
| Admin | ADMIN | NULL / NONE | Quản trị hệ thống |
| Head Office | HO | NULL / NONE | Cấp Head Office |
| Staff | STAFF | STAFF | Nhân sự IC thường |
| Staff Leader | STAFF | LEADER | Trưởng IC / người duyệt campus |
| Department Staff | DEPARTMENT | STAFF | Nhân sự phòng ban |
| Department Leader | DEPARTMENT | LEADER | Trưởng phòng ban |
| Student | STUDENT | NULL / NONE | Sinh viên hỗ trợ |
| Visitor | VISITOR | NULL / NONE | Khách ngoài |

### Quy tắc

- Không dùng role `DEPT`.
- Không dùng role `STAFF_LEADER`.
- Không dùng role `DEPT_LEADER`.
- Không dùng role `DEPARTMENT_LEADER`.
- Staff Leader luôn là `role_code = STAFF` + `sub_role = LEADER`.
- Staff thường luôn là `role_code = STAFF` + `sub_role = STAFF`.
- Department Leader luôn là `role_code = DEPARTMENT` + `sub_role = LEADER`.
- Department Staff luôn là `role_code = DEPARTMENT` + `sub_role = STAFF`.
- ADMIN, HO, STUDENT, VISITOR không dùng subRole trong bảng users.
- Nếu cần gắn permission theo subRole, các role không phân cấp dùng `NONE`.
- Code phải normalize role/subRole khi so sánh để tránh lỗi casing.
```

Cập nhật mọi bảng permission matrix đang ghi `DEPT` thành `DEPARTMENT`.

---

## 7. Search checklist bắt buộc

Sau khi sửa, tìm toàn repo để chắc chắn không còn các chuỗi cũ:

```txt
DEPT
STAFF_LEADER
IC_STAFF_LEADER
DEPT_LEADER
DEPARTMENT_LEADER
RoleCodes.Dept
isDeptStaff
isDeptLeader
email.Contains("leader")
LIKE '%leader%'
subRole !=
SubRole !=
```

Lưu ý: `isDeptStaff` và `isDeptLeader` có thể còn trong docs cũ hoặc comment, nhưng nên đổi thành `isDepartmentStaff`, `isDepartmentLeader` để đồng bộ naming.

Nếu có `DEPT` trong nội dung lịch sử hoặc migration cũ, cần ghi chú rõ đó là legacy migration, không phải quy ước hiện tại.

---

## 8. Query kiểm tra DB sau khi chạy lại seed

Chạy query tổng:

```sql
SELECT 
  u.user_id,
  u.full_name,
  u.email,
  u.status,
  u.sub_role,
  r.role_code
FROM users u
JOIN user_roles ur ON ur.user_id = u.user_id
JOIN roles r ON r.role_id = ur.role_id
ORDER BY r.role_code, u.sub_role, u.email;
```

Kết quả đúng:

```txt
ADMIN, HO, STUDENT, VISITOR => sub_role NULL
STAFF Leader                => role STAFF + sub_role LEADER
STAFF thường                => role STAFF + sub_role STAFF
Department Leader           => role DEPARTMENT + sub_role LEADER
Department Staff            => role DEPARTMENT + sub_role STAFF
Không có role DEPT
Không có role STAFF_LEADER
Không có role DEPARTMENT_LEADER
Không có STAFF/DEPARTMENT nào sub_role NULL
```

Chạy query kiểm tra lỗi:

```sql
SELECT u.user_id, u.full_name, u.email, r.role_code, u.sub_role
FROM users u
JOIN user_roles ur ON ur.user_id = u.user_id
JOIN roles r ON r.role_id = ur.role_id
WHERE 
  r.role_code = 'DEPT'
  OR r.role_code IN ('STAFF_LEADER', 'DEPT_LEADER', 'DEPARTMENT_LEADER')
  OR (r.role_code IN ('STAFF', 'DEPARTMENT') AND u.sub_role IS NULL)
  OR (r.role_code NOT IN ('STAFF', 'DEPARTMENT') AND u.sub_role IS NOT NULL);
```

Query này bắt buộc phải trả về **0 dòng**.

Kiểm tra role permissions:

```sql
SELECT DISTINCT role_code, sub_role
FROM role_permissions
ORDER BY role_code, sub_role;
```

Kết quả chỉ được có các cặp hợp lệ:

```txt
ADMIN + NONE
HO + NONE
STAFF + STAFF
STAFF + LEADER
DEPARTMENT + STAFF
DEPARTMENT + LEADER
STUDENT + NONE
VISITOR + NONE
```

---

## 9. Test nghiệp vụ bắt buộc

### 9.1. Staff thường

- Đăng nhập user `role = STAFF`, `subRole = STAFF`.
- Không bị hiểu là Staff Leader.
- Không có quyền duyệt nếu RBAC không cấp.
- Xem đúng tab “Đơn phụ trách” và “Đơn mời tham dự”.

### 9.2. Staff Leader

- Đăng nhập user `role = STAFF`, `subRole = LEADER`.
- Có quyền duyệt/gán host theo RBAC.
- Khi mở modal gán host, danh sách chỉ hiện Staff thường cùng campus:

```txt
role STAFF + subRole STAFF
```

- Không hiện Staff Leader.
- Không hiện Department Staff/Leader.

### 9.3. Department Staff

- Đăng nhập user `role = DEPARTMENT`, `subRole = STAFF`.
- Không bị hiểu là Staff IC.
- Không xuất hiện trong danh sách host IC.

### 9.4. Department Leader

- Đăng nhập user `role = DEPARTMENT`, `subRole = LEADER`.
- Không bị hiểu là Staff Leader.
- Không xuất hiện trong danh sách host IC.

### 9.5. Admin/HO/Student/Visitor

- Không có `subRole` trong bảng users.
- Không bị lọt vào logic Staff/Department.

---

## 10. Build bắt buộc

Sau khi sửa xong phải chạy:

```bash
dotnet build
npm run build
```

Nếu solution có nhiều project, build toàn solution.

Không được che lỗi bằng `any`, comment code, hoặc bỏ validation.

---

## 11. Báo cáo sau khi hoàn thành

Sau khi làm xong, báo cáo lại theo format:

```md
# Role/SubRole Standardization Report

## 1. SQL files changed
- ...

## 2. Backend files changed
- ...

## 3. Frontend files changed
- ...

## 4. Docs updated
- ...

## 5. Removed legacy role codes
- DEPT: removed/replaced by DEPARTMENT
- STAFF_LEADER: removed/not found
- DEPT_LEADER: removed/not found
- DEPARTMENT_LEADER: removed/not found

## 6. DB verification
- Query invalid role/subRole result: 0 rows
- role_permissions distinct pairs: valid

## 7. Build result
- dotnet build: PASS
- npm run build: PASS

## 8. Manual test result
- Staff: PASS/FAIL
- Staff Leader: PASS/FAIL
- Department Staff: PASS/FAIL
- Department Leader: PASS/FAIL
```

---

## 12. Nguyên tắc không được vi phạm

- Không đổi nghiệp vụ bằng cách thêm role mới.
- Không dùng `DEPT` nữa.
- Không dùng email contains để phân biệt Leader.
- Không dùng phủ định `subRole != LEADER` để tìm Staff thường.
- Không dùng biến tên `isStaff` để gom cả Department/Student/Visitor.
- Không sửa seed để che bug logic.
- Không đổi permission matrix ngoài phạm vi role/subRole nếu không cần.
- Không làm mất dữ liệu RBAC hiện có.
