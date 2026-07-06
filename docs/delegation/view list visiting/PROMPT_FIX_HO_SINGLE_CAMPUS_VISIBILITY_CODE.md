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

# PROMPT FIX CODE — HO XEM SINGLE_CAMPUS READ-ONLY TRONG DANH SÁCH ĐƠN TIẾP KHÁCH

## 0. Bối cảnh

Dự án PEMS đã chốt rule nghiệp vụ mới:

```text
HO được xem đơn SINGLE_CAMPUS để theo dõi ở chế độ read-only.
HO không được duyệt, từ chối, gán host, chuyển host, hủy hoặc xử lý nghiệp vụ trên SINGLE_CAMPUS.
HO chỉ được approve/reject MULTI_CAMPUS khi request đang PENDING_APPROVAL.
```

Hiện tại màn HO vẫn chỉ thấy đơn liên cơ sở. Nguyên nhân có thể là:

```text
1. Backend query dành cho HO vẫn filter visit_scope = MULTI_CAMPUS.
2. Backend đang dùng SQL view vw_visit_requests_for_ho cũ, view này filter WHERE vr.visit_scope = 'MULTI_CAMPUS'.
3. Frontend vẫn tự filter danh sách HO chỉ còn MULTI_CAMPUS.
4. Backend đã sửa nhưng chưa build/restart đúng API.
```

Nhiệm vụ của bạn là sửa code để HO nhìn thấy cả `MULTI_CAMPUS` và `SINGLE_CAMPUS`, trong đó `SINGLE_CAMPUS` luôn là read-only.

---

## 1. Vai trò của bạn

Bạn là:

```text
Senior .NET Clean Architecture Developer
Senior React TypeScript Engineer
RBAC/Scope Reviewer
Database-first MySQL Reviewer
```

Không được sửa bừa. Phải quét code hiện tại trước khi sửa.

---

## 2. Nguồn chuẩn cần kiểm tra

Đọc/kiểm tra các file sau trước khi sửa:

```text
backend/PEMS.Api/Controllers/DelegationsController.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationDetails/ViewGuestDelegationDetailsQueryHandler.cs
backend/PEMS.Application/Delegations/Queries/SearchDelegations/SearchDelegationsQueryHandler.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListDto.cs
backend/PEMS.Application/Delegations/Commands/ApproveCrossCampusRequest/
backend/PEMS.Application/Delegations/Commands/ProcessVisitRequest/
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/pages/dashboard/visit/HoVisitProcessDetail.tsx
frontend/pems-react/src/shared/auth/resolveEffectiveRole.ts
frontend/pems-react/src/shared/constants/roles.ts
```

Nếu backend dùng SQL view, kiểm tra:

```sql
SHOW CREATE VIEW vw_visit_requests_for_ho;
```

Nếu view vẫn có:

```sql
WHERE vr.visit_scope = 'MULTI_CAMPUS'
```

thì cần chạy SQL patch hoặc thay view bằng bản mới.

---

## 3. Backend rule bắt buộc

### 3.1. HO list/search/detail

HO phải thấy:

```text
visit_scope IN ('MULTI_CAMPUS', 'SINGLE_CAMPUS')
```

Không được filter `SINGLE_CAMPUS` khỏi list/search/detail.

Tìm và sửa mọi đoạn tương tự:

```csharp
if (currentUser.RoleCode == "HO")
{
    query = query.Where(x => x.VisitScope == "MULTI_CAMPUS");
}
```

hoặc:

```csharp
.Where(x => x.VisitScope == VisitScope.MultiCampus)
```

Nếu đoạn này chỉ dùng cho action approve/reject thì giữ. Nếu dùng cho list/detail/search thì phải bỏ filter hoặc đổi thành:

```csharp
if (currentUser.RoleCode == "HO")
{
    query = query.Where(x =>
        x.VisitScope == "MULTI_CAMPUS" ||
        x.VisitScope == "SINGLE_CAMPUS");
}
```

---

### 3.2. allowedActions cho HO

Với `MULTI_CAMPUS` đang `PENDING_APPROVAL`:

```json
["VIEW_DETAIL", "HO_APPROVE", "HO_REJECT"]
```

Với `MULTI_CAMPUS` không còn chờ HO duyệt:

```json
["VIEW_DETAIL"]
```

Với mọi `SINGLE_CAMPUS`:

```json
["VIEW_DETAIL"]
```

Không trả về các action sau cho HO trên `SINGLE_CAMPUS`:

```text
HO_APPROVE
HO_REJECT
APPROVE_AND_ASSIGN_HOST
CAMPUS_REJECT
TRANSFER_HOST
CANCEL_BY_HOST
CANCEL_BY_VISITOR
ASSIGN_HOST
PREPARE_VISIT
```

---

### 3.3. DTO field nên set đúng

Nếu DTO list/detail có các field convenience sau thì set:

```text
TabType
CurrentUserRelation
IsReadOnly
AllowedActions
```

Với HO + SINGLE_CAMPUS:

```text
TabType = RESPONSIBLE hoặc MANAGEMENT tùy enum hiện có
CurrentUserRelation = HO_MONITOR
IsReadOnly = true
AllowedActions = [VIEW_DETAIL]
```

