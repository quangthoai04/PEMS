using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Minutes;

public sealed class GetNewMinuteParticipantsQueryHandler
    : IRequestHandler<GetNewMinuteParticipantsQuery, List<MinuteParticipantDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public GetNewMinuteParticipantsQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<List<MinuteParticipantDto>> Handle(GetNewMinuteParticipantsQuery request, CancellationToken cancellationToken)
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

        // "Đồng bộ người mới" là thao tác trong phiên chỉnh sửa: chỉ người ĐANG GIỮ lock mới được gọi
        // (không chỉ canEdit) — nhất quán với SaveMinutes (lớp kiểm tra cuối, không phụ thuộc frontend).
        if (!MinuteAccess.IsLockHeldBy(minute, userId, _clock.VietnamNow))
            throw new ConflictException("Phiên chỉnh sửa biên bản đã hết hạn hoặc đang do người khác giữ. Vui lòng mở lại để chỉnh sửa.");

        var existing = await _db.MinuteParticipants
            .Where(p => p.MinutesId == minute.MinutesId)
            .ToListAsync(cancellationToken);

        var candidates = await MinuteAutoFill.ComputeNewRowsAsync(
            _db, instance, existing, minute.MinutesId, _clock.VietnamNow, cancellationToken);

        return candidates.Select(p => new MinuteParticipantDto
        {
            MinuteParticipantId = 0,
            MinutesId = p.MinutesId,
            UserId = p.UserId,
            GuestMemberId = p.GuestMemberId,
            FullNameSnapshot = p.FullNameSnapshot,
            RoleSnapshot = p.RoleSnapshot,
            OrganizationSnapshot = p.OrganizationSnapshot,
            EmailSnapshot = p.EmailSnapshot,
            AttendanceStatus = p.AttendanceStatus,
            AttendanceNote = p.AttendanceNote,
            DisplayOrder = p.DisplayOrder,
            ParticipantKind = MinuteParticipantDto.KindOf(p.UserId, p.GuestMemberId),
        }).ToList();
    }
}
