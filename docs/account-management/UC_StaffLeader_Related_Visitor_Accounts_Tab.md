> [!WARNING]
> **LEGACY ARCHITECTURE NOTE (Campus-independent Approval Update)**
> This document has been updated to reflect the new Campus-independent Approval architecture.
> - **HO is now monitor/read-only.** There is no centralized multi-campus approval by HO.
> - **Staff Leader approval is per-campus.** Each Staff Leader directly receives and approves/rejects their own campus instance right after submission.
> - **Self-hosting is supported.** Staff Leaders can assign themselves as the host during approval.
> - **ASSIGNED is removed.** Approving a request now requires assigning a host immediately.
> - **New statuses:** `PARTIALLY_APPROVED` (request level) and `REJECTED` (campus level) are added. 
> - **Cancel logic:** Visitors can cancel requests in `PENDING_APPROVAL` or `PARTIALLY_APPROVED` states.
> - **Transportation:** `transportation_note` and `transportation_note` are replaced by `transportation_note`.
> Please refer to the latest codebase and SQL schema for the current implementation.

# UC — Tab “Visitor liên quan” trong trang quản lý account của STAFF LEADER

> **Mục đích file:** Tài liệu này dùng cho AI Agent đọc và code chức năng hiển thị danh sách account `VISITOR` liên quan đến campus của `STAFF LEADER` trong trang **Account Management**.  
> **Phạm vi:** Chỉ thêm tab riêng **Visitor liên quan** cho Staff Leader. Chỉ cho xem danh sách và xem chi tiết. Không cho Staff Leader quản trị vòng đời account Visitor.

---

## 1. Tài liệu bắt buộc phải đọc trước khi code

AI Agent phải đọc và đối chiếu các file sau trước khi sửa code:

```text
DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT_v8_4_refined_v6_v10_FULL_UPDATED.md
VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
PROJECT_STRUCTURE_FULL.md
```

Nếu cần xác nhận schema thật, đọc SQL fresh-create mới nhất:

```text
pems_full_create_manual_wide_coverage_seed_v8_4_refined_v6_v10_clean_logistics_handover_fields.sql
```

Không được tự bịa field, enum, role, permission table, hoặc status ngoài schema hiện tại.

---

## 2. Bối cảnh nghiệp vụ

Hiện tại Staff Leader quản lý account nội bộ trong phạm vi campus của mình, gồm:

```text
STAFF + STAFF
STAFF + LEADER - chính mình
DEPARTMENT + LEADER
STUDENT
```

Cần bổ sung một tab riêng trong trang Account Management của Staff Leader:

```text
Visitor liên quan
```

Tab này hiển thị các account `VISITOR` có liên quan đến campus của Staff Leader thông qua đơn tham quan. Không hiển thị toàn bộ Visitor toàn hệ thống.

---

## 3. Nguyên tắc quan trọng nhất

`VISITOR` không thuộc campus nào. Vì vậy tuyệt đối không lọc Visitor bằng:

```sql
visitor.primary_campus_id = currentStaffLeader.primary_campus_id
```

Cách đúng là xác định Visitor liên quan qua chuỗi quan hệ:

```text
users VISITOR
→ visit_requests.visitor_user_id
→ visit_request_campuses.visit_request_id
→ visit_request_campuses.campus_id = currentStaffLeader.primary_campus_id
```

Predicate cốt lõi:

```sql
vr.visitor_user_id = visitor.user_id
AND vrc.campus_id = currentStaffLeader.primary_campus_id
```

---

## 4. Actor và điều kiện truy cập

### 4.1 Actor hợp lệ

Chỉ `STAFF LEADER` được dùng tab này.

Điều kiện current user:

```text
role_code = STAFF
sub_role = LEADER
status = ACTIVE
primary_campus_id IS NOT NULL
```

### 4.2 Actor không được truy cập

Các actor sau không được truy cập tab/API này:

