# PEMS SQL v8.3 Code Sync Report

> Generated khi đồng bộ toàn bộ backend (.NET 8 Clean Architecture) + frontend (React/Vite)
> theo SQL full mới nhất. Build backend: **0 errors / 0 warnings**. Frontend `vite build`: **OK**.

## 1. SQL source used
- File: `database/scripts/pems_full.sql` (đang ở trạng thái git *modified* — bản full mới nhất).
- Header ghi "v8.2 CANCEL_DELEGATION" nhưng **nội dung khớp đúng kỳ vọng v8.3** trong prompt:
  - 42 base tables.
  - `visit_requests.status` chỉ còn `PENDING_APPROVAL, APPROVED, REJECTED, CANCELLED`.
  - UC-136 cancel sau duyệt; **không** có cột external note; dùng `cancellation_reason`.
  - **Không** có `pending_visit_requests`, `public_contents`, `actual_start_at`, `actual_end_at`.
  - Mọi PK/FK = `BIGINT UNSIGNED` → C# `ulong`.
- Đây là source of truth. Không dùng các bản v5/v7/v8/v8.1/v8.2 cũ.

## 2. Docs read
- `docs/update entity/PROMPT_UPDATE_ENTITIES_ENUMS_AFTER_SQL_V8_3.md` (yêu cầu chính).
- Các file SQL liên quan để đối chiếu enum/seed (UC-136, cancellation, host assignment).
- Nghiệp vụ cancel/host-assignment lấy trực tiếp từ comment trong SQL + prompt.

## 3. Mismatch found before changes
- **Tables/Entities thừa:** `PendingVisitRequest`, `PublicContent` (bảng đã bị xóa khỏi SQL).
- **Columns:** `VisitRequest` thiếu nhóm cancellation; `VisitRequestCampus` thừa `actual_start_at/end_at`,
  thiếu `host_assigned_by/at/source` + nhóm cancellation; `VisitStatusLog` thiếu `status_owner_type`.
- **Type:** 44/44 entity dùng `string`/`Guid`/`long` cho PK/FK → phải `ulong`/`ulong?`.
- **Enum:** `VisitRequestStatuses` còn `IN_PROGRESS/COMPLETED`; `UserCreatedVia` thiếu `SSO_AUTO_PROVISION`;
  thiếu `HostAssignmentSource, CancellationActorType, CancellationSource, StatusOwnerType, DecisionActorRole`.
- **DbContext:** `DbSet<PendingVisitRequest>`, `DbSet<PublicContent>`; `role_permissions` map composite PK;
  toàn bộ map `CHAR(36)` `HasMaxLength(36).IsFixedLength()`.
- **Application:** chưa có UC-136; Initiate/Verify/ResendOtp insert form vào `pending_visit_requests` trước OTP.
- **API:** Delegations/VisitRequests controller chưa gắn `[RequirePermission]`; chưa có route cancel.
- **Frontend:** chưa có enum status; chưa có cancel API; verify gửi `sessionToken` (sai với schema mới).
- **Permission:** `PermissionConstants` thiếu `UC-136.CANCEL_VISIT_REQUEST`.

## 4. Files changed
### Backend — Domain
- Entities (ID `string/Guid/long` → `ulong`, 39 file): toàn bộ `PEMS.Domain/Entities/**`
  (Users, Delegations, Partners, News, Galleries, Documents, Emails, ApiIntegrations, Calendar, …).
- Rewrite đặc biệt: `VisitRequest`, `VisitRequestCampus`, `VisitStatusLog`, `RolePermission`, `User`.
- **Xóa:** `Entities/Delegations/PendingVisitRequest.cs`, `Entities/PublicContents/PublicContent.cs`.
- Enums mới: `HostAssignmentSource`, `CancellationActorType`, `CancellationSource`, `StatusOwnerType`,
  `DecisionActorRole`, `VisitScope`; sửa `UserCreatedVia` (+`SSO_AUTO_PROVISION`), `UserRoleCode` (6 role).
- `Constants/VisitRequestConstants.cs`: bỏ `IN_PROGRESS/COMPLETED`, thêm `OTHER` working language.

### Backend — Infrastructure
- `Persistence/ApplicationDbContext.cs`: bỏ 2 DbSet obsolete (còn đúng **42** DbSet); `role_permissions`
  → surrogate PK + `UNIQUE(role_id, sub_role, permission_id)`; bỏ toàn bộ map `CHAR(36)`; giữ FK/JSON.
- `Identity/`: `CurrentUserService`, `JwtTokenService`, `SessionService`, `PermissionChecker`,
  `OwnershipChecker`, `OtpService` → `ulong`; ID sinh bởi DB (bỏ `Guid.NewGuid().ToString()`).
- `Logging/SecurityAuditService`, `Services/UserProvisionService`, `Services/VisitRequestService` → `ulong`.

### Backend — Application
- Interfaces: `ICurrentUserService`, `IJwtTokenService`, `ISessionService`, `IPermissionChecker`,
  `IOwnershipChecker`, `ISecurityAuditService`, `IUserProvisionService`, `IVisitRequestService`,
  `IApplicationDbContext` → `ulong` + bỏ 2 DbSet obsolete.
- Auth/Accounts/Profiles/Campus handlers + DTOs → `ulong` (claims parse `ulong` ở biên).
- **UC-136 mới:** `Delegations/Commands/CancelVisitRequest/{Command,Response,Handler,Validator}`.
- `Common/Security/PermissionConstants.cs`: thêm `CancelVisitRequest = "UC-136.CANCEL_VISIT_REQUEST"`.
- **Luồng UC-17 (không pending table):** `InitiateVisitRequest` chỉ tạo OTP; `VerifyAndCreate` nhận lại
  full form + OTP; `ResendOtp` nhận email + name. DTO `PendingVisitRequestFormData` → `VisitRequestFormData`.

