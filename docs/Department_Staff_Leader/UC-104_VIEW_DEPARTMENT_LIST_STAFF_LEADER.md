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

# UC-104 — View Department List — Staff Leader

## 1. Goal

Allow Staff Leader to view a paginated department list for their own campus.

Product Owner update:

- Remove `Loại phòng ban` column from the visible table.
- Keep `department_type` in backend response only if needed for action visibility.
- The `IC` default department is visible in the list but does not show a status toggle.

## 2. Actor and authorization

Primary actor: Staff Leader.

Runtime authorization rule:

```text
currentUser.role_code = STAFF
currentUser.sub_role = LEADER
currentUser.status = ACTIVE
currentUser.primary_campus_id IS NOT NULL
```

Backend must query only:

```sql
departments.campus_id = @CurrentUserPrimaryCampusId
```

## 3. Preconditions

- Staff Leader is logged in.
- Staff Leader belongs to one active campus.
- Departments table has zero or more rows for that campus.

## 4. Postconditions

- Department Management page displays paginated department list for current campus.
- Each row shows department name, current head, status, and available actions.
- IC row does not show enable/disable toggle.
- GENERAL rows show toggle if user has action permission and no UI-level restriction applies.

## 5. UI layout

Page title:

```text
Quản lý phòng ban
```

Breadcrumb:

```text
Dashboard / Quản lý phòng ban
```

Toolbar:

```text
[Search: Tìm kiếm theo tên phòng, trưởng phòng]
[Status: Tất cả / Hoạt động / Ngừng hoạt động]
[Reset]
[+ Thêm phòng ban mới]
```

Desktop table columns:

| Column | Notes |
|---|---|
| STT | Row number based on page offset. |
| Tên phòng ban | Department name. Include campus label only if useful; do not add type label as a column. |
| Trưởng phòng | Head name or `Chưa gán trưởng phòng`. |
| Trạng thái | Badge: `Hoạt động` / `Ngừng hoạt động`. |
| Hành động | Toggle for GENERAL departments only. IC departments show no toggle. |

Do not display:

```text
Loại phòng ban column
Department Type filter
Campus selector with other campuses
```

Recommended IC action cell display:

```text
Phòng mặc định
```

or leave empty with disabled/hidden action. If text is used, keep it subtle.

Status badge mapping:

| DB status | UI label | Suggested style |
|---|---|---|
| ACTIVE | Hoạt động | green/success badge |
| INACTIVE | Ngừng hoạt động | gray/slate badge |

Head display mapping:

| DB value | UI |
|---|---|
| `head_user_id` has user | `head.full_name` |
| `head_user_id = NULL` | `Chưa gán trưởng phòng` |

## 6. API contract

Recommended endpoint:

```http
GET /api/departments?page=1&pageSize=20&status=&keyword=&sortBy=name&sortDirection=asc
```

Response example:

```json
{
  "items": [
    {
      "departmentId": 1,
      "campusId": 1,
      "campusName": "FPT University Hà Nội",
      "name": "Phòng Hợp tác quốc tế",
      "headUserId": 3,
      "headFullName": "Nguyễn Văn A",
      "status": "ACTIVE",
      "departmentType": "IC",
      "canToggleStatus": false,
      "createdAt": "2026-06-01T08:00:00",
      "updatedAt": null
    },
    {
      "departmentId": 10,
      "campusId": 1,
      "campusName": "FPT University Hà Nội",
      "name": "Phòng Công nghệ thông tin",
      "headUserId": null,
      "headFullName": null,
      "status": "ACTIVE",
      "departmentType": "GENERAL",
      "canToggleStatus": true,
      "createdAt": "2026-06-20T08:00:00",
      "updatedAt": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 2,
  "totalPages": 1
}
```

The frontend should not render `departmentType` as a visible column. It can use `canToggleStatus` to decide whether to render the toggle.

## 7. Backend flow

