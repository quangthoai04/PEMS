> PEMS Department Management — Staff Leader
> Current baseline: SQL v10 / fixed role policy / no dynamic permissions.
> Actor runtime: Staff Leader = `role_code = STAFF` + `sub_role = LEADER`. Do not use `STAFF_LEADER` as runtime role_code.
> Scope runtime: `departments.campus_id = currentUser.primary_campus_id`. Frontend must not decide data scope. Backend is the final guard.

## Source of truth to read before coding

1. `DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md`
2. `PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md`
3. `PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md`
4. `USE_CASE_LIST.md` and `USE_CASE_NOTES.md`
5. Existing backend/frontend source code before editing.

## Database facts for this module

Table: `departments`

Important columns:

| Column | Rule in this module |
|---|---|
| `department_id` | Primary key. |
| `campus_id` | Must always equal current Staff Leader's `primary_campus_id` for this screen. |
| `name` | Required, max 150 chars, unique inside one campus. |
| `department_type` | Enum `IC` / `GENERAL`. UI does not show this as a column/filter in list. |
| `head_user_id` | Nullable. New department can be created without department leader. |
| `status` | Enum `ACTIVE` / `INACTIVE`. |
| `created_at`, `created_by` | Audit/accountability on create. |
| `updated_at`, `updated_by` | Audit/accountability on update/status change. |

Constraints/indexes to respect:

```sql
UNIQUE KEY uq_departments_campus_name (campus_id, name)
KEY idx_departments_status (status)
KEY idx_departments_head (head_user_id)
KEY idx_departments_campus_type (campus_id, department_type)
```

General UI rule agreed by Product Owner:

- Department list does not show the `department_type` column.
- Search/filter bar does not include Department Type filter.
- `IC` department is the default International Cooperation department created during campus setup. Staff Leader cannot create or disable/enable IC department from this module.
- Staff Leader only creates `GENERAL` departments.

# UC-101 — Add New Department — Staff Leader

## 1. Goal

Allow Staff Leader to create a new department inside their own campus.

This UC creates only `GENERAL` departments. Staff Leader does not choose department type because the campus already has the default `IC` department created by the HO/campus setup flow.

## 2. Actor and authorization

Primary actor: Staff Leader.

Runtime authorization rule:

```text
currentUser.role_code = STAFF
currentUser.sub_role = LEADER
currentUser.status = ACTIVE
currentUser.primary_campus_id IS NOT NULL
```

Backend must reject all other actors with HTTP `403 Forbidden`.

Backend must not trust `campusId`, `departmentType`, `headUserId`, `status`, `createdBy`, or `createdAt` sent from frontend.

## 3. Preconditions

- Staff Leader is authenticated in Internal Portal.
- Staff Leader account is `ACTIVE`.
- Staff Leader has exactly one `primary_campus_id`.
- The Staff Leader's campus exists and is `ACTIVE`.
- Default IC department already exists for the campus. If not, do not create another IC department in this UC; fix campus creation/seed logic separately.

## 4. Postconditions

On success:

- One new row exists in `departments`.
- `campus_id = currentUser.primary_campus_id`.
- `department_type = GENERAL`.
- `head_user_id = NULL`.
- `status = ACTIVE`.
- `created_by = currentUser.user_id`.
- `created_at = NOW()`.
- An audit log records the create action.
- Frontend refreshes list and shows the new department.

On failure:

- No department is created.
- User sees a clear Vietnamese error message.
- Validation errors keep modal/form input where appropriate.

## 5. UI behavior

Entry point: Department Management page.

Button:

```text
+ Thêm phòng ban mới
```

Modal/form fields:

| UI Field | Required | Notes |
|---|---:|---|
| Tên phòng ban | Yes | User enters text. Trim before validate. |
| Cơ sở | Readonly | Show current Staff Leader's campus name only. Do not allow selection. |

Do not display:

```text
Loại phòng ban
Trưởng phòng
Trạng thái
Mô tả
Campus dropdown with other campuses
```

Reason:

- `department_type` is auto `GENERAL`.
- `head_user_id` is assigned later through another UC, not here.
- `status` is auto `ACTIVE`.
- `description` does not exist in SQL v10 `departments` table.
- Staff Leader cannot create department for another campus.

Success toast:

```text
Đã thêm phòng ban mới.
```

Duplicate name error:

```text
Tên phòng ban đã tồn tại trong cơ sở này.
```

## 6. API contract

Recommended endpoint:

```http
POST /api/departments
```

Request body:

```json
{
  "name": "Phòng Công nghệ thông tin"
}
```

Do not accept or persist these fields from frontend for this UC:

```json
{
  "campusId": 1,
  "departmentType": "IC",
  "headUserId": 10,
  "status": "ACTIVE",
  "description": "...",
  "createdBy": 99
}
```

Response body example:

```json
{
  "departmentId": 35,
  "campusId": 1,
  "campusName": "FPT University Hà Nội",
  "name": "Phòng Công nghệ thông tin",
  "headUserId": null,
  "headFullName": null,
  "status": "ACTIVE",
  "departmentType": "GENERAL",
  "canToggleStatus": true,
  "createdAt": "2026-06-25T09:00:00",
  "message": "Đã thêm phòng ban mới."
}
```

