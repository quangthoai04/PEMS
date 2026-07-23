using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
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
        public string ContactPersonFullName { get; set; }
        public string ContactPersonPhone { get; set; }
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
            var p = await _context.VisitParticipants
                .Include(p => p.VisitInstance)
                    .ThenInclude(c => c.VisitRequest)
                .FirstOrDefaultAsync(p => p.ParticipantId == request.ParticipantId && p.Status != "REMOVED", cancellationToken);

            if (p == null) return null;

            string senderName = "Hệ thống";
            if (p.InvitedBy.HasValue)
            {
                var sender = await _context.Users.FirstOrDefaultAsync(u => u.UserId == p.InvitedBy.Value, cancellationToken);
                if (sender != null) senderName = sender.FullName;
            }

            string responderName = "Người dùng";
            var responder = await _context.Users.FirstOrDefaultAsync(u => u.UserId == p.UserId, cancellationToken);
            if (responder != null) responderName = $"{responder.FullName} - {responder.SubRole ?? "Chuyên viên"}";

            var camp = p.VisitInstance;
            var unifiedStatus = NormalizeStatus(p.Status, p.AssignedBy != null, camp.Status, camp.PlannedStartAt, camp.PlannedEndAt);

            // ── Per-campus form v2 (INSTANCE-LEVEL: this invitation detail is keyed by a participant bound to
            // exactly ONE campus instance — camp.VisitInstanceId — so a MIXED request still returns 200,
            // sourcing the delegation name, the working content/purpose and the operational (contact-person)
            // fields ONLY from THAT target instance's per-campus detail, never the global fields and never a
            // sibling campus. Registrant identity fields stay request-level in both versions. v1 keeps the
            // global projection, byte-identical. The participant is already scoped to one instance. ──
            var visit = camp.VisitRequest;
            var content = await _formReadService.ResolveCampusFormContentAsync(
                visit, new[] { camp.VisitInstanceId }, cancellationToken);
            var d = content[camp.VisitInstanceId];
            string delegationName = d.DelegationName;
            string purpose = d.Purpose ?? "";
            string workingContent = d.WorkingContent ?? "";
            // OPERATIONAL contact of this campus — deliberately NOT the request-level primary contact.
            string contactPersonFullName = d.OperationalContact.FullName ?? "";
            string contactPersonPhone = d.OperationalContact.Phone ?? "";

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
                ContactPersonFullName = contactPersonFullName,
                ContactPersonPhone = contactPersonPhone
            };
        }

        private static string NormalizeStatus(string status, bool isStaffAssignment, string instanceStatus, System.DateTime startAt, System.DateTime endAt)
        {
            var now = VietnamTime.Now();
            if (instanceStatus == "CANCELLED") return "CANCELLED";
            if (status == ParticipantStatuses.Invited) return "REQUESTED";
            if (status == ParticipantStatuses.Assigned) return "ASSIGNED";
            if (status == ParticipantStatuses.Declined) return "REJECTED";
            if (status == ParticipantStatuses.Accepted)
            {
                if (instanceStatus == "CLOSED" || now > endAt) return "DONE";
                if (now >= startAt && now <= endAt) return "IN_PROGRESS";
                return "ACCEPTED";
            }
            return status;
        }
    }
}
