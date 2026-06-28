using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.ProposeRequestChange
{
    public class ProposeRequestChangeCommand : IRequest<bool>
    {
        public ulong LogisticsItemId { get; set; }
        /// <summary>Proposed quantity (optional). When set, must be >= 1. Stored on proposed_quantity —
        /// the original quantity (PLANNED figure) is never overwritten.</summary>
        public int? ProposedQuantity { get; set; }
        public string? ProposedUsageStartAt { get; set; } // YYYY-MM-DDTHH:mm:ss
        public string? ProposedUsageEndAt { get; set; } // YYYY-MM-DDTHH:mm:ss
        public string? ProposedDescription { get; set; }
        /// <summary>Reason/note for the proposal — REQUIRED.</summary>
        public string? ProposalNote { get; set; }
    }

    public class ProposeRequestChangeCommandHandler : IRequestHandler<ProposeRequestChangeCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public ProposeRequestChangeCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(ProposeRequestChangeCommand request, CancellationToken cancellationToken)
        {
            // proposal_note is the mandatory rationale; proposed quantity/time/description are optional.
            var note = (request.ProposalNote ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(note)) note = (request.ProposedDescription ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(note)) throw new Exception("Vui lòng nhập lý do/ghi chú đề xuất.");
            if (request.ProposedQuantity is { } pq && pq < 1) throw new Exception("Số lượng đề xuất phải là số nguyên ≥ 1.");

            var l = await _context.VisitLogisticsItems
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

            if (l == null) throw new Exception("Không tìm thấy đơn yêu cầu");

            ulong userId = _currentUserService.UserId.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null || l.RequestedToDepartmentId != user.DepartmentId)
                throw new Exception("Không có quyền đề xuất thay đổi đơn yêu cầu của phòng ban khác");

            var isDepartmentStaff = string.Equals(_currentUserService.RoleCode, RoleCodes.Department, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_currentUserService.SubRole, UserSubRoles.Staff, StringComparison.OrdinalIgnoreCase);
            if (isDepartmentStaff && l.AssignedToUserId != userId)
                throw new Exception("Ban chi co the de xuat thay doi don yeu cau duoc giao cho minh.");

            DateTime? ps = null, pe = null;
            if (!string.IsNullOrEmpty(request.ProposedUsageStartAt) && DateTime.TryParse(request.ProposedUsageStartAt, out var s))
                ps = DateTime.SpecifyKind(s, DateTimeKind.Unspecified);
            if (!string.IsNullOrEmpty(request.ProposedUsageEndAt) && DateTime.TryParse(request.ProposedUsageEndAt, out var e))
                pe = DateTime.SpecifyKind(e, DateTimeKind.Unspecified);
            if (ps.HasValue && pe.HasValue && pe.Value <= ps.Value)
                throw new Exception("Thời gian kết thúc đề xuất phải sau thời gian bắt đầu.");

            // Never overwrite the original quantity (the PLANNED figure) — only the proposed_* columns.
            l.ProposedQuantity = request.ProposedQuantity;
            l.ProposedDescription = string.IsNullOrWhiteSpace(request.ProposedDescription) ? null : request.ProposedDescription.Trim();
            l.ProposalNote = note;
            l.ProposedUsageStartAt = ps;
            l.ProposedUsageEndAt = pe;

            l.Status = "CHANGE_PROPOSED";
            l.ProposedBy = userId;
            l.ProposedAt = DateTime.UtcNow;
            l.ProposalResponse = null;
            l.ProposalRespondedBy = null;
            l.ProposalRespondedAt = null;
            l.ProposalResponseNote = null;
            l.UpdatedBy = userId;
            l.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