```text
ADMIN
HO
STAFF + STAFF
DEPARTMENT + LEADER
DEPARTMENT + STAFF
STUDENT
VISITOR
Unauthenticated user
```

Nếu gọi trực tiếp API thì backend trả:

```text
403 FORBIDDEN
```

### 4.3 Không tin dữ liệu campus từ frontend

Frontend không được quyết định campus scope.

Không nhận hoặc không tin các query kiểu:

```http
campusId=...
scope=ALL_VISITORS
```

Backend luôn lấy campus từ user đang đăng nhập:

```text
currentUser.primary_campus_id
```

---

## 5. Định nghĩa “Visitor liên quan đến campus của Staff Leader”

Một Visitor được xem là liên quan nếu có ít nhất một `visit_request` thỏa điều kiện theo từng loại đơn.

---

### 5.1 Đơn single-campus

Với đơn `SINGLE_CAMPUS`, Staff Leader được thấy Visitor nếu đơn có campus instance thuộc campus của mình.

Điều kiện:

```text
visit_requests.visit_scope = SINGLE_CAMPUS
visit_request_campuses.campus_id = currentStaffLeader.primary_campus_id
visit_requests.visitor_user_id = visitor.user_id
```

Cho phép hiển thị Visitor dù request đang ở các trạng thái:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

Lý do: đây là đơn single-campus gửi trực tiếp tới campus của Staff Leader, nên quan hệ Visitor ↔ campus đã tồn tại thật, kể cả đơn bị từ chối/hủy.

Ví dụ:

```text
Visitor A gửi đơn SINGLE_CAMPUS tới HN.
Staff Leader HN thấy Visitor A.
Staff Leader HCM không thấy Visitor A.
```

---

### 5.2 Đơn multi-campus

Với đơn `MULTI_CAMPUS`, Staff Leader của các campus liên quan chỉ được thấy Visitor sau khi HO đã duyệt/release đơn.

#### 5.2.1 Multi-campus đang chờ HO duyệt

Điều kiện request:

```text
visit_requests.visit_scope = MULTI_CAMPUS
visit_requests.status = PENDING_APPROVAL
```

Kết quả:

```text
Staff Leader các campus liên quan KHÔNG thấy Visitor.
```

Lý do: multi-campus pending chỉ HO thấy và xử lý request tổng. Campus con chưa được release dữ liệu.

#### 5.2.2 Multi-campus bị HO từ chối

Điều kiện request:

```text
visit_requests.visit_scope = MULTI_CAMPUS
visit_requests.status = REJECTED
```

Kết quả:

```text
Staff Leader các campus liên quan KHÔNG thấy Visitor.
```

Lý do: HO đã từ chối, đơn không được release xuống các campus.

#### 5.2.3 Multi-campus đã được HO duyệt

Điều kiện request:

```text
visit_requests.visit_scope = MULTI_CAMPUS
visit_requests.status = APPROVED
visit_request_campuses.campus_id = currentStaffLeader.primary_campus_id
visit_request_campuses.status <> WAITING_REQUEST_APPROVAL
```

Kết quả:

```text
Staff Leader của từng campus trong đơn được thấy Visitor.
Staff Leader campus không nằm trong đơn không thấy Visitor.
```

Ví dụ:

```text
Visitor C gửi đơn MULTI_CAMPUS tới HN + HCM.
HO chưa duyệt → Staff Leader HN/HCM chưa thấy Visitor C.
HO từ chối → Staff Leader HN/HCM không thấy Visitor C.
HO duyệt → Staff Leader HN thấy Visitor C, Staff Leader HCM thấy Visitor C, Staff Leader DN không thấy.
```

#### 5.2.4 Multi-campus đã từng được duyệt rồi sau đó bị hủy

Nếu request đã từng được HO duyệt/release, sau đó chuyển `CANCELLED`, có thể giữ Visitor trong tab như dữ liệu lịch sử read-only.

