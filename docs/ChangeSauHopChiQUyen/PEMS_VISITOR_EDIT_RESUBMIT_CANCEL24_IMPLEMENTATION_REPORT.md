# Implementation Report — Visitor Edit / Resubmit / Cancel 24h

Ngày thực hiện: 2026-07-06. Triển khai theo `PEMS_VISITOR_EDIT_RESUBMIT_CANCEL24_IMPLEMENTATION_PLAN.md`, trên baseline SQL:

```text
pems_full_v10_new_final_campus_independent_approval_self_host_transport_note_resubmit_agenda_cancel24_FULL_UPDATED.sql
```

## Files changed

| File | Change |
|---|---|
| `backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs` | Thêm 3 property `ResubmissionCount` (uint), `LastResubmittedAt`, `LastResubmittedBy` map cột mới (entity dùng data-annotation `[Column]`, không có file Configuration riêng) |
| `backend/PEMS.Domain/Constants/VisitRequestConstants.cs` | Thêm 8 error code: `VISIT_REQUEST_NOT_EDITABLE`, `VISIT_REQUEST_NOT_RESUBMITTABLE`, `RESUBMIT_CAMPUS_LIST_CHANGED`, `VISIT_CANCEL_WINDOW_EXPIRED`, `VISIT_ALREADY_STARTED_CANNOT_CANCEL`, `HOST_CANNOT_CANCEL_AFTER_VISIT_STARTED`, `VISIT_AGENDA_REQUIRED_BEFORE_START` |
| `backend/PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs` | `ApplyVisitRequestFormRules(minStartAdvanceHours = 72)` — submit mới giữ 72h, edit/resubmit truyền 24h |
| `backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/*` | DTO thêm `resubmissionCount/lastResubmittedAt/lastResubmittedBy/lastResubmittedByName` + `canEditPending/canResubmit`; handler tính eligibility (VietnamNow, wall-clock) và trả action `EDIT_PENDING_REQUEST` / `RESUBMIT_REJECTED_REQUEST` cho Visitor owner |
| `backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/*` | Detail DTO trả thêm resubmission fields |
| `backend/PEMS.Application/Delegations/Queries/GetEditableVisitRequestDetail/*` (mới) | Query + DTO + handler cho `GET /api/visit-requests/{id}/edit-detail` (owner-only, kèm `previousDecisions` cho banner resubmit) |
| `backend/PEMS.Application/Delegations/Commands/UpdatePendingVisitRequest/*` (mới) | Command/Validator(24h)/Handler/Response — sửa đơn pending, CHO đổi campus (diff update), audit `UPDATE_PENDING_VISIT_REQUEST`, notify Staff Leader, KHÔNG tăng resubmission_count |
| `backend/PEMS.Application/Delegations/Commands/ResubmitRejectedVisitRequest/*` (mới) | Command/Validator(24h)/Handler/Response — gửi lại đơn REJECTED toàn bộ, campus set phải trùng, snapshot JSON quyết định cũ vào `audit_log_changes` trước khi clear, 2-phase save (request → campuses), notify Staff Leader |
| `backend/PEMS.Application/Delegations/Commands/CancelVisitRequest/CancelVisitRequestCommandHandler.cs` | Rule 24h cho Visitor (cả nhánh PENDING lẫn sau duyệt) với `VISIT_CANCEL_WINDOW_EXPIRED`; Host chỉ hủy trước giờ bắt đầu (`HOST_CANNOT_CANCEL_AFTER_VISIT_STARTED`); so sánh giờ chuyển sang `VietnamNow` (wall-clock); thêm error codes cho DURING/AFTER/CLOSED |
| `backend/PEMS.Application/Delegations/Commands/CompleteVisitStage/CompleteVisitStageCommandHandler.cs` | `EnsureAgendaExistsAsync` trước MỌI transition sang DURING_VISIT / AFTER_VISIT / CLOSED (409 `VISIT_AGENDA_REQUIRED_BEFORE_START`, mirror trigger DB) |
| `backend/PEMS.Api/Controllers/VisitRequestsController.cs` | 3 endpoint mới: `GET {id}/edit-detail`, `PUT {id}/pending-edit`, `POST {id}/resubmit` (auth/ownership enforce trong handler) |
| `frontend/pems-react/src/features/delegations/types/delegations.types.ts` | Action `EDIT_PENDING_REQUEST`/`RESUBMIT_REJECTED_REQUEST`; row fields resubmit + `canEditPending`/`canResubmit` |
| `frontend/pems-react/src/shared/api/endpoints.ts` | `visitRequests.editDetail/pendingEdit/resubmit` |
| `frontend/pems-react/src/features/visit-request/api/visitRequestApi.ts` | Types `EditableVisitRequestDetail`… + `getEditableDetail`/`updatePending`/`resubmitRejected` (reuse `mapToPayload`, không OTP) |
| `frontend/pems-react/src/features/visit-request/schema/visitRequest.schema.ts` | Factory `buildVisitRequestSchema(minAdvanceHours)`; `visitRequestSchema` (72h) giữ nguyên, thêm `visitRequestEditSchema` (24h) |
| `frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx` | Slot 3: nút "Sửa đơn" (PencilLine, blue) và "Sửa & gửi lại" (RefreshCw, orange) theo allowedActions, điều hướng `/dashboard/visit/edit/:id` và `/dashboard/visit/resubmit/:id` |
| `frontend/pems-react/src/pages/dashboard/visit/EditVisitRequest.tsx` (mới) | Trang form edit/resubmit dùng chung — load `edit-detail` thật, reuse 5 section của form UC-17, banner theo mode + hiển thị lý do từ chối cũ, submit đúng endpoint |
| `frontend/pems-react/src/App.tsx` | Route `visit/edit/:visitRequestId` + `visit/resubmit/:visitRequestId` |

