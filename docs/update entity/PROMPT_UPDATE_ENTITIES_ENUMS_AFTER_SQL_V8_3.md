# PROMPT — Quét toàn bộ code và cập nhật PEMS theo SQL full mới nhất v8.3

> Dùng prompt này cho Claude/Cursor/AI code assistant khi database đã được thay bằng bản SQL full mới nhất.  
> Mục tiêu là **quét toàn bộ code liên quan đến schema mới**, tạo báo cáo mismatch trước khi sửa, rồi cập nhật backend/frontend để khớp với SQL.  
> Prompt này **không rút gọn tài liệu gốc**, chỉ yêu cầu đọc docs để hiểu nghiệp vụ và sửa code đúng theo SQL.

---

## 0. Vai trò

Bạn là **Senior Full-stack Developer + .NET 8 Clean Architecture Reviewer + EF Core/Pomelo MySQL Specialist + Security/RBAC Reviewer**.

Bạn đang sửa project:

```text
PEMS — Partnership Engagement Management System
```

Stack hiện tại:

```text
Backend: .NET 8, Clean Architecture, CQRS, MediatR, EF Core, Pomelo MySQL
Frontend: ReactJS / TypeScript
Database: MySQL 8
```

Cấu trúc chính:

```text
backend/
├── PEMS.Api
├── PEMS.Application
├── PEMS.Domain
└── PEMS.Infrastructure

frontend/
docs/
database/
```

---

## 1. Nguồn sự thật bắt buộc

### 1.1. SQL full mới nhất là nguồn chuẩn số 1

Trước khi sửa code, hãy tìm và đọc bản SQL full mới nhất. Ưu tiên file:

```text
pems_full_sql_42tables_final_v8_3_cancel_after_approval_full_create.sql
```

Nếu tên file trong project hơi khác, hãy tìm file SQL full mới nhất có nội dung/schema tương ứng v8.3.

**Luật quan trọng:**

```text
SQL full mới nhất là source of truth.
Docs chỉ dùng để hiểu nghiệp vụ.
Nếu docs/code khác SQL thì sửa theo SQL, không sửa theo trí nhớ hoặc file SQL cũ.
```

Không lấy các bản cũ làm chuẩn:

```text
v5
v7
v8
v8.1
v8.2
patch nhỏ lẻ nếu đã có SQL full v8.3
```

### 1.2. Docs cần đọc để hiểu nghiệp vụ

Sau khi đọc SQL, đọc thêm các file docs sau nếu tồn tại:

```text
docs/architecture/CLEAN_ARCHITECTURE.md
docs/architecture/PROJECT_STRUCTURE_FULL.md
docs/database/DATABASE_SCHEMA.md
docs/database/PEMS_V8_3_SCHEMA_FIX_REPORT.md
docs/delegation/uc17 submit form.md
docs/permissions/PERMISSION_MATRIX.md
docs/permissions/PERMISSION_RULES.md
docs/use-cases/USE_CASE_LIST.md
docs/use-cases/USE_CASE_NOTES.md
docs/PROJECT_OVERVIEW.md
docs/VISITOR_MANAGEMENT_SYSTEM.md
docs/Technology.md
```

Nếu docs có mâu thuẫn với SQL, ghi lại trong mismatch report và ưu tiên SQL.

---

## 2. Việc phải làm trước khi code

Không được sửa ngay. Trước tiên phải làm 4 bước:

### Bước 1 — Quét cấu trúc project

Đọc toàn bộ cấu trúc:

```text
backend/
frontend/
docs/
database/
```

Không tạo module trùng nếu module đã tồn tại.

### Bước 2 — Parse SQL full

Từ file SQL full mới nhất, lập danh sách:

```text
1. Tất cả bảng base
2. Tất cả column của từng bảng
3. PK/FK
4. Unique index/index
5. Enum values
6. Nullability
7. Default values
8. Trigger/view/procedure liên quan nếu có
9. Seed permissions nếu có
```

