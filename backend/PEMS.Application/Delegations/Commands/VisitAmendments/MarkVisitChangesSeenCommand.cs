using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.VisitAmendments;

/// <summary>
/// "I have looked at this request." Clears the caller's unread change badge for it.
///
/// Called when the DETAIL screen is opened, never when a row merely appears in a list. A badge that
/// clears itself on scroll is worse than no badge: the Staff Leader never learns what changed, and
/// the one signal telling them to look has already been spent.
/// </summary>
public sealed record MarkVisitChangesSeenCommand(ulong VisitRequestId) : IRequest<int>;

public sealed class MarkVisitChangesSeenCommandHandler
    : IRequestHandler<MarkVisitChangesSeenCommand, int>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public MarkVisitChangesSeenCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(MarkVisitChangesSeenCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var userId = _currentUser.UserId.Value;
        var now = _clock.VietnamNow;

        // Scoped by RECIPIENT, so this can only ever clear the caller's own notifications — there is
        // no way to spend somebody else's badge, and no authorization question beyond being signed in.
        var unread = await _db.Notifications
            .Where(n => n.RecipientUserId == userId
                        && n.VisitRequestId == request.VisitRequestId
                        && !n.IsRead)
            .ToListAsync(ct);
        if (unread.Count == 0) return 0;

        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }
        await _db.SaveChangesAsync(ct);
        return unread.Count;
    }
}
