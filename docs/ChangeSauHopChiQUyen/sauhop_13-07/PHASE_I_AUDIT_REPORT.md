# Phase I Zero-Unclassified Audit Report

## 1. Methodology
- Tiến hành tìm kiếm toàn bộ (regex & literal search) 10 legacy fields (`DelegationName`, `VisitType`, `VisitTypeOther`, `Purpose`, `WorkingContent`, `WorkingLanguage`, `TransportationNote`, `MediaConsentStatus`, `MediaConsentNote`, `NoteToFptu`) trên toàn bộ thư mục `backend/`.
- Thực hiện semantic analysis thủ công để xác định ngữ cảnh sử dụng (entity nào được gọi, có kiểm tra `FormSchemaVersion` hay không, ghi vào v1 hay v2).
- Loại trừ False Positives rõ ràng, bao gồm:
  - `FilesController.Purpose` (tham số API không liên quan tới database).
  - Enum `OtpPurpose` và các references trên entity `OtpToken.Purpose`.
  - Enum `FilePurpose` dùng cho Google Drive storage.
  - Các properties trên DTO/ViewModel nếu chúng được hydrate thuần túy từ bảng `visit_instance_form_details` (V2-aware reads).
- Các occurence còn lại bắt buộc map vào 1 trong 3 category: `Runtime V1 read`, `Runtime dual-read/compatibility read`, hoặc `Runtime compatibility projection write`. Không dùng `Various` hoặc `Unclassified`.

## 2. False Positives Excluded
- `backend/PEMS.Domain/Enums/OtpPurpose.cs` (và các file gọi nó)
- `backend/PEMS.Domain/Enums/FilePurpose.cs`
- `backend/PEMS.Infrastructure/Identity/OtpService.cs` (`t.Purpose == purpose`)
- `backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs` (`FilePurpose` check)
- `backend/PEMS.Api/Controllers/FilesController.cs` (`Purpose` query string param)
- `GetStaffLeaderDeptInvoiceItemsQuery` & `GetHoReportOverviewQueryHandler` (đây là V2-aware reads vì đã check `FormSchemaVersion >= FormSchemaVersions.PerCampus`).

## 3. Detailed Audit Table (Blockers Only)