`departmentType` may be returned for internal frontend logic, but the list UI must not show it as a visible column.

## 7. Backend flow

1. Resolve current user from auth context.
2. Verify effective role is Staff Leader: `STAFF + LEADER`.
3. Load current user including `primary_campus_id` and account `status`.
4. Load campus by `currentUser.primary_campus_id`; reject if not found or `INACTIVE`.
5. Normalize input name:
   - trim leading/trailing spaces.
   - collapse repeated internal spaces if existing project convention already does this.
6. Validate name.
7. Check duplicate within same campus using normalized comparison.
8. Create department with server-populated values.
9. Save in transaction.
10. Write audit log/audit changes.
11. Return created DTO.

Pseudo C# handler logic:

```csharp
if (!currentUser.IsStaffLeader())
    throw new ForbiddenException();

var campusId = currentUser.PrimaryCampusId
    ?? throw new BusinessRuleException("Tài khoản chưa được gán cơ sở.");

var campus = await _db.Campuses.FindAsync(campusId);
if (campus == null || campus.Status != Status.Active)
    throw new BusinessRuleException("Cơ sở hiện tại không hoạt động.");

var name = Normalize(request.Name);

var exists = await _db.Departments.AnyAsync(d =>
    d.CampusId == campusId &&
    d.Name.ToLower() == name.ToLower());

if (exists)
    throw new ConflictException("Tên phòng ban đã tồn tại trong cơ sở này.");

var department = new Department
{
    CampusId = campusId,
    Name = name,
    DepartmentType = DepartmentTypes.General,
    HeadUserId = null,
    Status = Status.Active,
    CreatedBy = currentUser.UserId,
    CreatedAt = clock.Now
};
```

## 8. Validation rules

Input validation:

| Field | Rule | Error |
|---|---|---|
| `name` | Required after trim | `Tên phòng ban là bắt buộc.` |
| `name` | Max 150 chars | `Tên phòng ban không được vượt quá 150 ký tự.` |

Business validation:

| Case | HTTP | Error |
|---|---:|---|
| User is not Staff Leader | 403 | `Bạn không có quyền tạo phòng ban.` |
| Current user has no primary campus | 422 | `Tài khoản chưa được gán cơ sở.` |
| Campus is not found | 422 | `Cơ sở hiện tại không tồn tại.` |
| Campus is inactive | 422 | `Không thể tạo phòng ban trong cơ sở đã ngừng hoạt động.` |
| Duplicate name in same campus | 409 | `Tên phòng ban đã tồn tại trong cơ sở này.` |
| Frontend sends `departmentType`, `campusId`, `headUserId`, `status` | Ignore or reject depending project convention | Backend must not use those fields. |

## 9. Business rules

| Code | Rule |
|---|---|
| BR-UC101-01 | Staff Leader can create departments only for their own campus. |
| BR-UC101-02 | New department created by this UC is always `department_type = GENERAL`. |
| BR-UC101-03 | New department is always `status = ACTIVE`. |
| BR-UC101-04 | New department has `head_user_id = NULL`; leader assignment is not part of this UC. |
| BR-UC101-05 | Department name must be unique inside the same campus. |
| BR-UC101-06 | Do not create IC department here. IC is a default/system department created during campus setup. |
| BR-UC101-07 | Do not add `description` unless database schema is patched first. |
| BR-UC101-08 | Create action must be audited. |

## 10. Frontend implementation notes

- Use real API data only; no mock departments.
- Add modal state for create form if not already present.
- Keep form simple: name + readonly campus label.
- Submit button disabled while saving.
- Show inline validation for name.
- On success: close modal, clear form, refresh list or prepend created item.
- Do not add department type dropdown.
- Do not add head/leader dropdown.
- Do not add status selector.

## 11. Manual test cases

| # | Given | When | Then |
|---:|---|---|---|
| 1 | Staff Leader HN logged in | Create department with unique valid name | Department is created in HN, type GENERAL, status ACTIVE, head NULL. |
| 2 | Staff Leader HN logged in | Submit empty name | 422/inline error, no row created. |
| 3 | Existing department name in HN | Submit same name with different casing/spaces | 409 conflict, no row created. |
| 4 | Staff Leader HN logged in | Frontend/devtools sends `campusId = HCM` | Backend ignores/rejects; department cannot be created outside HN. |
| 5 | Staff Leader HN logged in | Frontend/devtools sends `departmentType = IC` | Backend still creates GENERAL or rejects request; never creates IC. |
| 6 | Staff regular / Department user logged in | Calls create API directly | 403, no row created. |
| 7 | Campus is INACTIVE | Staff Leader tries create | 422, no row created. |

## 12. Definition of Done

- Backend endpoint runs against real MySQL data.
- No `NotImplementedException` remains in this UC path.
- Backend role/scope guard implemented.
- Input validation and duplicate validation implemented.
- Audit log implemented or documented if audit infrastructure is not ready.
- Frontend form connected to real API.
- No mock data.
- `department_type` is not selectable in UI.
- Created department is `GENERAL`, `ACTIVE`, and has no head.
- Backend build passes.
- Frontend build passes if frontend was changed.