Đặc biệt phải parse trực tiếp các enum từ SQL. **Không hardcode enum theo prompt nếu SQL khác.**

Ví dụ với:

```sql
cancellation_source ENUM(...)
```

phải lấy đúng giá trị từ SQL. Nếu SQL chỉ có:

```sql
ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION')
```

thì code chỉ được có 2 giá trị đó. Không tự thêm `INTERNAL_DECISION`.

Nếu SQL có:

```sql
ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION')
```

thì code mới được có đủ 3 giá trị.

### Bước 3 — Quét code hiện tại

So sánh SQL với toàn bộ code:

```text
PEMS.Domain/Entities
PEMS.Domain/Enums
PEMS.Application
PEMS.Infrastructure/Persistence
PEMS.Api/Controllers
PEMS.Api/Contracts/ApiRoutes.cs
PermissionConstants / PermissionCode
DTOs
Validators
MappingProfiles
frontend/src
```

### Bước 4 — Tạo mismatch report trước khi sửa

Trước khi edit file, tạo báo cáo ngắn trong chat hoặc file tạm:

```text
1. Tables thiếu/thừa so với SQL
2. Entities thiếu/thừa
3. Columns thiếu/thừa
4. Type mismatch
5. Nullable mismatch
6. Enum mismatch
7. DbSet/configuration mismatch
8. DTO/API mismatch
9. Frontend type/status mismatch
10. Permission/UC mismatch
```

Sau đó mới sửa code.

---

## 3. Nguyên tắc sửa code

Bắt buộc tuân thủ:

```text
1. Không code lại từ đầu.
2. Không đổi kiến trúc Clean Architecture.
3. Không tạo module trùng.
4. Không giữ entity/DbSet cho bảng đã xóa khỏi SQL.
5. Không tự tạo bảng mới nếu SQL không có.
6. Không tự ý đổi nghiệp vụ đã chốt.
7. Không dùng enum cũ nếu SQL đã đổi.
8. Không sửa docs theo kiểu rút gọn/mất nội dung gốc.
9. Nếu cần cập nhật docs, chỉ bổ sung/override rõ ràng, không xóa tài liệu gốc nếu không được yêu cầu.
10. Sau khi sửa phải build backend.
```

---

## 4. Các điểm bắt buộc phải đồng bộ theo SQL v8.3

## 4.1. ID strategy

SQL mới dùng PK dạng:

```sql
BIGINT UNSIGNED AUTO_INCREMENT
```

Yêu cầu code:

```text
- PK/FK trong Entity/DTO/Command/Query/Route param phải dùng kiểu thống nhất.
- Nếu SQL vẫn là BIGINT UNSIGNED thì dùng ulong/ulong? trong C#.
- Không dùng Guid/string/char(36) cho PK/FK nữa.
- Không mix long và ulong tùy tiện.
```

Ví dụ đúng nếu SQL dùng `BIGINT UNSIGNED`:

```csharp
public ulong VisitRequestId { get; set; }
public ulong? PartnerId { get; set; }
public ulong VisitorUserId { get; set; }
```

Sai:

```csharp
public Guid VisitRequestId { get; set; }
public string VisitRequestId { get; set; }
```

---

## 4.2. `role_permissions`

Nếu SQL dùng surrogate PK:

```sql
role_permission_id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY
UNIQUE(role_id, sub_role, permission_id)
```

thì code phải dùng:

```csharp
public ulong RolePermissionId { get; set; }
```

EF mapping:

```csharp
builder.HasKey(x => x.RolePermissionId);
builder.Property(x => x.RolePermissionId).ValueGeneratedOnAdd();
builder.HasIndex(x => new { x.RoleId, x.SubRole, x.PermissionId }).IsUnique();
```

Không dùng composite primary key cũ làm PK entity.

---

## 4.3. Không còn bảng `pending_visit_requests`

Nếu SQL không có:

