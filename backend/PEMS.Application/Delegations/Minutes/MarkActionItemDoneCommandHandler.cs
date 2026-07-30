using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Delegations.Minutes;

public sealed class MarkActionItemDoneCommandHandler : IRequestHandler<MarkActionItemDoneCommand, bool>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public MarkActionItemDoneCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(MarkActionItemDoneCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var userId = _currentUser.UserId.Value;

        var item = await _db.MinuteActionItems
            .FirstOrDefaultAsync(a => a.ActionItemId == request.ActionItemId, cancellationToken)
            ?? throw new NotFoundException("MinuteActionItem", request.ActionItemId);

        if (item.AssignedToUserId != userId)
            throw new ForbiddenException("Bạn không phải người phụ trách đầu việc này.");
        if (item.Status == "CANCELLED")
            throw new BusinessRuleException("Đầu việc này đã bị hủy, không thể đánh dấu hoàn thành.");
        if (item.Status == "DONE")
            return true; // already done — idempotent no-op

        var now = _clock.VietnamNow;
        item.Status = "DONE";
        item.CompletedAt = now;
        item.UpdatedAt = now;
        item.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
