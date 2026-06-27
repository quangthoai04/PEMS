using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Delegations.Commands.ConfirmTheChangeProposal;

public sealed class ConfirmTheChangeProposalCommandHandler : IRequestHandler<ConfirmTheChangeProposalCommand, ConfirmTheChangeProposalResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ConfirmTheChangeProposalCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ConfirmTheChangeProposalResponse> Handle(ConfirmTheChangeProposalCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is null)
            throw new UnauthorizedAccessException("Bạn cần đăng nhập để phản hồi đề xuất.");

        var item = await _db.VisitLogisticsItems
            .Include(x => x.VisitInstance)
                .ThenInclude(x => x.VisitRequest)
            .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

        if (item == null)
            throw new InvalidOperationException("Không tìm thấy đơn yêu cầu.");

        if (item.Status != "CHANGE_PROPOSED")
            throw new InvalidOperationException("Đơn yêu cầu không ở trạng thái đang đề xuất.");

        var now = DateTime.UtcNow;
        item.ProposalResponse = request.Accepted ? "ACCEPTED" : "REJECTED";
        item.ProposalRespondedBy = _currentUser.UserId.Value;
        item.ProposalRespondedAt = now;
        item.ProposalResponseNote = request.Note?.Trim();
        item.UpdatedBy = _currentUser.UserId.Value;
        item.UpdatedAt = now;

        if (request.Accepted)
        {
            if (item.ProposedUsageStartAt.HasValue) item.UsageStartAt = item.ProposedUsageStartAt.Value;
            if (item.ProposedUsageEndAt.HasValue) item.UsageEndAt = item.ProposedUsageEndAt.Value;
            if (!string.IsNullOrWhiteSpace(item.ProposedDescription)) item.Description = item.ProposedDescription;
            item.Status = "ACCEPTED";
        }
        else
        {
            item.Status = "REJECTED";
            item.DecisionNote = request.Note?.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new ConfirmTheChangeProposalResponse
        {
            LogisticsItemId = item.LogisticsItemId,
            Status = item.Status,
            Message = request.Accepted ? "Đã chấp nhận đề xuất thay đổi." : "Đã từ chối đề xuất thay đổi."
        };
    }
}