Điều kiện khuyến nghị:

```text
visit_requests.visit_scope = MULTI_CAMPUS
visit_requests.status = CANCELLED
visit_request_campuses.campus_id = currentStaffLeader.primary_campus_id
visit_request_campuses.status <> WAITING_REQUEST_APPROVAL
```

Không được hiển thị case `REJECTED` vì rejected nghĩa là không release xuống campus.

---

## 6. Main Flow

```text
[U] Step 1. Staff Leader đăng nhập Internal Portal.

[U] Step 2. Staff Leader mở trang Account Management.

[S] Step 3. Frontend render tab account nội bộ như hiện tại và thêm tab mới: “Visitor liên quan”.

[U] Step 4. Staff Leader click tab “Visitor liên quan”.

[S] Step 5. Frontend gọi API lấy danh sách Visitor liên quan.

[S] Step 6. Backend resolve current user từ token/session:
    - role_code = STAFF
    - sub_role = LEADER
    - status = ACTIVE
    - primary_campus_id != NULL

[S] Step 7. Backend query Visitor bằng relation:
    users VISITOR
    → visit_requests.visitor_user_id
    → visit_request_campuses.campus_id = currentStaffLeader.primary_campus_id

[S] Step 8. Backend áp dụng visibility:
    - SINGLE_CAMPUS: cùng campus Staff Leader.
    - MULTI_CAMPUS: chỉ khi HO đã duyệt/release, không lấy pending/rejected.

[S] Step 9. Backend áp dụng keyword/search/filter/paging/sort.

[S] Step 10. Backend trả danh sách Visitor read-only.

[S] Step 11. Frontend hiển thị bảng Visitor liên quan.

[U] Step 12. Staff Leader bấm “View detail”.

[S] Step 13. Backend check lại scope detail bằng EXISTS relation qua visit_request_campuses.

[S] Step 14. Nếu hợp lệ, backend trả detail Visitor. Nếu không hợp lệ, trả 403 hoặc 404.
```

---

## 7. Alternative Flows

### AF-01 — Không có Visitor liên quan

API trả:

```json
{
  "items": [],
  "totalCount": 0
}
```

Frontend hiển thị empty state:

```text
Chưa có tài khoản khách nào liên quan đến cơ sở của bạn.
```

---

### AF-02 — Staff Leader gọi detail Visitor ngoài scope

Ví dụ Staff Leader HN biết `userId` của Visitor chỉ liên quan HCM.

Backend phải chặn:

```text
403 VISITOR_SCOPE_FORBIDDEN
```

Hoặc dùng:

```text
404 NOT_FOUND
```

Nếu muốn che sự tồn tại của account.

---

### AF-03 — Multi-campus pending HO

Không hiển thị Visitor.

```text
visit_scope = MULTI_CAMPUS
status = PENDING_APPROVAL
```

---

### AF-04 — Multi-campus bị HO từ chối

Không hiển thị Visitor.

```text
visit_scope = MULTI_CAMPUS
status = REJECTED
```

---

### AF-05 — Request không có visitor_user_id

Nếu `visit_requests.visitor_user_id IS NULL` thì không đưa lên tab Account Management.

Lý do: không có account Visitor thật để hiển thị. Nếu cần xem thông tin người đăng ký không có account thì xem trong Visit/Delegation Management, không phải Account Management.

---

### AF-06 — Frontend gửi campusId khác campus của Staff Leader

Backend không dùng campusId đó. Nếu API có nhận campusId thì phải ignore hoặc trả 403 nếu phát hiện cố tình vượt quyền.

---

## 8. Business Rules

### BR-01 — Không dump toàn bộ Visitor toàn hệ thống

Staff Leader không được xem toàn bộ account Visitor toàn hệ thống.

Không được query kiểu:

```sql
SELECT *
FROM users u
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'VISITOR';
```

---

### BR-02 — Visitor không có campus

