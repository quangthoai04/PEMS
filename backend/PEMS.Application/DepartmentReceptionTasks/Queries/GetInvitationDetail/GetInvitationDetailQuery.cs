using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using PEMS.Application.Common;
namespace PEMS.Application.DepartmentReceptionTasks.Queries.GetInvitationDetail
{
    public class GetInvitationDetailQuery : IRequest<InvitationDetailDto>
    {
        public ulong ParticipantId { get; set; }
    }

    public class InvitationDetailDto
    {
        public ulong ParticipantId { get; set; }
        public string SenderName { get; set; }
        public string InvitedAt { get; set; }
        public string DelegationName { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Date { get; set; }
        public string Note { get; set; }
        public string Status { get; set; }
        public string RejectReason { get; set; }
        public string ActionTime { get; set; }
        public string ResponderName { get; set; }
        public ulong VisitInstanceId { get; set; }
        public ulong VisitRequestId { get; set; }
        public string? CancelReason { get; set; }
        
        // Full Details
        public string RegistrantFullName { get; set; }
        public string RegistrantEmail { get; set; }
        public string RegistrantPhone { get; set; }
        public string RegistrantOrganization { get; set; }
        public string RegistrantJobTitle { get; set; }
        public string Purpose { get; set; }
        public string WorkingContent { get; set; }
        /// <summary>
        /// "Ghi chú gửi FPTU" — the guest's one general remark about THIS campus, from
        /// visit_instance_form_details.notes. Named apart from <see cref="Note"/>, which is the
        /// invitation's own text (and, on a declined row, the reason): a department reading one as
        /// the other would act on the wrong sentence.
        /// </summary>
        public string NotesToFptu { get; set; }
        // The OPERATIONAL contact of THIS campus, all five fields. They used to be named
        // ContactPerson*, after the request-level contact that no longer exists, and carried only
        // name and phone — so a department read the right person under the wrong name and could not
        // tell whether they were talking to somebody who could decide.
        public string OperationalContactFullName { get; set; }
        public string OperationalContactOrganization { get; set; }
        public string OperationalContactJobTitle { get; set; }
        public string OperationalContactPhone { get; set; }
        public string OperationalContactEmail { get; set; }
    }

    public class GetInvitationDetailQueryHandler : IRequestHandler<GetInvitationDetailQuery, InvitationDetailDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IVisitFormReadService _formReadService;

        public GetInvitationDetailQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService, IVisitFormReadService formReadService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _formReadService = formReadService;
        }

