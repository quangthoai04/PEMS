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

# UC-106 — Manage Department Status — Staff Leader

## 1. Goal

Allow Staff Leader to enable or disable GENERAL departments inside their own campus.

Product Owner update:

- IC department is a default/system department. It must not show enable/disable toggle.
- Backend must block direct API attempts to change IC department status.
- Only GENERAL departments can be toggled.

## 2. Actor and authorization

Primary actor: Staff Leader.

Runtime authorization rule:

```text
currentUser.role_code = STAFF
currentUser.sub_role = LEADER
currentUser.status = ACTIVE
currentUser.primary_campus_id IS NOT NULL
```

Backend must reject all non-Staff Leader actors with HTTP `403 Forbidden`.

Backend must reject department outside current Staff Leader campus.

## 3. Preconditions

- Staff Leader is authenticated.
- Target department exists.
- Target department belongs to Staff Leader's `primary_campus_id`.
- Target department is `department_type = GENERAL`.
- Campus of department is `ACTIVE` when enabling.

## 4. Postconditions

On successful disable:

- `departments.status` changes from `ACTIVE` to `INACTIVE`.
- `updated_by = currentUser.user_id`.
- `updated_at = NOW()`.
- Existing historical relationships are preserved.
- Department is removed from new task/resource/routing dropdowns.
- Audit log records before/after status.

On successful enable:

- `departments.status` changes from `INACTIVE` to `ACTIVE`.
- `updated_by` and `updated_at` are updated.
- Department becomes selectable again for new tasks/resources if other rules allow it.
- If `head_user_id = NULL`, UI may show warning `Chưa gán trưởng phòng` but enabling is allowed unless Product Owner later changes this.

On failure:

- Status is unchanged.
- UI shows blocker/error message.

## 5. UI behavior

In department list action column:

| Department type | Toggle visible? | Action display |
|---|---:|---|
| `IC` | No | Empty or subtle label `Phòng mặc định`. |
| `GENERAL` | Yes | Toggle ACTIVE/INACTIVE. |

Toggle labels:

| Current status | UI action |
|---|---|
| ACTIVE | Ngừng hoạt động |
| INACTIVE | Kích hoạt lại |

Confirmation modal for disabling GENERAL department:

```text
Bạn có chắc muốn ngừng hoạt động phòng ban này?
Phòng ban ngừng hoạt động sẽ không được chọn cho nhiệm vụ hoặc yêu cầu hậu cần mới. Dữ liệu lịch sử vẫn được giữ nguyên.
```

Confirmation modal for enabling GENERAL department:

```text
Bạn có chắc muốn kích hoạt lại phòng ban này?
Sau khi kích hoạt, phòng ban có thể được chọn cho nhiệm vụ hoặc yêu cầu hậu cần mới.
```

No confirmation is needed for IC because no action is rendered.

## 6. API contract

Recommended endpoint:

```http
PATCH /api/departments/{departmentId}/status
```

Request body:

```json
{
  "newStatus": "INACTIVE",
  "reason": "Tạm ngừng nhận phân công hậu cần"
}
```

`reason` is optional. Since `departments` has no reason column, store reason in audit log only if audit supports metadata. Do not add `reason` column without SQL patch.

Response example:

```json
{
  "departmentId": 10,
  "name": "Phòng Công nghệ thông tin",
  "oldStatus": "ACTIVE",
  "newStatus": "INACTIVE",
  "status": "INACTIVE",
  "updatedAt": "2026-06-25T09:30:00",
  "updatedBy": 5,
  "message": "Đã ngừng hoạt động phòng ban."
}
```

Blocker response example:

```json
{
  "errorCode": "DEPARTMENT_STATUS_BLOCKED_BY_DEPENDENCIES",
  "message": "Không thể ngừng hoạt động phòng ban vì còn nhiệm vụ hoặc tài khoản đang hoạt động.",
  "blockers": [
    {
      "type": "ACTIVE_USERS",
      "count": 3,
      "message": "Còn 3 tài khoản đang thuộc phòng ban này."
    },
    {
      "type": "OPEN_LOGISTICS_ITEMS",
      "count": 2,
      "message": "Còn 2 yêu cầu hậu cần chưa hoàn tất."
    }
  ]
}
```

## 7. Backend flow

1. Resolve current user.
2. Verify Staff Leader role.
3. Validate `departmentId` and `newStatus`.
4. Load department by ID.
5. Verify department belongs to `currentUser.primary_campus_id`.
6. Verify `department.department_type == GENERAL`.
7. If target is same as current status, return success no-op or 409 depending project convention. Recommended: return current DTO with message `Trạng thái phòng ban không thay đổi.`
8. If disabling:
   - Check blockers.
   - If blockers exist: return 409 with blocker details.
   - Else set status `INACTIVE`.
9. If enabling:
   - Check campus is `ACTIVE`.
   - Check unique name still valid.
   - Set status `ACTIVE`.
10. Set `updated_by`, `updated_at`.
11. Write audit log/audit changes.
12. Save transaction and return DTO.

## 8. IC department rule

Backend must block status change for IC department even if frontend is bypassed.

Recommended error:

HTTP `409 Conflict`

```json
{
  "errorCode": "DEFAULT_IC_DEPARTMENT_STATUS_LOCKED",
  "message": "Không thể thay đổi trạng thái phòng Hợp tác quốc tế mặc định."
}
```

Reasoning:

