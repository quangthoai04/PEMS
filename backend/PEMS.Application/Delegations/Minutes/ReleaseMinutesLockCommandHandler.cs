using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Delegations.Minutes;

public sealed class ReleaseMinutesLockCommandHandler
    : IRequestHandler<ReleaseMinutesLockCommand, MinuteDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public ReleaseMinutesLockCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<MinuteDto> Handle(ReleaseMinutesLockCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var minute = await _db.Minutes.FirstOrDefaultAsync(m => m.MinutesId == request.MinutesId, cancellationToken)
            ?? throw new NotFoundException("Minute", request.MinutesId);

        // Only the holder (matching token) may release; otherwise leave the lock untouched.
        if (minute.EditLockedBy == userId && minute.EditLockToken == request.EditLockToken)
        {
            minute.EditLockedBy = null;
            minute.EditLockedAt = null;
            minute.EditLockExpiresAt = null;
            minute.EditLockToken = null;
            minute.UpdatedAt = _clock.VietnamNow;
            minute.UpdatedBy = userId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var dto = new MinuteDto
        {
            Exists = true,
            MinutesId = minute.MinutesId,
            VisitInstanceId = minute.VisitInstanceId,
            Title = minute.Title,
            Content = minute.Content,
            Status = minute.Status,
            RowVersion = minute.RowVersion,
            IsLockedByMe = false,
            IsLockedByOther = false,
            CanView = true,
            CanCreate = false,
        };
        await MinuteChildren.LoadInto(_db, dto, minute.MinutesId, cancellationToken);
        return dto;
    }
}