        public async Task<InvitationDetailDto> Handle(GetInvitationDetailQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
                throw new ForbiddenException();

            var p = await _context.VisitParticipants
                .Include(p => p.VisitInstance)
                    .ThenInclude(c => c.VisitRequest)
                .FirstOrDefaultAsync(p => p.ParticipantId == request.ParticipantId && p.Status != "REMOVED", cancellationToken);

            if (p == null) return null;

            string responderName = "Người dùng";
            var responder = await _context.Users.FirstOrDefaultAsync(u => u.UserId == p.UserId, cancellationToken);
            if (responder != null) responderName = $"{responder.FullName} - {responder.SubRole ?? "Chuyên viên"}";

            // The invitee may always view their own invitation, regardless of role or department — the
            // same ownership rule VisitInvitationResponse.ApplyCoreAsync already applies to answering it
            // (accept/decline), so a Host/Student/IC-support invitee with no DepartmentId at all must not
            // lose read access here either. On top of that, this module's own list
            // (GetAssignmentsProgressListQueryHandler, ItemType=INVITATION) additionally grants a
            // Department LEADER oversight of every invitation addressed to their own department's
            // roster — a Department STAFF member gets no such extra grant, self-view only.
            bool isSelf = p.UserId == _currentUserService.UserId.Value;
            bool isDepartmentLeaderOversight =
                !isSelf
                && responder != null
                && _currentUserService.DepartmentId is not null
                && responder.DepartmentId == _currentUserService.DepartmentId.Value
                && string.Equals(_currentUserService.RoleCode, RoleCodes.Department, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(_currentUserService.SubRole, UserSubRoles.Staff, StringComparison.OrdinalIgnoreCase);
            if (!isSelf && !isDepartmentLeaderOversight)
                throw new ForbiddenException("Không có quyền xem lời mời của người khác");

            string senderName = "Hệ thống";
            if (p.InvitedBy.HasValue)
            {
                var sender = await _context.Users.FirstOrDefaultAsync(u => u.UserId == p.InvitedBy.Value, cancellationToken);
                if (sender != null) senderName = sender.FullName;
            }

            var camp = p.VisitInstance;
            var unifiedStatus = NormalizeStatus(p.Status, p.AssignedBy != null, camp.Status, camp.PlannedStartAt, camp.PlannedEndAt);

            // ── INSTANCE-LEVEL: this invitation detail is keyed by a participant bound to exactly ONE campus
            // instance — camp.VisitInstanceId — so a request whose campuses differ still returns 200, sourcing
            // the delegation name, the working content/purpose and the operational (contact-person) fields
            // ONLY from THAT target instance's detail, never a sibling campus and never the request row, which
            // holds no form content. Registrant identity fields are genuinely request-level and stay there.
            // The participant is already scoped to one instance. ──
            var visit = camp.VisitRequest;
            var content = await _formReadService.ResolveCampusFormContentAsync(
                visit, new[] { camp.VisitInstanceId }, cancellationToken);
            var d = content[camp.VisitInstanceId];
            string delegationName = d.DelegationName;
            string purpose = d.Purpose ?? "";
            string workingContent = d.WorkingContent ?? "";
            // The guest's remark to FPTU about THIS campus — NOT p.Note, which belongs to the invitation.
            string notesToFptu = d.Notes ?? "";
            // OPERATIONAL contact of this campus — deliberately NOT the request-level primary contact.
            var opContact = d.OperationalContact;

            return new InvitationDetailDto
            {
                ParticipantId = p.ParticipantId,
                SenderName = senderName,
                InvitedAt = p.InvitedAt?.ToString("HH:mm dd-MM-yyyy") ?? "",
                DelegationName = delegationName,
                StartTime = camp.PlannedStartAt.ToString("HH:mm"),
                EndTime = camp.PlannedEndAt.ToString("HH:mm"),
                Date = camp.PlannedStartAt.ToString("dd-MM-yyyy"),
                Note = p.Note ?? "Trân trọng kính mời anh/chị tham gia tiếp đón đoàn khách.",
                Status = unifiedStatus,
                RejectReason = p.Status == "DECLINED" ? p.Note : "",
                ActionTime = p.RespondedAt?.ToString("HH:mm:ss dd-MM-yyyy") ?? "",
                ResponderName = responderName,
                VisitInstanceId = camp.VisitInstanceId,
                VisitRequestId = camp.VisitRequestId,
                CancelReason = camp.CancellationReason ?? camp.VisitRequest.CancellationReason,
                
                RegistrantFullName = camp.VisitRequest.RegistrantFullName ?? "",
                RegistrantEmail = camp.VisitRequest.RegistrantEmail ?? "",
                RegistrantPhone = camp.VisitRequest.RegistrantPhone ?? "",
                RegistrantOrganization = camp.VisitRequest.RegistrantOrganization ?? "",
                RegistrantJobTitle = camp.VisitRequest.RegistrantJobTitle ?? "",
                Purpose = purpose,
                WorkingContent = workingContent,
                NotesToFptu = notesToFptu,
                OperationalContactFullName = opContact.FullName ?? "",
                OperationalContactOrganization = opContact.Organization ?? "",
                OperationalContactJobTitle = opContact.JobTitle ?? "",
                OperationalContactPhone = opContact.Phone ?? "",
                OperationalContactEmail = opContact.Email ?? ""
            };
        }

        private static string NormalizeStatus(string status, bool isStaffAssignment, string instanceStatus, System.DateTime startAt, System.DateTime endAt)
        {
            var now = VietnamTime.Now();
            if (instanceStatus == "CANCELLED") return "CANCELLED";
            if (status == ParticipantStatuses.Declined) return "REJECTED";
            // Once the reception window has closed, the task is over regardless of whether this row
            // ever got an explicit Accept — a delegated or never-answered invitation for a visit that
            // has already happened is not still pending; it is history.
            if (instanceStatus == "CLOSED" || now > endAt) return "DONE";
            if (status == ParticipantStatuses.Invited) return "REQUESTED";
            if (status == ParticipantStatuses.Assigned) return "ASSIGNED";
            if (status == ParticipantStatuses.Accepted)
            {
                if (now >= startAt && now <= endAt) return "IN_PROGRESS";
                return "ACCEPTED";
            }
            return status;
        }
    }
}
