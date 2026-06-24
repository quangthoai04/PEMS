using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Minutes;

public sealed class AcquireMinutesLockCommandHandler
    : IRequestHandler<AcquireMinutesLockCommand, MinuteDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public AcquireMinutesLockCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<MinuteDto> Handle(AcquireMinutesLockCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var minute = await _db.Minutes.FirstOrDefaultAsync(m => m.MinutesId == request.MinutesId, cancellationToken)
            ?? throw new NotFoundException("Minute", request.MinutesId);

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == minute.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", minute.VisitInstanceId);

        var acceptedRole = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.UserId == userId
                && p.Status == ParticipantStatuses.Accepted && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        var (inScope, canEdit) = MinuteAccess.Evaluate(instance, instance.VisitRequest, _currentUser, acceptedRole);
        if (!inScope)
            throw new ForbiddenException("Bạn không có quyền xem biên bản của chuyến thăm này.");
        if (!canEdit)
            throw new ForbiddenException("Bạn không có quyền chỉnh sửa biên bản chuyến thăm này.");

        var now = _clock.UtcNow;
        if (MinuteAccess.IsLockActive(minute, now) && minute.EditLockedBy != userId)
            throw new ConflictException("Biên bản đang được chỉnh sửa bởi người khác. Vui lòng thử lại sau khi họ lưu hoặc phiên sửa hết hạn.");

        var token = Guid.NewGuid().ToString();
        minute.EditLockedBy = userId;
        minute.EditLockedAt = now;
        minute.EditLockExpiresAt = now.AddMinutes(MinuteAccess.LockMinutes);
        minute.EditLockToken = token;
        minute.UpdatedAt = now;
        minute.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);

        return new MinuteDto
        {
            Exists = true,
            MinutesId = minute.MinutesId,
            VisitInstanceId = minute.VisitInstanceId,
            Title = minute.Title,
            Content = minute.Content,
            Status = minute.Status,
            RowVersion = minute.RowVersion,
            EditLockedBy = minute.EditLockedBy,
            EditLockedAt = minute.EditLockedAt,
            EditLockExpiresAt = minute.EditLockExpiresAt,
            EditLockToken = token,
            IsLockedByMe = true,
            IsLockedByOther = false,
            CanView = true,
            CanEdit = true,
            CanCreate = false,
        };
    }
}