1. Resolve current user.
2. Verify Staff Leader role.
3. Resolve `currentUser.primary_campus_id`.
4. Query `departments` for the current campus only.
5. Left join `users` for `head_user_id`.
6. Apply status/keyword filters if the same endpoint supports UC-103.
7. Sort and paginate.
8. Map each row to DTO.
9. Compute `canToggleStatus`:

```text
false if department_type = IC
true if department_type = GENERAL
```

10. Return response.

## 8. SQL query reference

```sql
SELECT
    d.department_id,
    d.campus_id,
    c.name AS campus_name,
    d.name AS department_name,
    d.department_type,
    d.head_user_id,
    head.full_name AS head_full_name,
    d.status,
    d.created_at,
    d.updated_at
FROM departments d
JOIN campuses c ON c.campus_id = d.campus_id
LEFT JOIN users head ON head.user_id = d.head_user_id
WHERE d.campus_id = @CurrentUserPrimaryCampusId
ORDER BY d.name ASC
LIMIT @PageSize OFFSET @Offset;
```

## 9. Alternative flows

### AF-01 — No departments in current campus

System displays:

```text
Chưa có phòng ban nào trong cơ sở này.
```

The `+ Thêm phòng ban mới` button remains visible.

### AF-02 — Search/filter returns no result

System displays:

```text
Không tìm thấy phòng ban phù hợp với điều kiện lọc.
```

Search/filter controls remain visible.

### AF-03 — Staff Leader has no campus

Backend returns 422 or 403 depending project convention. UI displays:

```text
Tài khoản chưa được gán cơ sở nên không thể xem danh sách phòng ban.
```

### AF-04 — Unauthorized actor direct API call

Backend returns 403. UI should not show this menu for unauthorized roles, but backend must still enforce it.

## 10. Business rules

| Code | Rule |
|---|---|
| BR-UC104-01 | Staff Leader sees only departments in their own campus. |
| BR-UC104-02 | Department table does not show Department Type column. |
| BR-UC104-03 | IC department is visible but has no toggle action. |
| BR-UC104-04 | GENERAL department can show toggle action through `canToggleStatus = true`. |
| BR-UC104-05 | Department with no head must display `Chưa gán trưởng phòng`, not empty/null. |
| BR-UC104-06 | List must support pagination. |
| BR-UC104-07 | Backend response must not expose sensitive user fields of department head. |

## 11. Frontend implementation notes

- Use enterprise dashboard style already used in PEMS.
- No horizontal overflow on desktop/tablet/mobile.
- Desktop: table/grid.
- Mobile/tablet: card list if current UI supports responsive alternative.
- Do not refactor unrelated page logic.
- Do not change role/routing logic beyond hiding/showing menu/action as needed.
- Use real API data, no mock fallback.
- Every icon-only button must have title/aria-label.

## 12. Manual test cases

| # | Given | When | Then |
|---:|---|---|---|
| 1 | Staff Leader HN logged in | Opens page | Only HN departments show. |
| 2 | IC department exists | Page renders | IC row has no toggle; action cell shows empty/default label. |
| 3 | GENERAL department exists | Page renders | GENERAL row has toggle action. |
| 4 | Department has no head | Page renders | `Chưa gán trưởng phòng` is displayed. |
| 5 | Department has head user | Page renders | Head full name is displayed. |
| 6 | No department in campus | Page renders | Empty state shown, add button visible. |
| 7 | Non-Staff Leader calls endpoint | Direct API | 403. |
| 8 | HCM department exists | HN Staff Leader opens page | HCM department not returned. |

## 13. Definition of Done

- Department list API returns real DB data.
- Server-side campus scope enforced.
- Department Type column removed from UI.
- Department Type filter removed from UI.
- IC row has no status toggle.
- GENERAL row can show status toggle.
- Head display is correct for null/non-null head.
- Loading/empty/error states implemented.
- Backend build passes.
- Frontend build passes if frontend changed.
