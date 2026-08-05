using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.CancelVisitRequest;
using PEMS.Application.Delegations.Commands.CompleteVisitStage;
using PEMS.Application.Delegations.Commands.RejectCampusInstance;
using PEMS.Application.Delegations.Commands.SaveVisitAgenda;
using PEMS.Application.Delegations.Commands.UpdateRegistrantInfo;
using PEMS.Application.Delegations.Commands.InviteVisitParticipant;
using PEMS.Application.Delegations.Commands.RemoveVisitParticipant;
using PEMS.Application.Delegations.Commands.UpdateVisitInstancePreparationNote;
using PEMS.Application.Delegations.Commands.SaveVisitInstanceReminderSettings;
using PEMS.Application.Delegations.Commands.CancelVisitInstanceReminderSettings;
using PEMS.Application.Delegations.Queries.GetVisitInstanceReminderSettings;
using PEMS.Application.Delegations.Queries.GetVisitInstanceLogistics;
using PEMS.Application.Delegations.Commands.SignVisitLogisticsHandover;
using PEMS.Application.Delegations.Commands.SaveVisitLogisticsHandoverDocument;
using PEMS.Application.Delegations.Queries.GetVisitInstanceSentEmails;
using PEMS.Application.Delegations.SetupProgressEmail;
using PEMS.Application.Emails.Common;
using PEMS.Application.Delegations.Queries.GetVisitProcessDetail;
using PEMS.Application.Delegations.Queries.GetAgendaResponsibleCandidates;
using PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;
using PEMS.Application.Delegations.Queries.GetHostCandidates;
using PEMS.Application.Delegations.Queries.GetVisitInstanceParticipants;
using PEMS.Application.Delegations.Queries.GetVisitInstanceContribution;
using PEMS.Application.Delegations.Queries.GetParticipantCandidates;
using PEMS.Application.Delegations.Queries.GetSupportDepartments;
using PEMS.Application.Delegations.Queries.GetDepartmentStaffCandidates;
using PEMS.Application.Delegations.Queries.ViewMyVisitInvitations;
using PEMS.Domain.Constants;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Delegation reception — the core visit workflow (UC-17..UC-41, UC-136).
    ///
    /// The controller carried no authorization attribute at all, so approve/reject, host
    /// assignment, participant invite/remove, agenda writes and the setup-progress email send
    /// were all reachable without a token. It is now authenticated, and ADMIN is excluded at
    /// the coarse layer per PERMISSION_MATRIX §5.4 ("ADMIN must stay — for UC-17 to UC-41 and
    /// UC-136, and must not receive indirect visit access through dashboard/search/detail").
    ///
    /// This gate says nothing about WHICH delegation the caller may touch. Every handler still
    /// resolves object scope — current host, accepted participant, instance campus, assigned
    /// department, request owner — so changing visitRequestId or visitInstanceId in the URL
    /// does not widen access.
    /// </summary>
    [ApiController]
    [Authorize]
    [RoleAuthorize(
        EffectiveRole.Ho,
        EffectiveRole.StaffLeader,
        EffectiveRole.Staff,
        EffectiveRole.DepartmentLead,
        EffectiveRole.Department,
        EffectiveRole.Student,
        EffectiveRole.Visitor)]
    [Route("api/[controller]")]
    public class DelegationsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public DelegationsController(IMediator mediator) => _mediator = mediator;

        // UC-17 Submit Visit Request is the public, OTP-verified flow in VisitRequestsController:
        //   POST /api/visit-requests/initiate | /verify | /resend-otp
        // The former DelegationsController.submitvisitrequest scaffold (NotImplementedException)
        // was removed — it was unused and conflicted with the real UC-17.

        // ── HO approve/reject: PERMANENTLY DISABLED (campus-independent approval, SQL v10) ──
        // Multi-campus requests are no longer decided by HO: each campus instance is routed to
        // and decided by the Staff Leader of that campus. The old routes stay registered so a
        // stale client gets an explicit 410 Gone (never a silent 404) and can never mutate data.
        [HttpPost("{visitRequestId}/ho-approve")]
        public IActionResult HoApprove(ulong visitRequestId)
            => StatusCode(StatusCodes.Status410Gone, new
            {
                errorCode = "HO_APPROVAL_FLOW_REMOVED",
                message = "Luồng duyệt liên cơ sở đã thay đổi. Mỗi Staff Leader xử lý campus instance của campus mình; HO không còn duyệt request tổng."
            });

        [HttpPost("{visitRequestId}/ho-reject")]
        public IActionResult HoReject(ulong visitRequestId)
            => StatusCode(StatusCodes.Status410Gone, new
            {
                errorCode = "HO_APPROVAL_FLOW_REMOVED",
                message = "Luồng duyệt liên cơ sở đã thay đổi. Mỗi Staff Leader xử lý campus instance của campus mình; HO không còn duyệt request tổng."
            });

        // ── Submitted visit-request form snapshot (read-only, shared) ────────
        // The "what the guest submitted" detail, reused by the pre-approval review, the
        // approved/waiting-host detail and the rejected detail screens. Role/scope/status
        // visibility is enforced in the handler (HO → MULTI_CAMPUS; Staff Leader → own-campus
        // SINGLE_CAMPUS any status, or own-campus MULTI_CAMPUS only after HO approval; Visitor →
        // own request). Never mutates anything; decisions use the ho-approve / assign-host /
        // reject endpoints above.
        [HttpGet("visit-requests/{visitRequestId}/submitted-form-detail")]
        public async Task<IActionResult> GetSubmittedVisitRequestFormDetail(ulong visitRequestId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.Delegations.Queries.GetSubmittedVisitRequestFormDetail.GetSubmittedVisitRequestFormDetailQuery(visitRequestId),
                cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewguestdelegationdetails")]
        public async Task<IActionResult> ViewGuestDelegationDetails([FromQuery] PEMS.Application.Delegations.Queries.ViewGuestDelegationDetails.ViewGuestDelegationDetailsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewguestdelegationlist")]

        public async Task<IActionResult> ViewGuestDelegationList([FromQuery] PEMS.Application.Delegations.Queries.ViewGuestDelegationList.ViewGuestDelegationListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        // Permission flags for the visit-process detail page (source of truth for tab view/edit).
        // Scope/relation enforced in the handler (403 if the caller has no relation to the instance).
        [HttpGet("visit-instances/{visitInstanceId}/process-permissions")]
        public async Task<IActionResult> GetVisitProcessPermissions(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.Delegations.Queries.GetVisitProcessPermissions.GetVisitProcessPermissionsQuery(visitInstanceId),
                cancellationToken);
            return Ok(result);
        }

        // Contribution Page (spec §5.3 / §7 / §10.3): permission flags + read-only summary +
        // workspace status for participants who contribute results (minutes/media/news). Access is
        // enforced in the handler — Host / ACCEPTED|ASSIGNED participant / Department-with-logistics
        // only; Admin, Visitor, unrelated and not-yet-accepted callers get 403/404.
        [HttpGet("visit-instances/{visitInstanceId}/contribution")]
        public async Task<IActionResult> GetVisitInstanceContribution(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetVisitInstanceContributionQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        [HttpGet("visit-instances/{visitInstanceId}/summary")]
        public async Task<IActionResult> GetVisitInstanceSummary(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PEMS.Application.Delegations.Queries.GetVisitInstanceSummary.GetVisitInstanceSummaryQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchdelegations")]
        public async Task<IActionResult> SearchDelegations([FromQuery] PEMS.Application.Delegations.Queries.SearchDelegations.SearchDelegationsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        // ── UC-22 Staff Leader (campus-independent approval): list host candidates, then
        //    approve + assign host, or reject — always per campus instance of THEIR campus ──
        [HttpGet("campuses/{visitInstanceId}/host-candidates")]

        public async Task<IActionResult> GetHostCandidates(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetHostCandidatesQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        // Approve the campus instance: WAITING_REQUEST_APPROVAL → ASSIGNED with the mandatory
        // official host (IC Staff of the campus, or the approving Staff Leader themself).
        // "assign-host" is kept as a legacy alias of the same action.
        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/approve")]
        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/assign-host")]
        public async Task<IActionResult> ApproveCampusInstance(ulong visitRequestId, ulong visitInstanceId, [FromBody] ApproveCampusInstanceBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new ApproveCampusInstanceCommand(visitRequestId, visitInstanceId, body.HostUserId, body.DecisionNote),
                cancellationToken);
            return Ok(result);
        }

        // Reject ONLY this campus instance (mandatory reason → instance decision_note).
        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/reject")]
        public async Task<IActionResult> RejectCampusInstance(ulong visitRequestId, ulong visitInstanceId, [FromBody] RejectVisitRequestBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new RejectCampusInstanceCommand(visitRequestId, visitInstanceId, body.Reason),
                cancellationToken);
            return Ok(result);
        }

        // ── VisitProcess "Trước tiếp khách": real setup detail + agenda save ──
        // Real data for the process page (agenda + common). Scope enforced in the handler
        // (Host/Staff Leader/HO/Visitor owner/accepted participant; 403 otherwise).
        [HttpGet("{visitRequestId}/campuses/{visitInstanceId}/process-detail")]
        public async Task<IActionResult> GetProcessDetail(ulong visitRequestId, ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetVisitProcessDetailQuery(visitRequestId, visitInstanceId), cancellationToken);
            return Ok(result);
        }

        // "Báo cáo Lịch trình" PDF (đặc tả Prompt_AI_Bao_Cao_Lich_Trinh): same scope rule as
        // process-detail above (403 in the handler otherwise).
        [HttpGet("{visitRequestId}/campuses/{visitInstanceId}/schedule-report/pdf")]
        public async Task<IActionResult> ExportScheduleReportPdf(
            ulong visitRequestId, ulong visitInstanceId, [FromQuery] string? languageCode, CancellationToken cancellationToken)
        {
            var isEnglish = string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase);
            var fileBytes = await _mediator.Send(
                new PEMS.Application.Delegations.Queries.ExportScheduleReport.ExportScheduleReportPdfQuery(
                    visitRequestId, visitInstanceId, isEnglish ? "en" : "vi"),
                cancellationToken);
            var suffix = isEnglish ? "-EN" : "";
            return File(fileBytes, "application/pdf", $"BaoCaoLichTrinh-{visitInstanceId}{suffix}.pdf");
        }

        // ── "Gửi cập nhật chuẩn bị": the Host's manual update to the guest, with the Schedule
        // Report attached. All three routes re-derive host + prep-window from the database, so a
        // handover or a stage change between opening the composer and sending refuses.

        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/setup-progress-email/prepare")]
        public async Task<IActionResult> PrepareSetupProgressEmail(
            ulong visitRequestId, ulong visitInstanceId,
            [FromBody] SetupProgressEmailLanguageBody? body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PrepareVisitSetupProgressEmailCommand(visitRequestId, visitInstanceId, body?.LanguageCode),
                cancellationToken);
            return Ok(result);
        }

        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/setup-progress-email/refresh")]
        public async Task<IActionResult> RefreshSetupProgressEmail(
            ulong visitRequestId, ulong visitInstanceId,
            [FromBody] SetupProgressEmailLanguageBody? body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new RefreshVisitSetupProgressEmailCommand(visitRequestId, visitInstanceId, body?.LanguageCode),
                cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Sends the message the composer is holding. Requires <c>Idempotency-Key</c>: with no draft row to
        /// claim DRAFT → SENT, the reservation is the only thing standing between a double click and a
        /// delegation's guests being mailed twice.
        /// </summary>
        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/setup-progress-email/send")]
        public async Task<IActionResult> SendSetupProgressEmail(
            ulong visitRequestId, ulong visitInstanceId,
            [FromBody] SendVisitSetupProgressEmailCommand command, CancellationToken cancellationToken)
        {
            // The route is authoritative. A body that named a different visit would otherwise send this
            // Host's guards over somebody else's delegation.
            command.VisitRequestId = visitRequestId;
            command.VisitInstanceId = visitInstanceId;

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // Valid "Người phụ trách" candidates for the agenda editor: the active host + ACCEPTED
        // supporting participants of THIS instance only (never the whole-system user list). Scope
        // enforced in the handler (403 if the caller has no relation to the instance).
        [HttpGet("visit-instances/{visitInstanceId}/agenda-responsible-candidates")]
        public async Task<IActionResult> GetAgendaResponsibleCandidates(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetAgendaResponsibleCandidatesQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        // ── VisitProcess "Thành phần tham gia": participant list + invite candidate searches ──
        // The instance's host snapshot + invited supporters (scope enforced in the handler: internal
        // relation only). Used on first load and for cheap refetch after invite/remove/assign.
        [HttpGet("visit-instances/{visitInstanceId}/participants")]
        public async Task<IActionResult> GetVisitInstanceParticipants(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetVisitInstanceParticipantsQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        // Candidate search for the host's invite dropdowns. type = IC_SUPPORT | STUDENT. Each candidate
        // carries schedule-conflict info against the instance window. Host-only (re-validated server-side).
        [HttpGet("visit-instances/{visitInstanceId}/participant-candidates")]
        public async Task<IActionResult> GetParticipantCandidates(
            ulong visitInstanceId, [FromQuery] string type, [FromQuery] string? keyword, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetParticipantCandidatesQuery(visitInstanceId, type, keyword), cancellationToken);
            return Ok(result);
        }

        // GENERAL departments of the instance's campus + their resolved active leader (the real
        // invitee). Host-only (re-validated server-side).
        [HttpGet("visit-instances/{visitInstanceId}/support-departments")]
        public async Task<IActionResult> GetSupportDepartments(
            ulong visitInstanceId, [FromQuery] string? keyword, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetSupportDepartmentsQuery(visitInstanceId, keyword), cancellationToken);
            return Ok(result);
        }

        // Department staff the calling Department Leader may assign (own department/campus). Leader-only.
        [HttpGet("visit-instances/{visitInstanceId}/department-staff-candidates")]
        public async Task<IActionResult> GetDepartmentStaffCandidates(
            ulong visitInstanceId, [FromQuery] string? keyword, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetDepartmentStaffCandidatesQuery(visitInstanceId, keyword), cancellationToken);
            return Ok(result);
        }

        // Host invites a supporting participant (IC_SUPPORT/STUDENT by userId, DEPT_SUPPORT by
        // departmentId → backend resolves the leader). Inserts the participant, sends the invitation
        // email with one-time Accept/Decline tokens, and audits. Host-only (re-validated server-side).
        [HttpPost("visit-instances/{visitInstanceId}/participants/invite")]
        public async Task<IActionResult> InviteVisitParticipant(
            ulong visitInstanceId, [FromBody] InviteVisitParticipantBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new InviteVisitParticipantCommand(visitInstanceId, body.ParticipantType, body.UserId, body.DepartmentId, body.Message, body.EmailOverride),
                cancellationToken);
            return Ok(result);
        }

        // Host withdraws a still-pending (INVITED) participant they invited. Host-only; never the main host.
        [HttpPatch("visit-instances/{visitInstanceId}/participants/{participantId}/remove")]
        public async Task<IActionResult> RemoveVisitParticipant(
            ulong visitInstanceId, ulong participantId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new RemoveVisitParticipantCommand(visitInstanceId, participantId), cancellationToken);
            return Ok(result);
        }
        // NB: Department-Leader staff assignment is served by the existing
        // VisitInvitationsController: POST /api/visit-invitations/{participantId}/assign-department-staff
        // (login required). Candidate list: GET visit-instances/{id}/department-staff-candidates above.

        // Upsert the instance's agenda (Host only, prep window). Saves setup ONLY — does not change stage.
        // Also syncs the campus's planned visit window (plannedStartAt/plannedEndAt), which the Host may
        // have renegotiated with the delegation while drafting the agenda.
        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/agenda")]
        public async Task<IActionResult> SaveVisitAgenda(ulong visitRequestId, ulong visitInstanceId, [FromBody] SaveVisitAgendaBody body, CancellationToken cancellationToken)
        {
            var items = (body.Items ?? new List<SaveVisitAgendaItem>());
            var result = await _mediator.Send(
                new SaveVisitAgendaCommand(visitRequestId, visitInstanceId, items, body.PlannedStartAt, body.PlannedEndAt),
                cancellationToken);
            return Ok(result);
        }

        // ── VisitProcess "Ghi chú chung" (preparation note) — Host only, prep window ──
        // Saves visit_request_campuses.preparation_note. note may be null/empty (clears it).
        [HttpPut("visit-instances/{visitInstanceId}/preparation-note")]
        public async Task<IActionResult> UpdatePreparationNote(
            ulong visitInstanceId, [FromBody] UpdatePreparationNoteBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new UpdateVisitInstancePreparationNoteCommand(visitInstanceId, body?.Note), cancellationToken);
            return Ok(result);
        }

        // ── VisitProcess "Workspace - Media": Upload files cho chuyến thăm ──
        [HttpPost("visit-instances/{visitInstanceId}/media")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(300 * 1024 * 1024)] // 300 MB limit
        public async Task<IActionResult> UploadVisitMedia(ulong visitInstanceId, List<IFormFile> files, CancellationToken cancellationToken)
        {
            var command = new PEMS.Application.Delegations.Commands.UploadVisitPhotos.UploadVisitPhotosCommand
            {
                VisitInstanceId = visitInstanceId,
                Files = new List<PEMS.Application.Delegations.Commands.UploadVisitPhotos.UploadVisitPhotoFileDto>()
            };

            if (files != null)
            {
                foreach (var file in files)
                {
                    if (file.Length == 0) continue;
                    using var ms = new System.IO.MemoryStream();
                    await file.CopyToAsync(ms, cancellationToken);
                    command.Files.Add(new PEMS.Application.Delegations.Commands.UploadVisitPhotos.UploadVisitPhotoFileDto(
                        ms.ToArray(),
                        file.FileName,
                        file.ContentType
                    ));
                }
            }

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // ── VisitProcess "Cảnh báo & Thông báo" (scheduled reminder settings) — Host only, prep window ──
        // GET loads the saved schedule rows; PUT upserts the full set (enabled=false cancels a PENDING row);
        // PATCH .../cancel cancels every PENDING row. Nothing is sent on save — dispatch is a background job.
        [HttpGet("visit-instances/{visitInstanceId}/reminder-settings")]
        public async Task<IActionResult> GetReminderSettings(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetVisitInstanceReminderSettingsQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        [HttpPut("visit-instances/{visitInstanceId}/reminder-settings")]
        public async Task<IActionResult> SaveReminderSettings(
            ulong visitInstanceId, [FromBody] SaveReminderSettingsBody body, CancellationToken cancellationToken)
        {
            var items = (body?.Items ?? new List<SaveVisitReminderSettingItem>());
            var result = await _mediator.Send(
                new SaveVisitInstanceReminderSettingsCommand(visitInstanceId, items), cancellationToken);
            return Ok(result);
        }

        [HttpPatch("visit-instances/{visitInstanceId}/reminder-settings/cancel")]
        public async Task<IActionResult> CancelReminderSettings(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new CancelVisitInstanceReminderSettingsCommand(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        // VisitProcess "Chuẩn bị chi tiết": the campus instance's logistics/resource requests (read).
        [HttpGet("visit-instances/{visitInstanceId}/logistics")]
        public async Task<IActionResult> GetVisitInstanceLogistics(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetVisitInstanceLogisticsQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        // Host soft-cancels one of their logistics requests (e.g. "Không cần màn LED"). Sets CANCELLED
        // and stores the required cancellation reason into decision_note (validated in the handler).
        [HttpPatch("visit-instances/{visitInstanceId}/logistics/{logisticsItemId}/cancel")]
        public async Task<IActionResult> CancelVisitLogisticsItem(ulong visitInstanceId, ulong logisticsItemId, [FromBody] CancelLogisticsItemBody? body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.Delegations.Commands.CancelVisitLogisticsItem.CancelVisitLogisticsItemCommand(visitInstanceId, logisticsItemId, body?.Reason),
                cancellationToken);
            return Ok(result);
        }

        public sealed class CancelLogisticsItemBody
        {
            public string? Reason { get; set; }
        }

        // ── "Xem mail đã gửi": the sent-email history for one target of a campus instance ─────
        // Read-only; the handler scopes to the instance (host / campus Staff Leader / HO) and never
        // returns another instance's emails. Joins sent_emails + sent_email_recipients + email_templates.
        [HttpGet("visit-instances/{visitInstanceId}/participants/{participantId}/sent-emails")]
        public async Task<IActionResult> GetParticipantSentEmails(ulong visitInstanceId, ulong participantId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new GetVisitInstanceSentEmailsQuery(visitInstanceId, EmailActionTargetTypes.VisitParticipant, participantId),
                cancellationToken);
            return Ok(result);
        }

        [HttpGet("visit-instances/{visitInstanceId}/logistics/{logisticsItemId}/sent-emails")]
        public async Task<IActionResult> GetLogisticsSentEmails(ulong visitInstanceId, ulong logisticsItemId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new GetVisitInstanceSentEmailsQuery(visitInstanceId, EmailActionTargetTypes.LogisticsItem, logisticsItemId),
                cancellationToken);
            return Ok(result);
        }

        // VisitProcess "Đang/Sau tiếp khách": Host signs the BORROWER side of a borrow/return handover.
        // Host-only & status re-validated in the handler (non-host → 403, invalid state → 409/422).
        [HttpPost("visit-instances/{visitInstanceId}/logistics/{logisticsItemId}/handovers/sign-borrower")]
        public async Task<IActionResult> SignVisitLogisticsHandover(
            ulong visitInstanceId, ulong logisticsItemId, [FromBody] SignVisitLogisticsHandoverBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new SignVisitLogisticsHandoverCommand(visitInstanceId, logisticsItemId, body.HandoverType, body.ItemCondition, body.Note, body.ChecklistJson),
                cancellationToken);
            return Ok(result);
        }

        public sealed class SignVisitLogisticsHandoverBody
        {
            public string HandoverType { get; set; } = default!;   // BORROW | RETURN
            public string? ItemCondition { get; set; }
            public string? Note { get; set; }
            public string? ChecklistJson { get; set; }
        }

        // "Lưu vào hệ thống" — generates a real PDF of the fully-signed handover, uploads it to the
        // delegation's Drive "Hậu cần" folder, and creates/updates the matching documents row.
        // visitInstanceId is kept in the route only for URL consistency with the sibling sign route
        // above — the command re-derives everything it needs from logisticsItemId server-side.
        [HttpPost("visit-instances/{visitInstanceId}/logistics/{logisticsItemId}/handovers/{handoverType}/save-document")]
        public async Task<IActionResult> SaveVisitLogisticsHandoverDocument(
            ulong visitInstanceId, ulong logisticsItemId, string handoverType, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new SaveVisitLogisticsHandoverDocumentCommand { LogisticsItemId = logisticsItemId, HandoverType = handoverType },
                cancellationToken);
            return Ok(result);
        }

        // ── Operational reception stage transitions (Host only) ──────────────
        // Trước → Đang (complete preparation), Đang → Sau (complete visit), Sau → Đóng (close).
        // Status/scope re-validated in the handler (invalid status → 409, non-host → 403).
        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/process/complete-before-visit")]
        public async Task<IActionResult> CompleteBeforeVisit(ulong visitRequestId, ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CompleteVisitStageCommand(visitRequestId, visitInstanceId, VisitStageKeys.Before), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/process/complete-during-visit")]
        public async Task<IActionResult> CompleteDuringVisit(ulong visitRequestId, ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CompleteVisitStageCommand(visitRequestId, visitInstanceId, VisitStageKeys.During), cancellationToken);
            return Ok(result);
        }

        // §10: body tùy chọn { confirmNoNews } — Host xác nhận chuyến này không cần bài tin tức
        // (một trong các điều kiện đóng đoàn). Body rỗng vẫn hợp lệ (confirmNoNews = false).
        public sealed class CompleteAfterVisitRequest
        {
            public bool ConfirmNoNews { get; set; }
        }

        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/process/complete-after-visit")]
        public async Task<IActionResult> CompleteAfterVisit(ulong visitRequestId, ulong visitInstanceId, [FromBody] CompleteAfterVisitRequest? body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CompleteVisitStageCommand(visitRequestId, visitInstanceId, VisitStageKeys.After, body?.ConfirmNoNews ?? false), cancellationToken);
            return Ok(result);
        }

        // Update the registrant block of an internally-created request (creator only; scope/status
        // enforced in the handler → 403/409). Visitor-submitted requests are read-only here.
        [HttpPost("{visitRequestId}/registrant-info")]
        public async Task<IActionResult> UpdateRegistrantInfo(ulong visitRequestId, [FromBody] UpdateRegistrantInfoBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new UpdateRegistrantInfoCommand(visitRequestId, body.FullName, body.Organization, body.JobTitle, body.Phone, body.Email),
                cancellationToken);
            return Ok(result);
        }

        // NB: the old request-level "{visitRequestId}/campus-reject" endpoint was removed —
        // rejection is per campus instance now: POST {visitRequestId}/campuses/{visitInstanceId}/reject.

        [HttpPost("createguestdelegation")]
        public async Task<IActionResult> CreateGuestDelegation([FromBody] PEMS.Application.Delegations.Commands.CreateGuestDelegation.CreateGuestDelegationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updateguestdelegation")]
        public async Task<IActionResult> UpdateGuestDelegation([FromBody] PEMS.Application.Delegations.Commands.UpdateGuestDelegation.UpdateGuestDelegationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("preparevisitlogistics")]
        public async Task<IActionResult> PrepareVisitLogistics([FromBody] PEMS.Application.Delegations.Commands.PrepareVisitLogistics.PrepareVisitLogisticsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updatevisitlogistics")]
        public async Task<IActionResult> UpdateVisitLogistics([FromBody] PEMS.Application.Delegations.Commands.UpdateVisitLogistics.UpdateVisitLogisticsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("confirmparticipation")]
        public async Task<IActionResult> ConfirmParticipation([FromBody] PEMS.Application.Delegations.Commands.ConfirmParticipation.ConfirmParticipationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // ── UC-27 Confirm Participation: an invitee's own participation invitations ─────
        // The pending invitations to respond to (and optionally the responded history).
        [HttpGet("my-invitations")]

        public async Task<IActionResult> GetMyInvitations([FromQuery] bool includeResponded, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ViewMyVisitInvitationsQuery { IncludeResponded = includeResponded }, cancellationToken);
            return Ok(result);
        }

        // A single invitation for the invitation-detail screen (ownership enforced server-side).
        [HttpGet("invitations/{participantId}")]

        public async Task<IActionResult> GetInvitation(ulong participantId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetVisitInvitationByIdQuery(participantId), cancellationToken);
            return Ok(result);
        }

        // Accept / decline an invitation (decline requires a reason). Accepting makes the row
        // appear in the "Đơn mời tham dự" tab; this endpoint is NOT on that tab.
        [HttpPost("participants/{participantId}/respond")]

        public async Task<IActionResult> RespondInvitation(ulong participantId, [FromBody] RespondInvitationBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new RespondVisitParticipantInvitationCommand(participantId, body.Accept, body.DeclineReason), cancellationToken);
            return Ok(result);
        }

        [HttpPost("approveresourcerequest")]
        public async Task<IActionResult> ApproveResourceRequest([FromBody] PEMS.Application.Delegations.Commands.ApproveResourceRequest.ApproveResourceRequestCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("proposeresourcemodification")]
        public async Task<IActionResult> ProposeResourceModification([FromBody] PEMS.Application.Delegations.Commands.ProposeResourceModification.ProposeResourceModificationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("confirmthechangeproposal")]
        public async Task<IActionResult> ConfirmTheChangeProposal([FromBody] PEMS.Application.Delegations.Commands.ConfirmTheChangeProposal.ConfirmTheChangeProposalCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createmeetingminutes")]
        public async Task<IActionResult> CreateMeetingMinutes([FromBody] PEMS.Application.Delegations.Commands.CreateMeetingMinutes.CreateMeetingMinutesCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("editmeetingminutes")]
        public async Task<IActionResult> EditMeetingMinutes([FromBody] PEMS.Application.Delegations.Commands.EditMeetingMinutes.EditMeetingMinutesCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewmeetingminutesdetails")]
        public async Task<IActionResult> ViewMeetingMinutesDetails([FromQuery] PEMS.Application.Delegations.Queries.ViewMeetingMinutesDetails.ViewMeetingMinutesDetailsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("uploadattacheddocuments")]
        public async Task<IActionResult> UploadAttachedDocuments([FromBody] PEMS.Application.Delegations.Commands.UploadAttachedDocuments.UploadAttachedDocumentsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("submitdelegationfeedback")]
        public async Task<IActionResult> SubmitDelegationFeedback([FromBody] PEMS.Application.Delegations.Commands.SubmitDelegationFeedback.SubmitDelegationFeedbackCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("scanbusinesscard")]
        public async Task<IActionResult> ScanBusinessCard([FromBody] PEMS.Application.Delegations.Commands.ScanBusinessCard.ScanBusinessCardCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createpartnerprofile")]
        public async Task<IActionResult> CreatePartnerProfile([FromBody] PEMS.Application.Delegations.Commands.CreatePartnerProfile.CreatePartnerProfileCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("uploadvisitphotos")]
        public async Task<IActionResult> UploadVisitPhotos([FromBody] PEMS.Application.Delegations.Commands.UploadVisitPhotos.UploadVisitPhotosCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("tagfacesonphotos")]
        public async Task<IActionResult> TagFacesonPhotos([FromBody] PEMS.Application.Delegations.Commands.TagFacesonPhotos.TagFacesonPhotosCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createnewsarticle")]
        public async Task<IActionResult> CreateNewsArticle([FromBody] PEMS.Application.Delegations.Commands.CreateNewsArticle.CreateNewsArticleCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("closedelegation")]
        public async Task<IActionResult> CloseDelegation([FromBody] PEMS.Application.Delegations.Commands.CloseDelegation.CloseDelegationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // ── UC-136 Cancel Visit Request (post-approval only) ──────────────────
        // Cancel the whole approved request (Visitor self-cancel, or Staff Leader / HO).
        [HttpPost("{visitRequestId}/cancel")]

        public async Task<IActionResult> CancelVisitRequest(
            ulong visitRequestId,
            [FromBody] CancelVisitRequestBody body,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CancelVisitRequestCommand(visitRequestId, null, body.CancellationReason), cancellationToken);
            return Ok(result);
        }

        // Cancel a single campus instance (current Host, after external confirmation from the guest).
        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/cancel")]

        public async Task<IActionResult> CancelVisitRequestCampus(
            ulong visitRequestId,
            ulong visitInstanceId,
            [FromBody] CancelVisitRequestBody body,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CancelVisitRequestCommand(visitRequestId, visitInstanceId, body.CancellationReason), cancellationToken);
            return Ok(result);
        }
    }

    /// <summary>Request body for the UC-136 cancel endpoints.</summary>
    public sealed record CancelVisitRequestBody(string CancellationReason);

    /// <summary>Request body for the UC-22 campus-instance reject endpoint (reason is mandatory).</summary>
    public sealed record RejectVisitRequestBody(string Reason);

    /// <summary>Request body for the UC-22 approve-campus-instance endpoint: the official host is
    /// mandatory; the decision note is optional. Gán host không gửi email — Staff được gán xem
    /// qua thông báo/"Lịch của tôi".</summary>
    public sealed record ApproveCampusInstanceBody(ulong HostUserId, string? DecisionNote);

    /// <summary>Request body for updating an internally-created request's registrant block.</summary>
    public sealed record UpdateRegistrantInfoBody(string FullName, string Organization, string? JobTitle, string Phone, string Email);

    /// <summary>Request body for upserting a campus instance's agenda (full replace).</summary>
    public sealed record SaveVisitAgendaBody(List<SaveVisitAgendaItem>? Items, System.DateTime PlannedStartAt, System.DateTime PlannedEndAt);

    /// <summary>Request body for the UC-27 respond-to-invitation endpoint (decline requires a reason).</summary>
    public sealed record RespondInvitationBody(bool Accept, string? DeclineReason);

    /// <summary>Request body for inviting a supporting participant. userId is required for
    /// IC_SUPPORT/STUDENT; departmentId for DEPT_SUPPORT (backend resolves the leader). emailOverride
    /// carries the host-edited subject/body from the "Xem trước email" modal (optional).</summary>
    public sealed record InviteVisitParticipantBody(
        string ParticipantType, ulong? UserId, ulong? DepartmentId, string? Message, EmailOverride? EmailOverride);

    /// <summary>Request body for saving the host's "Ghi chú chung" (preparation note). Null clears it.</summary>
    public sealed record UpdatePreparationNoteBody(string? Note);

    /// <summary>Request body for upserting the reminder schedule (full set; enabled=false cancels a row).</summary>
    public sealed record SaveReminderSettingsBody(List<SaveVisitReminderSettingItem>? Items);

    /// <summary>
    /// The only thing "mở soạn thư" and "đồng bộ" need from the client. An omitted body means Vietnamese.
    ///
    /// <para>
    /// It used to also carry <c>reuseExistingDraft</c>, which existed so a second click did not leave a
    /// second draft row and a second archived PDF behind. Nothing is persisted now, so a second click
    /// simply renders again.
    /// </para>
    /// </summary>
    public sealed record SetupProgressEmailLanguageBody(string? LanguageCode);
}