- User is Staff Leader and can manage departments generally.
- The requested record is system/default IC department and cannot be toggled due to business rule.
- Therefore `409 Conflict` is more precise than `403`, though `403` is also acceptable if the project convention treats it as object-level forbidden.

## 9. Disable blocker rules

Disable GENERAL department must be blocked if any of these exist:

| Blocker | Required? | Notes |
|---|---:|---|
| Active internal users assigned to this department | Yes | Do not auto-disable accounts. Return blocker so Staff Leader handles account reassignment/status separately. |
| Active Department Leader/Department Staff in this department | Yes | Covered by active users, but can be separated for clearer message. |
| Open logistics/resource items assigned to this department | Yes | Any non-terminal status should block. Use current enum values from schema/code. |
| Open visit participants/tasks requiring this department | Yes | If `visit_participants` or task/assignment data references department. |
| Active delegations requiring this department | Yes | If current schema has department-scoped open delegation/resource records. |
| Campus inactive while enabling | Yes | Cannot enable department inside inactive campus. |

Do not hard-delete or cascade-delete department.

Do not auto-disable users.

Do not silently reassign tasks/users.

## 10. Suggested blocker query approach

Exact SQL depends on existing entity fields and enums. AI Agent must inspect schema/code before implementing. At minimum:

### Active users blocker

```sql
SELECT COUNT(*)
FROM users u
WHERE u.department_id = @DepartmentId
  AND u.status = 'ACTIVE';
```

### Open logistics blocker

Inspect `visit_logistics_items` for department-related columns in current entity/schema. If there is a department target/assignee department field, count rows where status is not terminal. Terminal statuses must come from code constants/schema, not guessed.

Example pattern only:

```sql
SELECT COUNT(*)
FROM visit_logistics_items li
WHERE li.department_id = @DepartmentId
  AND li.status NOT IN ('COMPLETED','CANCELLED','REJECTED');
```

If the current v10 schema does not have a direct `department_id` on a table, do not invent one. Use actual fields/joins only.

## 11. Validation rules

| Case | HTTP | Error |
|---|---:|---|
| Non-Staff Leader | 403 | `Bạn không có quyền thay đổi trạng thái phòng ban.` |
| Department not found | 404 | `Không tìm thấy phòng ban.` |
| Department outside current campus | 403 | `Bạn không có quyền thao tác với phòng ban ngoài cơ sở của mình.` |
| Department is IC | 409 | `Không thể thay đổi trạng thái phòng Hợp tác quốc tế mặc định.` |
| Invalid status | 422 | `Trạng thái phòng ban không hợp lệ.` |
| Disable has blockers | 409 | Return blocker list. |
| Enable while campus inactive | 422 | `Không thể kích hoạt phòng ban trong cơ sở đã ngừng hoạt động.` |

## 12. Business rules

| Code | Rule |
|---|---|
| BR-UC106-01 | Staff Leader can manage status only for GENERAL departments in their own campus. |
| BR-UC106-02 | IC department does not display toggle in UI. |
| BR-UC106-03 | Backend blocks direct status-change requests for IC department. |
| BR-UC106-04 | Disabling sets status to `INACTIVE`; it does not delete records. |
| BR-UC106-05 | Enabling sets status to `ACTIVE`; campus must be active. |
| BR-UC106-06 | Inactive departments must not be selectable for new task/resource/delegation routing. |
| BR-UC106-07 | Disable is blocked if active dependencies remain. |
| BR-UC106-08 | Do not auto-disable users when disabling a department. |
| BR-UC106-09 | Status changes must be audited with before/after values. |

## 13. Frontend implementation notes

- Use `canToggleStatus` from backend if available.
- If `canToggleStatus = false`, do not render toggle.
- Do not derive toggle visibility only from UI text/name such as `Phòng Hợp tác quốc tế`; use backend `departmentType` or `canToggleStatus`.
- Show confirmation modal only for GENERAL department action.
- After success, update the row in place or refetch list.
- On 409 blockers, show a readable modal/list of blockers.
- Do not show Department Type column.
- Do not use mock data.

## 14. Manual test cases

| # | Given | When | Then |
|---:|---|---|---|
| 1 | IC department in list | Page renders | Toggle is not visible. |
| 2 | User calls status API for IC directly | PATCH status | 409/403, status unchanged. |
| 3 | GENERAL active department with no blockers | Disable | Status becomes INACTIVE, audit saved. |
| 4 | GENERAL inactive department, campus active | Enable | Status becomes ACTIVE, audit saved. |
| 5 | GENERAL department has active users | Disable | 409 with ACTIVE_USERS blocker, status unchanged. |
| 6 | GENERAL department has open logistics/task dependency | Disable | 409 with blocker, status unchanged. |
| 7 | Department outside Staff Leader campus | PATCH status | 403, status unchanged. |
| 8 | Non-Staff Leader | PATCH status | 403, status unchanged. |
| 9 | Enable department while campus inactive | Enable | 422, status unchanged. |
| 10 | New task routing dropdown | Department inactive | Inactive department is not selectable. |

## 15. Definition of Done

- Backend endpoint runs against real DB.
- Object-level campus scope enforced.
- IC department toggle hidden in UI.
- IC direct API status change blocked.
- GENERAL active/inactive toggle works.
- Dependency blockers are checked using real schema fields only.
- No hard delete.
- No auto account disable.
- Audit log created for status changes.
- Frontend shows success and blocker errors clearly.
- Backend build passes.
- Frontend build passes if frontend changed.