```sql
pending_visit_requests
```

thì code phải:

```text
- Xóa/ngừng map PendingVisitRequest entity.
- Xóa DbSet<PendingVisitRequest>.
- Xóa configuration tương ứng.
- Không insert form chưa verify vào database.
- UC-17 chỉ insert visit_requests sau khi OTP verify thành công.
```

Luồng đúng:

```text
Frontend sessionStorage draft
→ backend lưu OTP trong otp_tokens
→ verify OTP
→ submit chính thức
→ insert visit_requests + visit_request_campuses + visit_guest_members
```

---

## 4.4. Không còn bảng `public_contents`

Nếu SQL không có:

```sql
public_contents
```

thì code phải:

```text
- Xóa/ngừng map PublicContent entity.
- Xóa DbSet<PublicContent>.
- Public homepage/contact/news/FAQ/gallery lấy từ bảng thật trong SQL:
  news, news_translations, faqs, galleries, gallery_images, partners...
```

Không tạo lại bảng `public_contents`.

---

## 4.5. `visit_requests.status`

Phải đọc enum trực tiếp từ SQL. Với logic v8.3 đang chốt, request status là trạng thái đơn, không phải tiến độ chuyến thăm.

Kỳ vọng hiện tại:

```sql
visit_requests.status ENUM(
  'PENDING_APPROVAL',
  'APPROVED',
  'REJECTED',
  'CANCELLED'
)
```

Code tương ứng:

```csharp
public enum VisitRequestStatus
{
    PendingApproval,
    Approved,
    Rejected,
    Cancelled
}
```

Không để các trạng thái tiến độ trong request status:

```text
IN_PROGRESS
COMPLETED
APPROVED_BUT_NO_HOST
```

Nếu UI cần “Đang diễn ra/Đã hoàn tất”, hãy derive từ campus status, không lưu vào `visit_requests.status`.

---

## 4.6. `visit_request_campuses.status`

Phải đọc enum trực tiếp từ SQL.

Kỳ vọng hiện tại:

```sql
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

Code tương ứng:

```csharp
public enum VisitInstanceStatus
{
    WaitingRequestApproval,
    Assigned,
    BeforeVisit,
    DuringVisit,
    AfterVisit,
    Closed,
    Cancelled
}
```

Ý nghĩa bắt buộc:

```text
WAITING_REQUEST_APPROVAL = campus đã được chọn nhưng request tổng chưa duyệt.
ASSIGNED = request đã duyệt và campus đã có host.
BEFORE_VISIT = đang chuẩn bị.
DURING_VISIT = đang diễn ra.
AFTER_VISIT = hậu xử lý.
CLOSED = đã đóng campus instance.
CANCELLED = campus instance bị hủy.
```

Không hiểu `WAITING_REQUEST_APPROVAL` là “chờ gán host”.

---

## 4.7. Bỏ `actual_start_at` và `actual_end_at`

Nếu SQL không có:

```sql
actual_start_at
actual_end_at
```

thì phải xóa khỏi:

```text
Entity
DTO
Command
Query
Validator
MappingProfile
EF Configuration
Frontend type
Frontend UI
```

Không thêm lại 2 cột này.

Nếu cần lịch sử thời điểm thực tế, dùng status log/audit log hoặc bảng event riêng nếu sau này được chốt.

---

## 4.8. Host assignment

Nếu SQL có các cột:

```sql
current_host_user_id
host_assigned_by
host_assigned_at
host_assignment_source
host_transferred_by
host_transferred_at
host_transfer_note
```

thì Entity/DTO/mapping phải có đủ.

Enum:

```csharp
public enum HostAssignmentSource
{
    AutoStaffLeader,
    ManualApproval,
    Transferred
}
```

Map DB:

```text
AUTO_STAFF_LEADER
MANUAL_APPROVAL
TRANSFERRED
```

Luồng bắt buộc:

```text
MULTI_CAMPUS:
HO approve → request APPROVED → từng campus ASSIGNED → current_host_user_id = Staff Leader của campus → source AUTO_STAFF_LEADER.