Visitor hợp lệ phải thỏa:

```text
role_code = VISITOR
sub_role IS NULL
primary_campus_id IS NULL
department_id IS NULL
```

---

### BR-03 — List và detail đều phải check scope

Không chỉ check scope ở API list.

Khi xem detail, backend phải kiểm tra lại Visitor đó có ít nhất một request/campus instance visible với Staff Leader hiện tại.

---

### BR-04 — Chỉ read-only

Staff Leader chỉ được:

```text
View list
Search/filter trong tab Visitor liên quan
View detail
```

Staff Leader không được:

```text
Create Visitor
Update Visitor profile
Disable / Enable Visitor
Lock / Unlock Visitor
Reset password Visitor
Update role Visitor
Change campus Visitor
Change department Visitor
Delete Visitor
```

Nếu frontend ẩn nút nhưng user gọi API trực tiếp, backend vẫn phải trả 403.

---

### BR-05 — Không trả dữ liệu nhạy cảm

DTO list/detail không trả các trường nhạy cảm:

```text
password_hash
password_salt
refresh_token_hash
provider_subject
provider_uid
security_stamp
otp_token
reset_token
failed_login_count, nếu không thật sự cần hiển thị
locked_until, nếu không thật sự cần hiển thị
```

---

### BR-06 — Không đổi luồng account nội bộ hiện có

Tab “Visitor liên quan” là tab bổ sung. Không được phá các luồng account nội bộ hiện tại của Staff Leader.

---

## 9. API đề xuất

Ưu tiên tạo endpoint riêng để không làm rối list account nội bộ hiện tại:

```http
GET /api/accounts/staff-leader/related-visitors?page=1&pageSize=20&keyword=&status=&sortBy=lastRelatedRequestAt&sortDirection=desc
```

Hoặc nếu project hiện tại đã có Account List API chung, có thể dùng:

```http
GET /api/accounts?accountGroup=RELATED_VISITORS&page=1&pageSize=20&keyword=&status=&sortBy=lastRelatedRequestAt&sortDirection=desc
```

Nhưng backend phải tự suy ra scope từ current user. Không nhận `campusId` từ frontend để lọc dữ liệu.

### Detail endpoint

Có thể dùng chung endpoint account detail hiện tại:

```http
GET /api/accounts/{visitorUserId}
```

Hoặc endpoint riêng:

```http
GET /api/accounts/staff-leader/related-visitors/{visitorUserId}
```

Dù dùng endpoint nào, backend bắt buộc check lại scope relation.

---

## 10. Request query params

```text
page: number, default 1, min 1
pageSize: number, default 20, max 100
keyword: optional string, trim, search full_name/email/phone/nationality/request_code/delegation_name nếu cần
status: optional ACTIVE/INACTIVE/LOCKED
sortBy: optional, default lastRelatedRequestAt
sortDirection: asc/desc, default desc
```

Không cho sort/filter bằng field nhạy cảm.

---

## 11. Response DTO đề xuất

### 11.1 List item DTO

```csharp
public sealed class RelatedVisitorAccountListItemDto
{
    public long UserId { get; init; }
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string? Phone { get; init; }
    public string? Nationality { get; init; }

    public string RoleCode { get; init; } = "VISITOR";
    public string Status { get; init; } = default!;
    public string? CreatedVia { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }

    public int RelatedRequestCount { get; init; }
    public DateTime? LastRelatedRequestAt { get; init; }
    public DateTime? LatestPlannedStartAt { get; init; }

    public bool CanViewDetails { get; init; } = true;
    public bool CanManageStatus { get; init; } = false;
    public bool CanUpdateRole { get; init; } = false;
    public bool CanResetPassword { get; init; } = false;
}
```

### 11.2 Detail DTO gợi ý

Detail có thể dùng DTO account detail hiện tại nhưng phải giới hạn trường. Nên trả thêm phần context liên quan campus:

