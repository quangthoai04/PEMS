using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Minutes;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Minutes;

public sealed class CreateOrLockMinutesCommandHandler
    : IRequestHandler<CreateOrLockMinutesCommand, MinuteDto>
{
    /// <summary>Refusal code for opening a FIRST biên bản on a campus that has not started yet.</summary>
    public const string MinutesNotStartedYet = "MINUTES_VISIT_NOT_STARTED";


    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public CreateOrLockMinutesCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<MinuteDto> Handle(CreateOrLockMinutesCommand request, CancellationToken cancellationToken)
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
        if (!canEdit)
            throw new ForbiddenException("Bạn không có quyền tạo hoặc chỉnh sửa biên bản chuyến thăm này.");

        var now = _clock.VietnamNow;
        var token = Guid.NewGuid().ToString();
        var expiresAt = now.AddMinutes(MinuteAccess.LockMinutes);

        // Serialize the create/lock so two concurrent openers cannot both create or both lock.
        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        var minute = await _db.Minutes
            .FirstOrDefaultAsync(m => m.VisitInstanceId == instance.VisitInstanceId, cancellationToken);

        if (minute == null)
        {
            // A biên bản is the record of a meeting that is HAPPENING or has happened, and until now
            // nothing in the backend said so — the only thing standing between an approved campus and
            // a biên bản opened three weeks early was the frontend, which does not render the tab
            // before DURING_VISIT. That left the rule unstated where it counts: a direct API call
            // could open one at ASSIGNED, and the auto-fill would freeze a delegation list, a host and
            // an đầu mối that were all still free to change afterwards.
            //
            // Only CREATION is gated. Re-opening an existing record is deliberately not: a biên bản
            // written during the visit must stay editable while the campus finishes, and a rule that
            // locked it out retroactively would be worse than the gap it closes. CLOSED and CANCELLED
            // are already refused above, by canEdit.
            if (instance.Status != VisitInstanceStatus.DuringVisit
                && instance.Status != VisitInstanceStatus.AfterVisit)
                throw new ConflictException(
                    "Chưa thể tạo biên bản cho chuyến thăm này: biên bản chỉ mở khi đã bắt đầu tiếp khách.",
                    MinutesNotStartedYet);

            // First open → create the single minutes record, already locked by the caller.
            minute = new Minute
            {
                VisitInstanceId = instance.VisitInstanceId,
                Title = string.IsNullOrWhiteSpace(request.Title) ? "Biên bản cuộc họp" : request.Title!.Trim(),
                Content = null,
                Status = MinuteAccess.StatusDraft,
                EditLockedBy = userId,
                EditLockedAt = now,
                EditLockExpiresAt = expiresAt,
                EditLockToken = token,
                RowVersion = 0,
                CreatedAt = now,
                CreatedBy = userId,
            };
            _db.Minutes.Add(minute);
            // Persist first so the record gets its identity, then auto-fill the attendance snapshot
            // (Host + accepted internal participants + guests) inside the same transaction.
            await _db.SaveChangesAsync(cancellationToken);

            var autoFilled = await MinuteAutoFill.ComputeNewRowsAsync(
                _db, instance, Array.Empty<MinuteParticipant>(), minute.MinutesId, now, cancellationToken);
            if (autoFilled.Count > 0)
            {
                foreach (var row in autoFilled) _db.MinuteParticipants.Add(row);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            // Existing record: only acquire if free / expired / already mine.
            if (MinuteAccess.IsLockActive(minute, now) && minute.EditLockedBy != userId)
                throw new ConflictException("Biên bản đang được chỉnh sửa bởi người khác. Vui lòng thử lại sau khi họ lưu hoặc phiên sửa hết hạn.");

            minute.EditLockedBy = userId;
            minute.EditLockedAt = now;
            minute.EditLockExpiresAt = expiresAt;
            minute.EditLockToken = token;
            minute.UpdatedAt = now;
            minute.UpdatedBy = userId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        var dto = new MinuteDto
        {
            Exists = true,
            MinutesId = minute.MinutesId,
            VisitInstanceId = minute.VisitInstanceId,
            Title = minute.Title,
            Content = minute.Content,
            Status = minute.Status,
            RowVersion = minute.RowVersion,
            UpdatedAt = minute.UpdatedAt,
            EditLockedBy = minute.EditLockedBy,
            EditLockedAt = minute.EditLockedAt,
            EditLockExpiresAt = minute.EditLockExpiresAt,
            EditLockToken = token,   // returned ONLY to the lock holder
            IsLockedByMe = true,
            IsLockedByOther = false,
            CanView = true,
            CanEdit = true,
            CanCreate = false,
        };
        await MinuteChildren.LoadInto(_db, dto, minute.MinutesId, cancellationToken);
        return dto;
    }
}
