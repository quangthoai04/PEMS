using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Minutes;

public sealed class SaveMinutesCommandHandler
    : IRequestHandler<SaveMinutesCommand, MinuteDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public SaveMinutesCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<MinuteDto> Handle(SaveMinutesCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new BusinessRuleException("Tiêu đề biên bản không được để trống.");

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

        // The caller must currently hold the lock (same token, not expired, same user).
        bool holdsLock = minute.EditLockedBy == userId
            && minute.EditLockToken == request.EditLockToken
            && minute.EditLockExpiresAt.HasValue && minute.EditLockExpiresAt.Value > now;
        if (!holdsLock)
            throw new ConflictException("Phiên chỉnh sửa biên bản đã hết hạn hoặc đang do người khác giữ. Vui lòng mở lại để chỉnh sửa.");

        // Optimistic concurrency: reject if the record changed since it was opened.
        if (minute.RowVersion != request.RowVersion)
            throw new ConflictException("Biên bản đã được cập nhật bởi người khác. Vui lòng tải lại nội dung mới nhất.");

        minute.Title = request.Title.Trim();
        minute.Content = request.Content;
        minute.Status = MinuteAccess.StatusSaved;
        minute.RowVersion += 1;
        minute.UpdatedAt = now;
        minute.UpdatedBy = userId;
        // Save releases the lock so others can edit next.
        minute.EditLockedBy = null;
        minute.EditLockedAt = null;
        minute.EditLockExpiresAt = null;
        minute.EditLockToken = null;

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
            IsLockedByMe = false,
            IsLockedByOther = false,
            CanView = true,
            CanEdit = canEdit,
            CanCreate = false,
        };
    }
}