```csharp
public sealed class RelatedVisitorAccountDetailDto
{
    public long UserId { get; init; }
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string? Phone { get; init; }
    public string? Nationality { get; init; }
    public string? Gender { get; init; }
    public string Status { get; init; } = default!;
    public string CreatedVia { get; init; } = default!;
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }

    public IReadOnlyList<RelatedVisitorRequestDto> RelatedRequests { get; init; } = [];

    public bool CanManageStatus { get; init; } = false;
    public bool CanUpdateRole { get; init; } = false;
    public bool CanResetPassword { get; init; } = false;
}

public sealed class RelatedVisitorRequestDto
{
    public long VisitRequestId { get; init; }
    public long VisitInstanceId { get; init; }
    public string RequestCode { get; init; } = default!;
    public string DelegationName { get; init; } = default!;
    public string VisitScope { get; init; } = default!;
    public string RequestStatus { get; init; } = default!;
    public string CampusInstanceStatus { get; init; } = default!;
    public DateTime PlannedStartAt { get; init; }
    public DateTime PlannedEndAt { get; init; }
}
```

---

## 12. SQL tham khảo — List Visitor liên quan

> Lưu ý: Đây là SQL tham khảo. Khi code bằng EF/LINQ, phải giữ nguyên logic scope.

```sql
-- Input:
-- @CurrentUserId
-- @Keyword
-- @Status
-- @PageSize
-- @Offset

WITH current_staff_leader AS (
    SELECT
        u.user_id,
        u.primary_campus_id
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = @CurrentUserId
      AND r.role_code = 'STAFF'
      AND u.sub_role = 'LEADER'
      AND u.status = 'ACTIVE'
      AND u.primary_campus_id IS NOT NULL
),
visible_visitor_requests AS (
    SELECT
        vr.visitor_user_id,
        COUNT(DISTINCT vr.visit_request_id) AS related_request_count,
        MAX(vr.submitted_at) AS last_related_request_at,
        MAX(vrc.planned_start_at) AS latest_planned_start_at
    FROM current_staff_leader csl
    JOIN visit_request_campuses vrc
        ON vrc.campus_id = csl.primary_campus_id
    JOIN visit_requests vr
        ON vr.visit_request_id = vrc.visit_request_id
    WHERE vr.visitor_user_id IS NOT NULL
      AND (
            -- SINGLE_CAMPUS: cùng campus thì Staff Leader thấy
            vr.visit_scope = 'SINGLE_CAMPUS'

            OR

            -- MULTI_CAMPUS: chỉ sau khi HO approve/release.
            -- REJECTED/PENDING_APPROVAL không được thấy.
            (
                vr.visit_scope = 'MULTI_CAMPUS'
                AND vr.status IN ('APPROVED', 'CANCELLED')
                AND vrc.status <> 'WAITING_REQUEST_APPROVAL'
            )
      )
    GROUP BY vr.visitor_user_id
)
SELECT
    u.user_id,
    u.full_name,
    u.email,
    u.phone,
    u.nationality,
    u.status,
    u.created_via,
    u.created_at,
    u.last_login_at,
    vvr.related_request_count,
    vvr.last_related_request_at,
    vvr.latest_planned_start_at
FROM visible_visitor_requests vvr
JOIN users u
    ON u.user_id = vvr.visitor_user_id
JOIN roles r
    ON r.role_id = u.role_id
WHERE r.role_code = 'VISITOR'
  AND u.primary_campus_id IS NULL
  AND u.department_id IS NULL
  AND u.sub_role IS NULL
  AND (@Status IS NULL OR @Status = '' OR u.status = @Status)
  AND (
        @Keyword IS NULL
        OR @Keyword = ''
        OR u.full_name LIKE CONCAT('%', @Keyword, '%')
        OR u.email LIKE CONCAT('%', @Keyword, '%')
        OR u.phone LIKE CONCAT('%', @Keyword, '%')
        OR u.nationality LIKE CONCAT('%', @Keyword, '%')
      )
ORDER BY vvr.last_related_request_at DESC, u.created_at DESC
LIMIT @PageSize OFFSET @Offset;
```