### Backend — API
- `Controllers/DelegationsController.cs`: thêm 2 route cancel + `[RequirePermission(UC-136, O)]`.
- `Filters/PermissionAuthorizeAttribute.cs`, `Middleware/SessionValidationMiddleware.cs` → `ulong`.

### Frontend (`frontend/pems-react/src`)
- `features/delegations/types/delegations.types.ts`: enum `VisitRequestStatus` + `VisitInstanceStatus`,
  labels, `canCancelInstance()`, cancel payload/result types.
- `features/delegations/api/delegationsApi.ts`: `cancelVisitRequest`, `cancelVisitRequestCampus`.
- `shared/api/endpoints.ts`: route cancel + cancelCampus.
- `features/visit-request/api/visitRequestApi.ts` + `hooks/useVisitRequestForm.ts`: verify resubmit full form,
  resend gửi email + name (đồng bộ contract v8.3).

## 5. Entities updated
Tất cả 42 entity khớp SQL: PK/FK = `ulong`/`ulong?`; `VisitRequest`/`VisitRequestCampus` đủ cancellation +
host-assignment, bỏ `actual_*`; `VisitStatusLog` có `status_owner_type`; `RolePermission` có surrogate PK.

## 6. Enums updated
`VisitRequestStatus`, `VisitInstanceStatus`, `LogisticsItemStatus`, `SubRole` (đã đúng); thêm
`HostAssignmentSource (AUTO_STAFF_LEADER/MANUAL_APPROVAL/TRANSFERRED)`,
`CancellationActorType (VISITOR/HOST/STAFF_LEADER/HO/SYSTEM)`,
`CancellationSource (SELF_SERVICE/EXTERNAL_CONFIRMATION — đúng 2 giá trị)`,
`StatusOwnerType (REQUEST/CAMPUS_INSTANCE)`, `DecisionActorRole (HO/STAFF_LEADER/SYSTEM)`,
`VisitScope`; `UserCreatedVia` +`SSO_AUTO_PROVISION`.

## 7. DbContext/configurations updated
42 DbSet; `RolePermission` surrogate PK + unique index + `ValueGeneratedOnAdd`; bỏ map CHAR(36); FK/JSON giữ.

## 8. Commands/handlers/validators updated
UC-136 mới (command/handler/validator/response). UC-17 Initiate/Verify/ResendOtp tái cấu trúc theo otp_tokens.
Account/Auth/Profile/Campus handlers cập nhật kiểu ID.

## 9. Controllers/routes updated
`POST /api/delegations/{visitRequestId}/cancel` và
`POST /api/delegations/{visitRequestId}/campuses/{visitInstanceId}/cancel` (gắn UC-136, level Own).

## 10. Permission constants/RBAC updated
`UC-136.CANCEL_VISIT_REQUEST` thêm vào `PermissionCodes`; grant theo SQL: VISITOR=O, STAFF/Leader=E,
STAFF/Staff=O, HO=E (Admin/Dept **không** có → Admin không cancel, có check phòng thủ trong handler).

## 11. Removed obsolete code
- Entity `PendingVisitRequest`, `PublicContent` + DbSet + mọi tham chiếu runtime.
- `IN_PROGRESS/COMPLETED` khỏi request status.
- Cột `actual_start_at/actual_end_at` khỏi `VisitRequestCampus`.
- `pending_visit_requests` flow (insert trước OTP).

## 12. Build/test result
- `dotnet build` (PEMS.Api → kéo theo Domain/Application/Infrastructure): **Build succeeded, 0 Error(s), 0 Warning(s)**.
- `dotnet test`: project không có test project.
- Frontend `npm run build` (vite): **✓ built** (chỉ cảnh báo chunk-size, không lỗi).
- §7 grep backend runtime: 0 hit cho `PendingVisitRequest, PublicContents, ActualStartAt/EndAt,
  ExternalConfirmationNote, APPROVED_BUT_NO_HOST, Guid/string VisitRequestId, char(36)`
  (chỉ còn 1 comment giải thích `actual_*` trong `VisitRequestCampus.cs` — không gây hiểu nhầm).

## 13. Remaining risks / manual checks
1. **Scaffold DTOs còn `Guid? Id`** (~125 file Application thuộc các feature *chưa implement* — trả về
   `NotImplementedException`/stub). Không ảnh hưởng schema/build; nên đổi sang `ulong` khi implement feature.
   (2 `Guid` còn lại ở Domain/Infra/Api là JWT `Jti` và random request-code — hợp lệ, không phải ID entity.)
2. **Frontend cancel UI:** đã có types + API + `canCancelInstance()`; chưa nối nút Cancel + form lý do vào
   `VisitRequestManagement.tsx` (trang đang dùng mock data). Cần wiring khi nối API thật.
3. **EF không dùng migration file** — schema áp bằng `pems_full.sql`. Khi chạy thật cần dùng đúng DB v8.3.
4. **Nghiệp vụ host-assignment** (auto Staff Leader khi HO duyệt multi-campus; bắt buộc chọn host khi
   Staff Leader duyệt single-campus): cột đã có đủ trong entity; logic gán host nằm ở Approve handlers
   (đang scaffold) — cần implement đầy đủ khi làm UC-18/UC-22.
5. **Frontend typecheck**: `npm run build` (vite/esbuild) không typecheck; còn lỗi scaffold tiền-tồn ở `tsc`.
