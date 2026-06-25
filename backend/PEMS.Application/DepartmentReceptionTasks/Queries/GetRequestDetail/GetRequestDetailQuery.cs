using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.DepartmentReceptionTasks.Queries.GetRequestDetail
{
    public class GetRequestDetailQuery : IRequest<RequestDetailDto>
    {
        public ulong LogisticsItemId { get; set; }
    }

    public class AssignmentAttemptDto
    {
        public ulong AttemptId { get; set; }
        public ulong AssigneeUserId { get; set; }
        public string AssigneeName { get; set; }
        public string Status { get; set; }
        public string AssignedAt { get; set; }
        public string RespondedAt { get; set; }
        public string ResponseNote { get; set; }
    }

    public class RequestDetailDto
    {
        public ulong LogisticsItemId { get; set; }
        public string SenderName { get; set; }
        public string RequestedAt { get; set; }
        public string DelegationName { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Date { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public ulong? AssigneeId { get; set; }
        public string AssigneeName { get; set; }
        public string Status { get; set; }
        public string RejectReason { get; set; }
        public string ActionTime { get; set; }
        public string ResponderName { get; set; }
        public ulong VisitInstanceId { get; set; }
        public ulong VisitRequestId { get; set; }

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

        // Assignment history from visit_logistics_assignment_attempts
        public List<AssignmentAttemptDto> AssignmentHistory { get; set; } = new();

        // Latest attempt status for UI state decisions
        public string LatestAttemptStatus { get; set; }
    }

    public class GetRequestDetailQueryHandler : IRequestHandler<GetRequestDetailQuery, RequestDetailDto>
    {
        private readonly IApplicationDbContext _context;

        public GetRequestDetailQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<RequestDetailDto> Handle(GetRequestDetailQuery request, CancellationToken cancellationToken)
        {
            var l = await _context.VisitLogisticsItems
                .Include(l => l.VisitInstance)
                    .ThenInclude(c => c.VisitRequest)
                .FirstOrDefaultAsync(l => l.LogisticsItemId == request.LogisticsItemId && l.Status != "CANCELLED", cancellationToken);

            if (l == null) return null;

            string senderName = "Hệ thống";
            if (l.RequestedBy.HasValue)
            {
                var sender = await _context.Users.FirstOrDefaultAsync(u => u.UserId == l.RequestedBy.Value, cancellationToken);
                if (sender != null) senderName = sender.FullName;
            }

            string assigneeName = "Chưa gán";
            if (l.AssignedToUserId.HasValue)
            {
                var assignee = await _context.Users.FirstOrDefaultAsync(u => u.UserId == l.AssignedToUserId.Value, cancellationToken);
                if (assignee != null) assigneeName = assignee.FullName;
            }

            string responderName = "Hệ thống";
            ulong? responderId = l.ReceivedBy ?? l.UpdatedBy;
            if (responderId.HasValue)
            {
                var responder = await _context.Users.FirstOrDefaultAsync(u => u.UserId == responderId.Value, cancellationToken);
                if (responder != null) responderName = $"{responder.FullName} - {responder.SubRole ?? "Nhân viên"}";
            }

            // Assignment history
            var attempts = await _context.VisitLogisticsAssignmentAttempts
                .Where(a => a.LogisticsItemId == request.LogisticsItemId)
                .OrderBy(a => a.AssignedAt)
                .ToListAsync(cancellationToken);

            var historyDtos = new List<AssignmentAttemptDto>();
            foreach (var att in attempts)
            {
                string attAssigneeName = att.AssigneeUserId.ToString();
                var attUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == att.AssigneeUserId, cancellationToken);
                if (attUser != null) attAssigneeName = attUser.FullName;

                historyDtos.Add(new AssignmentAttemptDto
                {
                    AttemptId = att.AssignmentAttemptId,
                    AssigneeUserId = att.AssigneeUserId,
                    AssigneeName = attAssigneeName,
                    Status = att.Status,
                    AssignedAt = att.AssignedAt.ToString("HH:mm dd/MM/yyyy"),
                    RespondedAt = att.RespondedAt?.ToString("HH:mm dd/MM/yyyy") ?? "",
                    ResponseNote = att.ResponseNote ?? ""
                });
            }

            string latestAttemptStatus = attempts.OrderByDescending(a => a.AssignedAt).FirstOrDefault()?.Status ?? "";

            var camp = l.VisitInstance;

            return new RequestDetailDto
            {
                LogisticsItemId = l.LogisticsItemId,
                SenderName = senderName,
                RequestedAt = l.RequestedAt?.ToString("HH:mm dd-MM-yyyy") ?? "",
                DelegationName = camp.VisitRequest.DelegationName,
                StartTime = l.UsageStartAt?.ToString("HH:mm") ?? "",
                EndTime = l.UsageEndAt?.ToString("HH:mm") ?? "",
                Date = l.UsageStartAt?.ToString("dd-MM-yyyy") ?? "",
                Title = l.Title,
                Description = l.Description ?? "",
                AssigneeId = l.AssignedToUserId,
                AssigneeName = assigneeName,
                Status = l.Status,
                RejectReason = l.Status == "REJECTED" ? l.DecisionNote ?? l.AssigneeResponseNote : "",
                ActionTime = l.UpdatedAt?.ToString("HH:mm:ss dd-MM-yyyy") ?? "",
                ResponderName = responderName,
                VisitInstanceId = camp.VisitInstanceId,
                VisitRequestId = camp.VisitRequestId,

                RegistrantFullName = camp.VisitRequest.RegistrantFullName ?? "",
                RegistrantEmail = camp.VisitRequest.RegistrantEmail ?? "",
                RegistrantPhone = camp.VisitRequest.RegistrantPhone ?? "",
                RegistrantOrganization = camp.VisitRequest.RegistrantOrganization ?? "",
                RegistrantJobTitle = camp.VisitRequest.RegistrantJobTitle ?? "",
                Purpose = camp.VisitRequest.Purpose ?? "",
                WorkingContent = camp.VisitRequest.WorkingContent ?? "",
                ContactPersonFullName = camp.VisitRequest.ContactPersonFullName ?? "",
                ContactPersonPhone = camp.VisitRequest.ContactPersonPhone ?? "",

                AssignmentHistory = historyDtos,
                LatestAttemptStatus = latestAttemptStatus
            };
        }
    }
}