---

## 13. SQL tham khảo — Count để phân trang

```sql
WITH current_staff_leader AS (
    SELECT
        u.user_id,
        u.primary_campus_id
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = @CurrentUserId
      AND r.role_code = 'STAFF'
      AND u.sub_role = 'LEADER'
      AND u.status = 'ACTIVE'
      AND u.primary_campus_id IS NOT NULL
),
visible_visitors AS (
    SELECT DISTINCT vr.visitor_user_id
    FROM current_staff_leader csl
    JOIN visit_request_campuses vrc
        ON vrc.campus_id = csl.primary_campus_id
    JOIN visit_requests vr
        ON vr.visit_request_id = vrc.visit_request_id
    WHERE vr.visitor_user_id IS NOT NULL
      AND (
            vr.visit_scope = 'SINGLE_CAMPUS'
            OR (
                vr.visit_scope = 'MULTI_CAMPUS'
                AND vr.status IN ('APPROVED', 'CANCELLED')
                AND vrc.status <> 'WAITING_REQUEST_APPROVAL'
            )
      )
)
SELECT COUNT(*) AS total_count
FROM visible_visitors vv
JOIN users u ON u.user_id = vv.visitor_user_id
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code = 'VISITOR'
  AND u.primary_campus_id IS NULL
  AND u.department_id IS NULL
  AND u.sub_role IS NULL
  AND (@Status IS NULL OR @Status = '' OR u.status = @Status)
  AND (
        @Keyword IS NULL
        OR @Keyword = ''
        OR u.full_name LIKE CONCAT('%', @Keyword, '%')
        OR u.email LIKE CONCAT('%', @Keyword, '%')
        OR u.phone LIKE CONCAT('%', @Keyword, '%')
        OR u.nationality LIKE CONCAT('%', @Keyword, '%')
      );
```

---

## 14. SQL tham khảo — Check scope khi xem detail

```sql
SELECT 1
FROM users visitor
JOIN roles visitor_role
    ON visitor_role.role_id = visitor.role_id
WHERE visitor.user_id = @VisitorUserId
  AND visitor_role.role_code = 'VISITOR'
  AND visitor.primary_campus_id IS NULL
  AND visitor.department_id IS NULL
  AND visitor.sub_role IS NULL
  AND EXISTS (
      SELECT 1
      FROM users current_user
      JOIN roles current_role
          ON current_role.role_id = current_user.role_id
      JOIN visit_request_campuses vrc
          ON vrc.campus_id = current_user.primary_campus_id
      JOIN visit_requests vr
          ON vr.visit_request_id = vrc.visit_request_id
      WHERE current_user.user_id = @CurrentUserId
        AND current_role.role_code = 'STAFF'
        AND current_user.sub_role = 'LEADER'
        AND current_user.status = 'ACTIVE'
        AND vr.visitor_user_id = visitor.user_id
        AND (
              vr.visit_scope = 'SINGLE_CAMPUS'
              OR (
                  vr.visit_scope = 'MULTI_CAMPUS'
                  AND vr.status IN ('APPROVED', 'CANCELLED')
                  AND vrc.status <> 'WAITING_REQUEST_APPROVAL'
              )
            )
  );
```

Nếu không có row:

```text
403 VISITOR_SCOPE_FORBIDDEN
```

hoặc:

```text
404 NOT_FOUND
```

Chọn một policy thống nhất với project.

---

## 15. Backend implementation checklist

### 15.1 Không được làm

```text
Không query toàn bộ VISITOR.
Không dùng visitor.primary_campus_id để lọc.
Không cho Staff Leader update/delete/disable Visitor.
Không tin campusId từ frontend.
Không để Controller chứa business logic phức tạp.
Không trả field nhạy cảm.
Không sửa dynamic permissions vì v10 không dùng permissions/role_permissions.
```