Với HO + MULTI_CAMPUS + PENDING_APPROVAL:

```text
CurrentUserRelation = HO_APPROVER
IsReadOnly = false
AllowedActions = [VIEW_DETAIL, HO_APPROVE, HO_REJECT]
```

Với HO + MULTI_CAMPUS đã xử lý:

```text
CurrentUserRelation = HO_MONITOR
IsReadOnly = true
AllowedActions = [VIEW_DETAIL]
```

---

### 3.4. Mutation endpoint vẫn phải chặn

HO không được thao tác nghiệp vụ trên `SINGLE_CAMPUS`.

Các command/action sau phải reject nếu actor là HO và request là `SINGLE_CAMPUS`:

```text
ApproveCrossCampusRequest
RejectCrossCampusRequest nếu có
ProcessVisitRequest
ApproveAndAssignHost
CampusReject
TransferHost
CancelVisitRequest
CancelVisitCampusInstance
```

Trả lỗi gợi ý:

```json
{
  "success": false,
  "errorCode": "HO_SINGLE_CAMPUS_READ_ONLY",
  "message": "HO chỉ được xem đơn một cơ sở ở chế độ theo dõi, không được xử lý nghiệp vụ trên đơn này."
}
```

HTTP status khuyến nghị:

```text
403 Forbidden nếu coi đây là vượt quyền/scope.
422 Business Rule nếu project đang dùng lỗi nghiệp vụ cho sai flow.
```

---

## 4. Frontend rule bắt buộc

Frontend không được tự loại `SINGLE_CAMPUS` khỏi danh sách HO.

Tìm và sửa các đoạn tương tự:

```ts
items.filter(x => x.visitScope === 'MULTI_CAMPUS')
```

hoặc:

```ts
if (effectiveRole === 'HO') {
  return item.visitScope === 'MULTI_CAMPUS';
}
```

Frontend phải render theo `allowedActions[]` từ backend.

Với HO + SINGLE_CAMPUS:

```text
- Row vẫn hiển thị trong danh sách.
- Nút xem chi tiết vẫn hiển thị.
- Không hiển thị duyệt/từ chối/gán host/hủy.
- Detail mở read-only.
```

Nếu DTO có `isReadOnly = true`, UI phải disable/ẩn mọi action mutation.

---

## 5. SQL view rule nếu backend dùng view

Nếu backend dùng `vw_visit_requests_for_ho`, view phải trả cả 2 scope:

```sql
WHERE vr.visit_scope IN ('MULTI_CAMPUS', 'SINGLE_CAMPUS')
```

`can_ho_decide` phải chỉ bằng 1 khi:

```sql
vr.visit_scope = 'MULTI_CAMPUS'
AND vr.status = 'PENDING_APPROVAL'
```

Với `SINGLE_CAMPUS`, `can_ho_decide` luôn bằng 0.

---

## 6. Test bắt buộc

### SQL test

```sql
SELECT visit_scope, COUNT(*)
FROM visit_requests
GROUP BY visit_scope;

SELECT visit_scope, COUNT(*)
FROM vw_visit_requests_for_ho
GROUP BY visit_scope;

SELECT request_code, delegation_name, visit_scope, request_status, can_ho_decide
FROM vw_visit_requests_for_ho
WHERE visit_scope = 'SINGLE_CAMPUS';
```

Kỳ vọng:

```text
visit_requests có SINGLE_CAMPUS.
vw_visit_requests_for_ho cũng có SINGLE_CAMPUS.
SINGLE_CAMPUS trong vw_visit_requests_for_ho có can_ho_decide = 0.
```

### Backend API test

Login HO rồi gọi API list/search/detail.

Kỳ vọng:

```text
HO list có cả MULTI_CAMPUS và SINGLE_CAMPUS.
SINGLE_CAMPUS trả allowedActions = [VIEW_DETAIL].
SINGLE_CAMPUS trả isReadOnly = true nếu DTO có field này.
```

Gọi approve/reject trên SINGLE_CAMPUS bằng HO:

```text
Phải bị chặn 403 hoặc 422.
```

### Frontend test

```text
HO vào màn Quản lý đơn tiếp khách.
Thấy cả đơn liên cơ sở và đơn một cơ sở.
Đơn một cơ sở chỉ có nút xem.
Mở chi tiết đơn một cơ sở ở chế độ read-only.
Không có nút duyệt/từ chối/gán host/hủy.
```

---

## 7. Build/test command

Backend:

```bash
dotnet restore
dotnet build
```

Nếu có test project chạy được:

```bash
dotnet test
```

Frontend:

```bash
cd frontend/pems-react
npm run build
```

Nếu có script:

```bash
npm run lint
npm run typecheck
```

Nếu thiếu script thì báo rõ, không được nói pass giả.

---

## 8. Báo cáo sau khi sửa

Báo cáo theo format:

```text
1. Root cause
2. Files changed
3. Backend changes
4. Frontend changes
5. SQL/view changes nếu có
6. API contract impact
7. Permission/scope confirmation
8. Manual test result
9. Build/test result
10. Remaining TODO
```
