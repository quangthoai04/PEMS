using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Minutes;

public sealed class GetVisitInstanceMinutesQueryHandler
    : IRequestHandler<GetVisitInstanceMinutesQuery, MinuteDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public GetVisitInstanceMinutesQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<MinuteDto> Handle(GetVisitInstanceMinutesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var acceptedRole = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.UserId == userId
                && p.Status == ParticipantStatuses.Accepted && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        var (inScope, canEdit) = MinuteAccess.Evaluate(instance, instance.VisitRequest, _currentUser, acceptedRole);
        if (!inScope)
            throw new ForbiddenException("Bạn không có quyền xem biên bản của chuyến thăm này.");

        var minute = await _db.Minutes
            .FirstOrDefaultAsync(m => m.VisitInstanceId == instance.VisitInstanceId, cancellationToken);

        var now = _clock.UtcNow;
        var dto = new MinuteDto
        {
            VisitInstanceId = instance.VisitInstanceId,
            CanView = true,
            CanEdit = canEdit,
            CanCreate = canEdit && minute == null,
        };

        if (minute == null)
            return dto;

        bool lockActive = MinuteAccess.IsLockActive(minute, now);
        string? lockedByName = null;
        if (minute.EditLockedBy is { } lockerId)
            lockedByName = await _db.Users.Where(u => u.UserId == lockerId)
                .Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);

        dto.Exists = true;
        dto.MinutesId = minute.MinutesId;
        dto.Title = minute.Title;
        dto.Content = minute.Content;
        dto.Status = minute.Status;
        dto.RowVersion = minute.RowVersion;
        dto.EditLockedBy = minute.EditLockedBy;
        dto.EditLockedByName = lockedByName;
        dto.EditLockedAt = minute.EditLockedAt;
        dto.EditLockExpiresAt = minute.EditLockExpiresAt;
        dto.IsLockedByOther = lockActive && minute.EditLockedBy != userId;
        dto.IsLockedByMe = lockActive && minute.EditLockedBy == userId;
        // Editing is offered only when the user may edit AND no one else holds the lock.
        dto.CanEdit = canEdit && !dto.IsLockedByOther;
        return dto;
    }
}
