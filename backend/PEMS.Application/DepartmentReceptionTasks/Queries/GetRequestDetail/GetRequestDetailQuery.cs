using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
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
        public int Quantity { get; set; }
        /// <summary>Loại hạng mục hậu cần: ROOM | TRANSPORT | MEAL | EQUIPMENT | BANNER | LED | OTHER.
        /// TRANSPORT dùng để hiện biên bản bàn giao xe ô tô điện với checklist cố định.</summary>
        public string ItemType { get; set; }
        public string? Priority { get; set; }   // LOW | MEDIUM | HIGH | URGENT
        public string? DueAt { get; set; }      // "yyyy-MM-ddTHH:mm:ss" wall-clock, null if none
        public ulong? AssigneeId { get; set; }
        public string AssigneeName { get; set; }
        public string Status { get; set; }
        public string RejectReason { get; set; }
        public string ActionTime { get; set; }
        public string ResponderName { get; set; }
        public ulong VisitInstanceId { get; set; }
        public ulong VisitRequestId { get; set; }
        public string? CancelReason { get; set; }
        public string? ProposedUsageStartAt { get; set; }
        public string? ProposedUsageEndAt { get; set; }
        public string? ProposedDescription { get; set; }
        public string? ProposedAt { get; set; }
        public string? ProposedByName { get; set; }
        public string? ProposedByRole { get; set; }
        public string? ProposalNote { get; set; }
        public string? ProposalResponse { get; set; }
        public string? ProposalRespondedAt { get; set; }
        public string? ProposalRespondedByName { get; set; }
        public string? ProposalRespondedByRole { get; set; }
        public string? ProposalResponseNote { get; set; }
        public HandoverSignatureDto? BorrowProviderSignature { get; set; }
        public HandoverSignatureDto? BorrowBorrowerSignature { get; set; }
        public HandoverSignatureDto? ReturnProviderSignature { get; set; }
        public HandoverSignatureDto? ReturnBorrowerSignature { get; set; }
        public string? BorrowNote { get; set; }
        public string? ReturnNote { get; set; }
        /// <summary>JSON checklist xe điện (TRANSPORT) — null nếu đơn không phải TRANSPORT hoặc chưa ai lưu.</summary>
        public string? ChecklistJson { get; set; }

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

    public class HandoverSignatureDto
    {
        public ulong? UserId { get; set; }
        public string? Name { get; set; }
        public string? SignedAt { get; set; }
    }

    public class GetRequestDetailQueryHandler : IRequestHandler<GetRequestDetailQuery, RequestDetailDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IVisitFormReadService _formReadService;

        public GetRequestDetailQueryHandler(IApplicationDbContext context, IVisitFormReadService formReadService)
        {
            _context = context;
            _formReadService = formReadService;
        }

        public async Task<RequestDetailDto> Handle(GetRequestDetailQuery request, CancellationToken cancellationToken)
        {
            var l = await _context.VisitLogisticsItems
                .Include(l => l.VisitInstance)
                    .ThenInclude(c => c.VisitRequest)
                .FirstOrDefaultAsync(l => l.LogisticsItemId == request.LogisticsItemId, cancellationToken);

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
            string? proposedByName = null;
            string? proposedByRole = null;
            if (l.ProposedBy.HasValue)
            {
                var proposedBy = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserId == l.ProposedBy.Value, cancellationToken);
                proposedByName = proposedBy?.FullName;
                proposedByRole = proposedBy == null ? null : $"{proposedBy.Role?.RoleCode ?? ""}{(string.IsNullOrWhiteSpace(proposedBy.SubRole) ? "" : $" - {proposedBy.SubRole}")}";
            }

            string? responseByName = null;
            string? responseByRole = null;
            if (l.ProposalRespondedBy.HasValue)
            {
                var responseBy = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserId == l.ProposalRespondedBy.Value, cancellationToken);
                responseByName = responseBy?.FullName;
                responseByRole = responseBy == null ? null : $"{responseBy.Role?.RoleCode ?? ""}{(string.IsNullOrWhiteSpace(responseBy.SubRole) ? "" : $" - {responseBy.SubRole}")}";
            }

            var borrowSigned = await _context.VisitLogisticsItemHandovers.AnyAsync(h =>
                h.LogisticsItemId == l.LogisticsItemId
                && h.HandoverType == "BORROW"
                && h.BorrowerSignedAt != null
                && h.ProviderSignedAt != null,
                cancellationToken);
            var returnSigned = await _context.VisitLogisticsItemHandovers.AnyAsync(h =>
                h.LogisticsItemId == l.LogisticsItemId
                && h.HandoverType == "RETURN"
                && h.BorrowerSignedAt != null
                && h.ProviderSignedAt != null,
                cancellationToken);
            var unifiedStatus = NormalizeStatus(l.Status, camp.Status, camp.VisitRequest.Status, borrowSigned, returnSigned);
            var handovers = await _context.VisitLogisticsItemHandovers
                .Where(h => h.LogisticsItemId == l.LogisticsItemId)
                .ToListAsync(cancellationToken);
            var signerIds = handovers
                .SelectMany(h => new[] { h.BorrowerSignedBy, h.ProviderSignedBy })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            var signerNames = await _context.Users
                .Where(u => signerIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);
            var borrowHandover = handovers.FirstOrDefault(h => h.HandoverType == "BORROW");
            var returnHandover = handovers.FirstOrDefault(h => h.HandoverType == "RETURN");

            // ── INSTANCE-LEVEL: this reception detail is keyed by a logistics item that belongs to exactly
            // ONE campus instance — camp.VisitInstanceId — so a request whose campuses differ still returns
            // 200, sourcing the delegation name, the working content/purpose and the operational
            // (contact-person) fields ONLY from THAT target instance's detail, never a sibling campus and
            // never the request row, which holds no form content. Registrant identity fields are genuinely
            // request-level and stay there. The item is already scoped to one instance. ──
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

            return new RequestDetailDto
            {
                LogisticsItemId = l.LogisticsItemId,
                SenderName = senderName,
                RequestedAt = l.RequestedAt?.ToString("HH:mm dd-MM-yyyy") ?? "",
                DelegationName = delegationName,
                StartTime = l.UsageStartAt?.ToString("HH:mm") ?? "",
                EndTime = l.UsageEndAt?.ToString("HH:mm") ?? "",
                Date = l.UsageStartAt?.ToString("dd-MM-yyyy") ?? "",
                Title = l.Title,
                Description = l.Description ?? "",
                Quantity = l.Quantity ?? 1,
                ItemType = l.ItemType,
                Priority = l.Priority,
                DueAt = l.DueAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
                AssigneeId = l.AssignedToUserId,
                AssigneeName = assigneeName,
                Status = unifiedStatus,
                RejectReason = l.Status == "REJECTED" ? l.DecisionNote ?? l.AssigneeResponseNote : "",
                ActionTime = l.UpdatedAt?.ToString("HH:mm:ss dd-MM-yyyy") ?? "",
                ResponderName = responderName,
                VisitInstanceId = camp.VisitInstanceId,
                VisitRequestId = camp.VisitRequestId,
                CancelReason = camp.CancellationReason ?? camp.VisitRequest.CancellationReason,
                ProposedUsageStartAt = l.ProposedUsageStartAt?.ToString("O"),
                ProposedUsageEndAt = l.ProposedUsageEndAt?.ToString("O"),
                ProposedDescription = l.ProposedDescription,
                ProposedAt = l.ProposedAt?.ToString("O"),
                ProposedByName = proposedByName,
                ProposedByRole = proposedByRole,
                ProposalNote = l.ProposalNote,
                ProposalResponse = l.ProposalResponse,
                ProposalRespondedAt = l.ProposalRespondedAt?.ToString("O"),
                ProposalRespondedByName = responseByName,
                ProposalRespondedByRole = responseByRole,
                ProposalResponseNote = l.ProposalResponseNote,
                BorrowProviderSignature = ToSignature(borrowHandover?.ProviderSignedBy, borrowHandover?.ProviderSignedAt, signerNames),
                BorrowBorrowerSignature = ToSignature(borrowHandover?.BorrowerSignedBy, borrowHandover?.BorrowerSignedAt, signerNames),
                ReturnProviderSignature = ToSignature(returnHandover?.ProviderSignedBy, returnHandover?.ProviderSignedAt, signerNames),
                ReturnBorrowerSignature = ToSignature(returnHandover?.BorrowerSignedBy, returnHandover?.BorrowerSignedAt, signerNames),
                // TRANSPORT: condition_note của dòng BORROW đang giữ checklist JSON (không phải note tự
                // do) — tách riêng qua ChecklistJson, BorrowNote trả null để không hiện nhầm JSON thô.
                BorrowNote = l.ItemType == "TRANSPORT" ? null : borrowHandover?.ConditionNote,
                ReturnNote = returnHandover?.ConditionNote,
                ChecklistJson = l.ItemType == "TRANSPORT" ? borrowHandover?.ConditionNote : null,

                RegistrantFullName = camp.VisitRequest.RegistrantFullName ?? "",
                RegistrantEmail = camp.VisitRequest.RegistrantEmail ?? "",
                RegistrantPhone = camp.VisitRequest.RegistrantPhone ?? "",
                RegistrantOrganization = camp.VisitRequest.RegistrantOrganization ?? "",
                RegistrantJobTitle = camp.VisitRequest.RegistrantJobTitle ?? "",
                Purpose = purpose,
                WorkingContent = workingContent,
                ContactPersonFullName = contactPersonFullName,
                ContactPersonPhone = contactPersonPhone,

                AssignmentHistory = historyDtos,
                LatestAttemptStatus = latestAttemptStatus
            };
        }

        private static string NormalizeStatus(string status, string instanceStatus, string requestStatus, bool borrowSigned, bool returnSigned)
        {
            if (requestStatus == "CANCELLED" || instanceStatus == "CANCELLED" || status == "CANCELLED") return "CANCELLED";
            if (status == "DECLINED") return "REJECTED";
            if (returnSigned || status == "DONE") return "DONE";
            if (borrowSigned || status == "IN_PROGRESS") return "IN_PROGRESS";
            return status;
        }

        private static HandoverSignatureDto? ToSignature(ulong? userId, DateTime? signedAt, Dictionary<ulong, string> signerNames)
        {
            if (userId == null || signedAt == null) return null;
            return new HandoverSignatureDto
            {
                UserId = userId,
                Name = signerNames.TryGetValue(userId.Value, out var name) ? name : $"User #{userId.Value}",
                SignedAt = signedAt.Value.ToString("O")
            };
        }
    }
}