### 15.2 Nên làm

```text
[ ] Thêm query/handler riêng cho Related Visitor Accounts, hoặc branch rõ trong AccountListQueryExecutor.
[ ] Resolve current user và validate STAFF + LEADER + ACTIVE.
[ ] Lấy currentUser.primary_campus_id từ CurrentUser service/session.
[ ] Query Visitor qua visit_requests + visit_request_campuses.
[ ] Áp dụng single-campus/multi-campus visibility rule.
[ ] Thêm paging/filter/sort.
[ ] Mapping DTO read-only.
[ ] View detail check lại EXISTS scope.
[ ] Viết test/manual verification cho pending/rejected/approved multi-campus.
[ ] Build backend.
```

### 15.3 File backend có thể liên quan

Dựa trên cấu trúc project hiện tại, AI Agent cần tự quét repo trước khi sửa. Các file/khu vực có thể liên quan:

```text
backend/PEMS.Api/Controllers/AccountsController.cs
backend/PEMS.Application/Accounts/Queries/ViewAccountList/
backend/PEMS.Application/Accounts/Queries/SearchandFilterAccounts/
backend/PEMS.Application/Accounts/Queries/ViewAccountDetails/
backend/PEMS.Application/Accounts/Common/AccountListQueryExecutor.cs
backend/PEMS.Application/Accounts/Common/AccountListCriteriaRules.cs
backend/PEMS.Application/Accounts/Common/AccountListItemDto.cs
backend/PEMS.Application/Common/Interfaces/IApplicationDbContext.cs
backend/PEMS.Domain/Constants hoặc Enums liên quan RoleCodes/SubRoles/Statuses
backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs
```

Không sửa những file này theo tên cứng nếu repo thực tế khác. Phải search code trước.

---

## 16. Frontend implementation checklist

### 16.1 UI behavior

Trong trang Account Management của Staff Leader, thêm tab:

```text
Tài khoản nội bộ
Visitor liên quan
```

Hoặc nếu UI đang có tab/filter sẵn, thêm tab riêng:

```text
Visitor liên quan
```

Tab này chỉ hiện với:

```text
role_code = STAFF
sub_role = LEADER
```

Không hiện cho role khác.

### 16.2 Cột bảng đề xuất

```text
Họ tên
Email
Số điện thoại
Quốc tịch
Trạng thái account
Nguồn tạo account
Số đơn liên quan
Lần gửi đơn gần nhất
Ngày visit gần nhất
Hành động: Xem chi tiết
```

### 16.3 Action được phép

Chỉ hiển thị:

```text
View detail
```

Không hiển thị:

```text
Edit
Disable
Enable
Lock
Unlock
Reset Password
Update Role
Delete
Change Campus
Change Department
```

### 16.4 Empty state

```text
Chưa có tài khoản khách nào liên quan đến cơ sở của bạn.
```

### 16.5 Loading/error state

Giữ đúng design system hiện tại. Không refactor UI lớn nếu không cần.

### 16.6 File frontend có thể liên quan

AI Agent phải tự quét repo. Các khu vực có thể liên quan:

```text
frontend/pems-react/src/features/accounts/
frontend/pems-react/src/pages/
frontend/pems-react/src/shared/
frontend/pems-react/src/features/accounts/api/
frontend/pems-react/src/features/accounts/types/
frontend/pems-react/src/features/accounts/components/
```

---

## 17. Acceptance Criteria

### AC-01 — Staff Leader thấy Visitor single-campus của campus mình

```text
Given Staff Leader HN đang đăng nhập
And Visitor A có SINGLE_CAMPUS request tới HN
When Staff Leader HN mở Account Management → tab Visitor liên quan
Then Visitor A xuất hiện trong danh sách
And row chỉ có action View detail
```