## SQL

- KHÔNG sửa file SQL — baseline `..._resubmit_agenda_cancel24_FULL_UPDATED.sql` đã có sẵn 3 cột resubmit + index/FK + trigger agenda-required + trigger cancel-24h (đã đối chiếu: cột dòng ~815-819, FK ~866, agenda rule trong `trg_visit_campuses_assignment_validate_bu`, cancel-24h trong `trg_visit_requests_cancel_validate_bu`).
- ⚠️ **Phải import file SQL này vào DB local trước khi chạy** (DB cũ chưa có `resubmission_count` ⇒ EF sẽ lỗi cột không tồn tại).
- Lưu ý timezone: trigger 24h dùng `cancelled_at` (backend ghi UtcNow) nên lỏng hơn check backend (VietnamNow) 7 tiếng → backend luôn chặn trước, không bao giờ rơi vào SIGNAL 45000.

## Backend

- `EDIT_PENDING_REQUEST`: Visitor owner + `PENDING_APPROVAL` + mọi campus `WAITING_REQUEST_APPROVAL` + start sớm nhất ≥ now(VN) + 24h.
- `RESUBMIT_REJECTED_REQUEST`: Visitor owner + request `REJECTED` + mọi campus `REJECTED`.
- Pending edit: cho đổi campus (diff theo campus_id — instance WAITING chưa có dữ liệu con nên xóa/thêm an toàn); campus mới phải ACTIVE + có Staff Leader ACTIVE; guest members replace toàn bộ; audit `UPDATE_PENDING_VISIT_REQUEST`.
- Resubmit: campus set phải trùng (`RESUBMIT_CAMPUS_LIST_CHANGED`); audit `RESUBMIT_REJECTED_VISIT_REQUEST` với 3 dòng `audit_log_changes` (`request.status`, `resubmission_count`, `campus_decisions_before_resubmit_json` = snapshot JSON) ghi TRƯỚC khi clear decision/host/cancel fields; 2-phase SaveChanges (request PENDING trước → campuses WAITING sau) trong 1 transaction; coordinator re-route về Staff Leader hiện tại.
- Cancel: Visitor bị chặn khi còn campus active < 24h (`VISIT_CANCEL_WINDOW_EXPIRED`, message đúng spec); Host hủy được ASSIGNED/BEFORE_VISIT tới trước giờ bắt đầu, sau đó `HOST_CANNOT_CANCEL_AFTER_VISIT_STARTED`; DURING/AFTER/CLOSED → `VISIT_ALREADY_STARTED_CANNOT_CANCEL`.
- Agenda: mọi transition sang DURING_VISIT/AFTER_VISIT/CLOSED trong `CompleteVisitStage` đều check `visit_agendas` ≥ 1 dòng.
- Notification (in-app, giống pattern submit): edit → "Visitor đã cập nhật đơn…", resubmit → "Visitor đã gửi lại đơn bị từ chối…" cho Staff Leader các campus liên quan. Email riêng cho 2 luồng này chưa làm (backlog — hệ thống notify in-app là kênh chính của luồng duyệt hiện tại).