| Field | File | Category | Read/write | Runtime caller/consumer | V1/V2 behavior | Blocker? |
|---|---|---|---|---|---|---|
| DelegationName | `PEMS.Application/EmailActions/ExecuteEmailActionCommandHandler.cs` | Runtime compatibility projection write | Write | V2 Submit/Edit flow | Compatibility | Yes |
| DelegationName | `PEMS.Application/EmailActions/GetEmailActionInfoQueryHandler.cs` | Runtime dual-read/compatibility read | Read | Visitor Email Action | Compatibility | Yes |
| DelegationName | `PEMS.Infrastructure/BackgroundJobs/HoUnprocessedCampusAlertHostedService.cs` | Runtime V1 read | Read | Background Job | Compatibility | Yes |
| DelegationName | `PEMS.Infrastructure/BackgroundJobs/VisitReminderDispatchHostedService.cs` | Runtime V1 read | Read | Background Job | Compatibility | Yes |
| DelegationName | `PEMS.Infrastructure/Services/VisitContactClaimService.cs` | Runtime V1 read | Read | Auth/Claim Flow | Compatibility | Yes |
| DelegationName | `PEMS.Infrastructure/Services/VisitRequestService.cs` | Runtime V1 read | Read | V1 GET API | Compatibility | Yes |
| DelegationName | `PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs` | Runtime compatibility projection write | Write | V2 Submit API | Compatibility | Yes |
| DelegationName | `PEMS.Infrastructure/Services/VisitRequestV2EditOps.cs` | Runtime compatibility projection write | Write | V2 Edit flow | Compatibility | Yes |
| DelegationName | `PEMS.Infrastructure/Services/VisitRequestV2EditService.cs` | Runtime compatibility projection write | Write | V2 Edit API | Compatibility | Yes |
| DelegationName | `PEMS.Infrastructure/Services/VisitSafeEditService.cs` | Runtime compatibility projection write | Write | V2 Safe Edit API | Compatibility | Yes |
| VisitType | `PEMS.Infrastructure/Services/VisitRequestService.cs` | Runtime V1 read | Read | V1 GET API | Compatibility | Yes |
| VisitType | `PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs` | Runtime compatibility projection write | Write | V2 Submit API | Compatibility | Yes |
| VisitType | `PEMS.Infrastructure/Services/VisitRequestV2EditService.cs` | Runtime compatibility projection write | Write | V2 Edit API | Compatibility | Yes |
| VisitTypeOther | `PEMS.Infrastructure/Services/VisitRequestService.cs` | Runtime V1 read | Read | V1 GET API | Compatibility | Yes |
| VisitTypeOther | `PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs` | Runtime compatibility projection write | Write | V2 Submit API | Compatibility | Yes |
| Purpose | `PEMS.Infrastructure/Services/VisitRequestService.cs` | Runtime V1 read | Read | V1 GET API | Compatibility | Yes |
| Purpose | `PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs` | Runtime compatibility projection write | Write | V2 Submit API | Compatibility | Yes |
| Purpose | `PEMS.Infrastructure/Services/VisitRequestV2EditService.cs` | Runtime compatibility projection write | Write | V2 Edit API | Compatibility | Yes |
| Purpose | `PEMS.Infrastructure/Services/VisitSafeEditService.cs` | Runtime compatibility projection write | Write | V2 Safe Edit API | Compatibility | Yes |
| WorkingContent | `PEMS.Infrastructure/Services/VisitRequestService.cs` | Runtime V1 read | Read | V1 GET API | Compatibility | Yes |
| WorkingContent | `PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs` | Runtime compatibility projection write | Write | V2 Submit API | Compatibility | Yes |
| WorkingLanguage | `PEMS.Infrastructure/Services/VisitRequestService.cs` | Runtime V1 read | Read | V1 GET API | Compatibility | Yes |
| WorkingLanguage | `PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs` | Runtime compatibility projection write | Write | V2 Submit API | Compatibility | Yes |
| TransportationNote | `PEMS.Infrastructure/Services/VisitRequestService.cs` | Runtime V1 read | Read | V1 GET API | Compatibility | Yes |
| TransportationNote | `PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs` | Runtime compatibility projection write | Write | V2 Submit API | Compatibility | Yes |
| MediaConsentStatus | `PEMS.Infrastructure/Services/VisitRequestService.cs` | Runtime V1 read | Read | V1 GET API | Compatibility | Yes |
| MediaConsentStatus | `PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs` | Runtime compatibility projection write | Write | V2 Submit API | Compatibility | Yes |
| MediaConsentNote | `PEMS.Infrastructure/Services/VisitRequestService.cs` | Runtime V1 read | Read | V1 GET API | Compatibility | Yes |
| MediaConsentNote | `PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs` | Runtime compatibility projection write | Write | V2 Submit API | Compatibility | Yes |
| NoteToFptu | `PEMS.Infrastructure/Services/VisitRequestService.cs` | Runtime V1 read | Read | V1 GET API | Compatibility | Yes |
| NoteToFptu | `PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs` | Runtime compatibility projection write | Write | V2 Submit API | Compatibility | Yes |

## 4. Aggregate Counts
- **Total Occurrences Reviewed**: ~120
- **False Positives Excluded**: ~80 (OtpPurpose, FilePurpose, Controllers, V2-aware Reports)
- **True Blockers**: 31 (liệt kê trên)
  - Runtime V1 Read: 14
  - Runtime Dual-Read: 1
  - Runtime Compatibility Write: 16

## 5. Execution Flags & Readiness Statement
- **`full backfill`**: NOT RUN / UNKNOWN
- **`export/restore proof`**: NOT RUN / UNKNOWN
- **`disposable drills`**: NOT RUN (Chưa chạy thật trên MySQL client)
- **Readiness**: Phase I is **NOT READY FOR EXECUTION** because V1 fallback logic and runtime legacy reads/writes are still fully active in the codebase and rely on the global columns.