SINGLE_CAMPUS:
Staff Leader approve → bắt buộc chọn host → request APPROVED → campus ASSIGNED → source MANUAL_APPROVAL.

Transfer host:
current_host_user_id = new host → source TRANSFERRED → ghi host_transferred_by/at/note.
```

Không có trạng thái “Approved but waiting host”.

---

## 4.9. UC-136 Cancel Visit Request

UC mới thuộc:

```text
Feature group: Delegation Reception Management / FE-02
Permission code: UC-136.CANCEL_VISIT_REQUEST
```

### Rule nghiệp vụ

Cancel chỉ dùng **sau khi request đã được duyệt**.

```text
Trước duyệt:
- Không dùng cancel.
- Nếu không chấp nhận đơn thì dùng reject flow UC-18/UC-22.

Sau duyệt:
- Visitor có thể tự hủy request của chính họ nếu còn được phép hủy.
- Host có thể hủy thay khách nếu khách xác nhận hủy qua kênh ngoài hệ thống.
```

### Không dùng `external_confirmation_note`

Không được có:

```text
external_confirmation_note
ExternalConfirmationNote
```

Dùng:

```text
cancellation_reason
```

Nếu Host hủy thay khách, ghi đầy đủ thông tin xác nhận ngoài hệ thống vào `cancellation_reason`.

Ví dụ:

```text
Khách xác nhận hủy qua email lúc 09:15 ngày 19/06/2026 do thay đổi lịch công tác.
```

### Cancellation fields

Nếu SQL có các field này thì Entity/DTO/mapping phải có đủ:

```csharp
public ulong? CancelledBy { get; set; }
public DateTime? CancelledAt { get; set; }
public CancellationActorType? CancellationActorType { get; set; }
public CancellationSource? CancellationSource { get; set; }
public string? CancellationReason { get; set; }
```

`CancellationActorType` phải khớp SQL:

```text
VISITOR
HOST
STAFF_LEADER
HO
SYSTEM
```

`CancellationSource` phải khớp chính xác SQL, không tự thêm giá trị.

Ví dụ nếu SQL là:

```sql
ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION')
```

thì code chỉ có:

```csharp
public enum CancellationSource
{
    SelfService,
    ExternalConfirmation
}
```

Nếu SQL có thêm `INTERNAL_DECISION`, khi đó mới thêm:

```csharp
InternalDecision
```

### Rule cancel

```text
1. Không cancel request PENDING_APPROVAL. Pending dùng reject flow.
2. Không cancel request REJECTED.
3. Không cancel lại request CANCELLED.
4. Chỉ cancel khi request APPROVED.
5. Chỉ cancel nếu campus còn ở trạng thái ASSIGNED hoặc BEFORE_VISIT.
6. Không cancel khi campus đã DURING_VISIT, AFTER_VISIT hoặc CLOSED.
7. Visitor chỉ cancel own approved request.
8. Host chỉ cancel campus instance mà họ là current_host_user_id.
9. Với single-campus: Host cancel thì cancel cả request + campus.
10. Với multi-campus: Host cancel campus của họ; nếu tất cả campus bị hủy thì request tổng thành CANCELLED.
11. Admin không có quyền cancel delegation.
12. Ghi visit_status_logs và audit log.
```

---

## 5. Backend cần rà soát

## 5.1. Domain Entities

Quét toàn bộ:

```text
backend/PEMS.Domain/Entities
```

Cần sửa:

```text
- ID/FK type
- Columns thiếu/thừa
- Nullable
- Enum
- Navigation
- Domain methods
```

Đặc biệt:

```text
VisitRequest
VisitRequestCampus
VisitGuestMember
VisitParticipant
VisitAgenda
VisitLogisticsItem
VisitStatusLog
RolePermission
User
OtpToken
News
Files/Documents
Feedbacks
```

---

## 5.2. Domain Enums

Quét:

```text
backend/PEMS.Domain/Enums
```

Bắt buộc rà:

```text
VisitRequestStatus
VisitInstanceStatus
HostAssignmentSource
CancellationActorType
CancellationSource
StatusOwnerType
VisitScope
DecisionActorRole
PermissionLevel
UserCreatedVia
SubRole
UserRoleCode
LogisticsItemStatus
```

Nếu còn enum cũ như:

```text
DelegationStatus
ApprovedButNoHost
InProgress trên request
Completed trên request
```

thì xóa hoặc chuyển thành UI/Application display-only, không map DB.

---

## 5.3. EF Core / DbContext / Configurations

Quét:

```text
backend/PEMS.Infrastructure
ApplicationDbContext
IApplicationDbContext
EntityConfigurations
```

Yêu cầu:

```text
- PK bigint unsigned + ValueGeneratedOnAdd.
- FK bigint unsigned, không ValueGeneratedOnAdd.
- Enum map string đúng DB value, không map int ordinal.
- Xóa mapping field/bảng không còn trong SQL.
- Thêm mapping field mới.
- FK/index/unique khớp SQL.
- DbSet khớp đúng 42 base tables hoặc đúng các table trong SQL.
```

---

## 5.4. Application Commands/Queries

Quét:

```text
backend/PEMS.Application
```

Đặc biệt:

```text
Delegations/Commands/SubmitVisitRequest
Delegations/Commands/ApproveCrossCampusRequest
Delegations/Commands/ProcessVisitRequest
Delegations/Commands/CancelVisitRequest
Delegations/Commands/CloseDelegation
Delegations/Queries/ViewGuestDelegationList
Delegations/Queries/ViewGuestDelegationDetails
Delegations/Queries/SearchDelegations
Authentication
Accounts
Profiles
Permissions/Roles
```

Yêu cầu:

```text
- Submit không insert trước OTP.
- Approve multi-campus auto assign Staff Leader.
- Approve single-campus bắt buộc chọn host.
- Cancel thuộc Delegations, không nằm trong UC17.
- Close khác Cancel.
- Reject khác Cancel.
- Query trả requestStatus + campusStatus.
```

---

## 5.5. API layer

Quét:

```text
backend/PEMS.Api/Controllers
backend/PEMS.Api/Contracts/ApiRoutes.cs
backend/PEMS.Api/Filters/PermissionAuthorizeAttribute.cs
```

Yêu cầu:

```text
- Controller không chứa business if/else.
- Controller chỉ gọi IMediator.
- Thêm route cancel đúng Delegations.
- Gắn UC-136 permission nếu endpoint yêu cầu đăng nhập.
- Public/pre-auth endpoint không check RBAC sai chỗ.
```

Gợi ý route:

```http
POST /api/delegations/{visitRequestId}/cancel
POST /api/delegations/{visitRequestId}/campuses/{visitInstanceId}/cancel
```

---

## 5.6. Authorization / Permission

Quét:

```text
PermissionConstants
PermissionCode
PermissionChecker
OwnershipChecker
AuthorizationExtensions
RBAC frontend config nếu có
```

Yêu cầu:

```text
- Có UC-136.CANCEL_VISIT_REQUEST.
- Group: Delegation Reception Management.
- Admin không có quyền cancel delegation.
- Visitor Own scope.
- Host own/current-host scope.
- Staff Leader campus scope.
- HO multi-campus scope.
```

Không chỉ dựa vào permission level; phải check data scope.

---

## 5.7. Frontend

Quét:

```text
frontend/src
```

Tìm:

```text
VisitRequestStatus
VisitInstanceStatus
DelegationStatus
actualStartAt
actualEndAt
externalConfirmationNote
cancel
CancelVisitRequest
APPROVED_BUT_NO_HOST
IN_PROGRESS
COMPLETED
```

Yêu cầu:

```text
- Type/status khớp SQL.
- Không hiển thị enum kỹ thuật trực tiếp.
- UI dùng 2 lớp status: requestStatus + campusStatus/progressDisplayStatus.
- Cancel button chỉ hiện khi request APPROVED và campus ASSIGNED/BEFORE_VISIT.
- Pending request không hiện cancel; pending phía internal dùng reject.
- Visitor self cancel và Host cancel phải có form reason.
```

---

## 6. Lệnh kiểm tra sau khi sửa

Chạy backend:

```bash
dotnet clean
dotnet restore
dotnet build
```

Nếu có test:

```bash
dotnet test
```

Chạy frontend nếu có:

```bash
npm install
npm run build
```

Hoặc dùng package manager đúng của project:

```bash
pnpm install
pnpm build
```

---

## 7. Grep/check bắt buộc

Sau khi sửa, tìm toàn repo:

```text
PendingVisitRequest
pending_visit_requests
PublicContent
public_contents
actual_start_at
actual_end_at
ActualStartAt
ActualEndAt
external_confirmation_note
ExternalConfirmationNote
APPROVED_BUT_NO_HOST
ApprovedButNoHost
VisitRequestStatus.InProgress
VisitRequestStatus.Completed
IN_PROGRESS as request status
COMPLETED as request status
Guid VisitRequestId
string VisitRequestId
char(36)
```

Nếu keyword còn trong docs cũ hoặc comment, phải đảm bảo không gây hiểu nhầm. Nếu còn trong code runtime thì phải sửa.

---

## 8. Acceptance Criteria

Hoàn thành khi:

```text
1. Build backend thành công.
2. Nếu có frontend build thì frontend build thành công.
3. Entity/Enum/DTO/EF mapping khớp SQL full mới.
4. Không còn table/entity/mapping đã bị xóa khỏi SQL.
5. Không còn column đã bị xóa khỏi SQL.
6. Enum code khớp đúng enum trong SQL, đặc biệt cancellation_source phải parse từ SQL.
7. UC-136 có command/handler/validator/response hoặc đã cập nhật nếu module tồn tại.
8. Cancel thuộc Delegation Reception Management.
9. Cancel chỉ dùng sau khi request approved.
10. Pending request không cancel, dùng reject.
11. Host assignment sau approve hoạt động đúng.
12. Admin không xem/hủy delegation.
13. Query/list/detail không trả trạng thái sai.
14. Frontend không dùng enum cũ.
15. Không tạo module trùng.
16. Có báo cáo file changed.
```

---

## 9. Báo cáo bắt buộc sau khi sửa

Khi hoàn thành, trả báo cáo theo mẫu:

```text
# PEMS SQL v8.3 Code Sync Report

## 1. SQL source used
- File:
- Hash/modified time nếu có:

## 2. Docs read
- CLEAN_ARCHITECTURE.md
- PROJECT_STRUCTURE_FULL.md
- DATABASE_SCHEMA.md
- PERMISSION_MATRIX.md
- PERMISSION_RULES.md
- USE_CASE_LIST.md
- USE_CASE_NOTES.md
- VISITOR_MANAGEMENT_SYSTEM.md
- Others:

## 3. Mismatch found before changes
- Entities:
- Enums:
- DbContext/mapping:
- Application:
- API:
- Frontend:
- Permission:
- Obsolete fields/tables:

## 4. Files changed
- Backend:
- Frontend:
- Docs/config:

## 5. Entities updated

## 6. Enums updated

## 7. DbContext/configurations updated

## 8. Commands/handlers/validators updated

## 9. Controllers/routes updated

## 10. Permission constants/RBAC updated

## 11. Removed obsolete code

## 12. Build/test result
- dotnet build:
- dotnet test:
- frontend build:

## 13. Remaining risks / manual checks
```

Không được trả lời chung chung “done”. Phải liệt kê rõ file đã sửa và phần đã kiểm tra.