## Frontend

- List: nút hiển thị THEO `allowedActions` backend trả (không tự đoán quyền).
- Form edit/resubmit: reuse đúng RegisterInfo/VisitInfo/VisitorList/Contact/Additional section của form public; schema 24h; datetime giữ wall-clock (`slice(0,16)` → datetime-local, gửi lên qua `mapToPayload` như submit).
- Mode resubmit hiển thị banner cam + danh sách lý do từ chối cũ theo campus (`previousDecisions`).
- Route mode xác định theo path (`/edit/` vs `/resubmit/`); nếu trạng thái thật không khớp mode → hiện lỗi + nút quay về danh sách.

## Business rules enforced

1. ✅ Visitor sửa đơn khi PENDING toàn phần + còn ≥ 24h (đổi campus được phép khi pending).
2. ✅ Visitor sửa & gửi lại sau khi bị từ chối toàn bộ (không đổi campus set).
3. ✅ Visitor chỉ hủy trước ≥ 24h (`VISIT_CANCEL_WINDOW_EXPIRED`).
4. ✅ Host chỉ hủy campus instance trước `planned_start_at` (ASSIGNED/BEFORE_VISIT).
5. ✅ Không hủy khi DURING_VISIT / AFTER_VISIT / CLOSED.
6. ✅ DURING_VISIT trở đi bắt buộc có agenda (backend 409 + trigger DB).
7. ✅ Snapshot lý do từ chối cũ vào audit trước khi clear decision fields.

## Tests

- Backend build: **pass** (`dotnet build PEMS.Api` — 0 error, chỉ warning cũ; build ra BaseOutputPath tạm để tránh file-lock dev server).
- Frontend build: **pass** (`npm run build` 0 error, `npm run lint`/tsc 0 error).
- Backend unit tests theo §14.1: **chưa viết** (đề xuất bổ sung theo danh sách test case trong plan ở iteration test).
- Manual tests (§14.2): **chưa chạy** — cần import SQL mới + chạy full stack, test tay theo checklist 11 mục.

## Remaining risks

- DB local phải import đúng file SQL `..._resubmit_agenda_cancel24_FULL_UPDATED.sql` trước khi chạy backend.
- Email nhắc Staff Leader cho 2 luồng edit/resubmit đang là in-app notification (chưa gửi email SMTP riêng).
- Pending edit dùng xóa/tạo lại instance khi bỏ campus: an toàn với schema hiện tại (instance WAITING chưa có bảng con); nếu sau này thêm bảng con tạo từ lúc submit thì cần chuyển sang soft-handling.
- Validator FE/BE mốc 24h dùng giờ máy (server local / browser local) — nhất quán với hành vi 72h hiện có của luồng submit; server production cần chạy timezone VN như hiện tại.