### AC-02 — Staff Leader không thấy Visitor của campus khác

```text
Given Staff Leader HN đang đăng nhập
And Visitor B chỉ có request tới HCM
When Staff Leader HN mở tab Visitor liên quan
Then Visitor B không xuất hiện
```

### AC-03 — Multi-campus pending HO không hiển thị

```text
Given Visitor C gửi MULTI_CAMPUS request tới HN + HCM
And request đang PENDING_APPROVAL
When Staff Leader HN mở tab Visitor liên quan
Then Visitor C không xuất hiện
```

### AC-04 — Multi-campus bị HO từ chối không hiển thị

```text
Given Visitor D gửi MULTI_CAMPUS request tới HN + HCM
And HO đã reject request, status = REJECTED
When Staff Leader HN mở tab Visitor liên quan
Then Visitor D không xuất hiện
```

### AC-05 — Multi-campus sau HO approve mới hiển thị

```text
Given Visitor C gửi MULTI_CAMPUS request tới HN + HCM
And HO đã approve request, status = APPROVED
When Staff Leader HN mở tab Visitor liên quan
Then Visitor C xuất hiện
And Staff Leader HCM cũng thấy Visitor C
And Staff Leader DN không thấy nếu DN không nằm trong request
```

### AC-06 — Detail ngoài scope bị chặn

```text
Given Staff Leader HN biết userId của Visitor E
And Visitor E không có request nào liên quan HN
When Staff Leader HN gọi GET detail Visitor E
Then backend trả 403 hoặc 404
```

### AC-07 — Không có action quản trị Visitor

```text
Given Visitor A xuất hiện trong tab Visitor liên quan
When Staff Leader xem row hoặc detail Visitor A
Then UI không hiển thị Disable/Enable/Lock/Unlock/Reset Password/Update Role/Delete
And nếu gọi API thao tác trực tiếp thì backend trả 403
```

### AC-08 — Visitor không có account thật thì không hiển thị

```text
Given visit_request có registrant info nhưng visitor_user_id IS NULL
When Staff Leader mở tab Visitor liên quan
Then request này không tạo row account Visitor trong tab
```

---

## 18. Manual test cases

```text
[ ] Staff Leader HN mở tab Visitor liên quan: chỉ thấy Visitor có request liên quan HN.
[ ] Staff Leader HN không thấy Visitor chỉ liên quan HCM.
[ ] Multi-campus PENDING_APPROVAL: Staff Leader campus con không thấy Visitor.
[ ] Multi-campus REJECTED: Staff Leader campus con không thấy Visitor.
[ ] Multi-campus APPROVED: Staff Leader campus con trong request thấy Visitor.
[ ] Staff Leader campus không nằm trong multi-campus request không thấy Visitor.
[ ] View detail Visitor trong scope: success.
[ ] View detail Visitor ngoài scope: 403/404.
[ ] UI tab Visitor liên quan chỉ có View detail.
[ ] Gọi API disable/update role/reset password Visitor bằng Staff Leader: 403.
[ ] Backend/frontend build pass.
```

---

## 19. Kết luận triển khai

Triển khai chức năng này theo hướng:

```text
Thêm tab “Visitor liên quan” trong Account Management của Staff Leader.
Danh sách Visitor được lấy bằng quan hệ visit_requests + visit_request_campuses.
Staff Leader chỉ thấy Visitor có request liên quan đến campus của mình.
Multi-campus chỉ hiển thị sau khi HO approve/release; pending hoặc rejected không hiển thị.
Tab Visitor liên quan chỉ read-only: list + detail, không có action quản trị account.
```

Không triển khai theo hướng:

```text
Staff Leader xem toàn bộ Visitor toàn hệ thống.
Staff Leader quản lý trạng thái/role/password của Visitor.
Lọc Visitor bằng primary_campus_id.
Tin campusId do frontend gửi lên.
```
