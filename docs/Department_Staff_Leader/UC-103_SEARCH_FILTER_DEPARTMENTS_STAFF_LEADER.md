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

# UC-103 — Search and Filter Departments — Staff Leader

## 1. Goal

Allow Staff Leader to search and filter departments inside their own campus.

Product Owner update:

- Remove Department Type filter.
- Keep keyword search and status filter.
- Do not show `department_type` as a list column.

## 2. Actor and authorization

Primary actor: Staff Leader.

Runtime authorization rule:

```text
currentUser.role_code = STAFF
currentUser.sub_role = LEADER
currentUser.status = ACTIVE
currentUser.primary_campus_id IS NOT NULL
```

Backend must always scope results to:

```sql
departments.campus_id = @CurrentUserPrimaryCampusId
```

Do not trust campus filter from frontend.

## 3. Preconditions

- Staff Leader is authenticated.
- Staff Leader has an active account and primary campus.
- Department Management page is accessible from the Staff Leader dashboard/menu.

## 4. Postconditions

- The list displays only departments belonging to Staff Leader's campus.
- Search and filters apply on top of campus scope.
- Pagination metadata is returned and rendered.
- No data from other campuses is returned even when keyword matches.

## 5. UI behavior

Filter bar must contain only:

| Control | Behavior |
|---|---|
| Search input | Search by department name and current head full name. |
| Status dropdown | `Tất cả`, `Hoạt động`, `Ngừng hoạt động`. |
| Reset button | Clears keyword and status filter. |
| Add button | Opens UC-101 modal. |

Do not render:

```text
Department Type filter
Campus dropdown with other campuses
```

Search placeholder:

```text
Tìm kiếm theo tên phòng, trưởng phòng
```

Status options mapping:

| UI label | API value |
|---|---|
| Tất cả | empty/null/ALL |
| Hoạt động | ACTIVE |
| Ngừng hoạt động | INACTIVE |

Empty search result message:

```text
Không tìm thấy phòng ban phù hợp với điều kiện lọc.
```

## 6. API contract

Recommended endpoint:

```http
GET /api/departments?keyword=&status=&page=1&pageSize=20&sortBy=name&sortDirection=asc
```

Do not send `departmentType` from frontend for this UC.

Query params:

| Param | Type | Required | Rule |
|---|---|---:|---|
| `keyword` | string | No | Trim. Search by department name and head full name. |
| `status` | enum | No | `ACTIVE`, `INACTIVE`, or empty/all. |
| `page` | number | No | Default 1. Must be >= 1. |
| `pageSize` | number | No | Default 20. Recommended max 100. |
| `sortBy` | enum | No | `name`, `status`, `headName`, `createdAt`. Default `name`. |
| `sortDirection` | enum | No | `asc` or `desc`. Default `asc`. |

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

`departmentType` is returned only for internal action logic such as `canToggleStatus`; it is not a visible column/filter.

## 7. Backend query logic

Base query:

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
  AND (@Status IS NULL OR d.status = @Status)
  AND (
      @Keyword IS NULL
      OR d.name LIKE CONCAT('%', @Keyword, '%')
      OR head.full_name LIKE CONCAT('%', @Keyword, '%')
  )
ORDER BY d.name ASC
LIMIT @PageSize OFFSET @Offset;
```

Count query must use the same filters and campus scope.

`canToggleStatus` calculation:

```text
canToggleStatus = department_type == GENERAL
```

Further status-change blockers are checked in UC-106, not in list/search.

## 8. Validation rules

| Param | Rule | Error |
|---|---|---|
| `page` | >= 1 | `Số trang không hợp lệ.` |
| `pageSize` | 1..100 | `Kích thước trang không hợp lệ.` |
| `status` | `ACTIVE` / `INACTIVE` / empty | `Trạng thái phòng ban không hợp lệ.` |
| `sortBy` | Allowed fields only | `Trường sắp xếp không hợp lệ.` |
| `sortDirection` | `asc` / `desc` | `Hướng sắp xếp không hợp lệ.` |

Backend must ignore/reject unsupported `departmentType` filter for this Staff Leader UI if the current API contract is being simplified.

## 9. Business rules

| Code | Rule |
|---|---|
| BR-UC103-01 | Search/filter must always respect Staff Leader campus scope. |
| BR-UC103-02 | Keyword searches department name and head full name. |
| BR-UC103-03 | Status filter supports all, active, inactive. |
| BR-UC103-04 | Department type is not a user-facing filter in this UC. |
| BR-UC103-05 | Campus is not user-selectable for Staff Leader. |
| BR-UC103-06 | IC departments may appear in results, but their status toggle is hidden via `canToggleStatus = false`. |
| BR-UC103-07 | Results must be paginated. |

## 10. Frontend implementation notes

- Use debounce for keyword input, recommended 300-500ms.
- Keep the filter bar compact.
- Do not display department type filter.
- Do not display campus dropdown unless readonly current campus label is useful.
- Preserve current page when status changes only if UX already does so; otherwise reset to page 1.
- Show loading state while fetching.
- Show empty state on no result.
- Show Vietnamese error message on API error.
- Do not use mock data.

## 11. Manual test cases

| # | Given | When | Then |
|---:|---|---|---|
| 1 | Staff Leader HN logged in | Opens Department Management | Only HN departments are returned. |
| 2 | Department in HCM has matching keyword | Staff Leader HN searches same keyword | HCM department is not returned. |
| 3 | Keyword matches department name | Search | Matching departments show. |
| 4 | Keyword matches head full name | Search | Departments with that head show. |
| 5 | Status = ACTIVE | Filter | Only active departments in current campus show. |
| 6 | Status = INACTIVE | Filter | Only inactive departments in current campus show. |
| 7 | No match | Search | Empty state shows. |
| 8 | API called by non-Staff Leader | Request list | 403 or role-appropriate rejection. |
| 9 | Frontend sends departmentType filter accidentally | Request list | Backend does not leak behavior; type filter is ignored/rejected according to final API convention. |

## 12. Definition of Done

- Search/filter API returns real DB data.
- Scope is enforced server-side.
- No Department Type filter in UI.
- No Department Type column in UI.
- Search by name/head works.
- Status filter works.
- Pagination works.
- Loading/empty/error states implemented.
- Backend build passes.
- Frontend build passes if frontend changed.
