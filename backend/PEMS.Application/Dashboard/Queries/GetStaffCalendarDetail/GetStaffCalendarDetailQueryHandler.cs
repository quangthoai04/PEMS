using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Dashboard.Common;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Dashboard.Queries.GetStaffCalendarDetail;

/// <summary>
/// Chi tiết yêu cầu đến thăm cho modal của bảng lịch dashboard Staff/Staff Leader.
/// Scope (backend là nguồn chuẩn):
///   • Staff Leader — instance thuộc campus mình; đơn liên cơ sở chỉ sau khi HO duyệt.
///   • Staff thường — instance thuộc campus mình (xem lịch văn phòng) hoặc mình là host.
/// Chỉ đọc: visit_requests / visit_request_campuses / visit_guest_members / visit_participants.
/// </summary>
public sealed class GetStaffCalendarDetailQueryHandler
    : IRequestHandler<GetStaffCalendarDetailQuery, StaffCalendarDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public GetStaffCalendarDetailQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<StaffCalendarDetailDto> Handle(
        GetStaffCalendarDetailQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var isStaffLeader = _currentUser.RoleCode == RoleCodes.Staff
            && _currentUser.SubRole == UserSubRoles.Leader;
        var isStaffMember = _currentUser.RoleCode == RoleCodes.Staff
            && _currentUser.SubRole == UserSubRoles.Staff;
        if (!isStaffLeader && !isStaffMember)
            throw new ForbiddenException("Chỉ Staff hoặc Staff Leader mới xem được chi tiết này.");

        var userId = _currentUser.UserId.Value;
        var primaryCampusId = _currentUser.PrimaryCampusId;

        var row = await (
                from c in _db.VisitRequestCampuses
                join vr in _db.VisitRequests on c.VisitRequestId equals vr.VisitRequestId
                join camp in _db.Campuses on c.CampusId equals camp.CampusId
                where c.VisitInstanceId == request.VisitInstanceId
                select new { c, vr, CampusName = camp.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var instance = row.c;
        var visit = row.vr;

        // ── Scope enforcement ──
        var sameCampus = primaryCampusId.HasValue && instance.CampusId == primaryCampusId.Value;
        var isHost = instance.CurrentHostUserId == userId;
        if (isStaffLeader)
        {
            // Đơn liên cơ sở chưa được HO duyệt thì chưa thuộc phạm vi campus.
            var multiVisible = visit.VisitScope != VisitScopes.MultiCampus
                || visit.Status == VisitRequestStatuses.Approved
                || instance.CurrentHostUserId != null;
            if (!sameCampus || !multiVisible)
                throw new ForbiddenException("Yêu cầu này không thuộc phạm vi campus của bạn.");
        }
        else if (!sameCampus && !isHost)
        {
            throw new ForbiddenException("Yêu cầu này không thuộc phạm vi của bạn.");
        }

        var now = _clock.VietnamNow;
        var viewer = new StaffCalendarLogic.ViewerContext(userId, isStaffLeader, primaryCampusId);
        var snapshot = new StaffCalendarLogic.InstanceSnapshot(
            visit.Status, instance.Status, visit.VisitScope, instance.CampusId,
            instance.CurrentHostUserId, instance.PlannedStartAt, instance.PlannedEndAt);
        var actions = StaffCalendarLogic.BuildAllowedActions(snapshot, viewer, now);
        var (displayStatus, colorType, isCancelled, isExpired, isPast) =
            StaffCalendarLogic.ResolveStatus(snapshot, viewer, actions, now);

        // ── Host + người gán host ──
        string? hostName = null, hostEmail = null, hostAssignedByName = null;
        if (instance.CurrentHostUserId.HasValue)
        {
            var host = await _db.Users
                .Where(u => u.UserId == instance.CurrentHostUserId.Value)
                .Select(u => new { u.FullName, u.Email })
                .FirstOrDefaultAsync(cancellationToken);
            hostName = host?.FullName;
            hostEmail = host?.Email;
        }
        if (instance.HostAssignedBy.HasValue)
        {
            hostAssignedByName = await _db.Users
                .Where(u => u.UserId == instance.HostAssignedBy.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Campus-independent approval: the decision lives on THIS campus instance.
        string? decidedByName = null;
        if (instance.DecidedBy.HasValue)
        {
            decidedByName = await _db.Users
                .Where(u => u.UserId == instance.DecidedBy.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var guestCount = await _db.VisitGuestMembers
            .CountAsync(g => g.VisitRequestId == visit.VisitRequestId, cancellationToken);

        // Phản hồi của thành phần hỗ trợ (đã nhận / đã từ chối / đã gán) — hiển thị lịch sử phản hồi.
        var participantResponses = await (
                from p in _db.VisitParticipants
                join u in _db.Users on p.UserId equals u.UserId
                where p.VisitInstanceId == instance.VisitInstanceId
                    && !p.IsHost
                    && (p.Status == ParticipantStatuses.Accepted
                        || p.Status == ParticipantStatuses.Declined
                        || p.Status == ParticipantStatuses.Assigned)
                orderby p.RespondedAt descending
                select new StaffCalendarParticipantResponseDto
                {
                    ParticipantId = p.ParticipantId,
                    UserId = p.UserId,
                    FullName = u.FullName,
                    ParticipantRole = p.ParticipantRole,
                    Status = p.Status,
                    RespondedAt = p.RespondedAt,
                    Note = p.Note,
                })
            .ToListAsync(cancellationToken);

        // Lý do hủy: ưu tiên mức instance, fallback mức request (UC-136).
        var cancellationReason = instance.Status == VisitInstanceStatus.Cancelled
            ? (instance.CancellationReason ?? visit.CancellationReason)
            : visit.CancellationReason;
        var cancelledAt = instance.Status == VisitInstanceStatus.Cancelled
            ? (instance.CancelledAt ?? visit.CancelledAt)
            : visit.CancelledAt;

        return new StaffCalendarDetailDto
        {
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            RequestCode = visit.RequestCode,
            DelegationName = visit.DelegationName,
            VisitScope = visit.VisitScope,
            RequestStatus = visit.Status,
            CampusStatus = instance.Status,
            DisplayStatus = displayStatus,
            ColorType = colorType,
            CampusId = instance.CampusId,
            CampusName = row.CampusName,
            PlannedStartAt = instance.PlannedStartAt,
            PlannedEndAt = instance.PlannedEndAt,
            RegistrantFullName = visit.RegistrantFullName,
            RegistrantOrganization = visit.RegistrantOrganization,
            RegistrantJobTitle = visit.RegistrantJobTitle,
            RegistrantNationality = visit.RegistrantNationality,
            RegistrantPhone = visit.RegistrantPhone,
            RegistrantEmail = visit.RegistrantEmail,
            ContactPersonFullName = visit.ContactPersonFullName,
            ContactPersonPhone = visit.ContactPersonPhone,
            ContactPersonEmail = visit.ContactPersonEmail,
            Purpose = visit.Purpose,
            WorkingContent = visit.WorkingContent,
            VisitType = visit.VisitType,
            VisitTypeOther = visit.VisitTypeOther,
            GuestCount = guestCount,
            WorkingLanguage = visit.WorkingLanguage,
            MediaConsentStatus = visit.MediaConsentStatus,
            MediaConsentNote = visit.MediaConsentNote,
            TransportationNote = visit.TransportationNote,
            NoteToFptu = visit.NoteToFptu,
            CurrentHostUserId = instance.CurrentHostUserId,
            CurrentHostName = hostName,
            CurrentHostEmail = hostEmail,
            HostAssignedAt = instance.HostAssignedAt,
            HostAssignedByName = hostAssignedByName,
            IsCurrentHost = isHost,
            DecisionNote = instance.DecisionNote,
            DecidedByName = decidedByName,
            DecidedAt = instance.DecidedAt,
            IsCancelled = isCancelled,
            CancellationReason = isCancelled ? cancellationReason : null,
            CancelledAt = isCancelled ? cancelledAt : null,
            IsPast = isPast,
            IsExpired = isExpired,
            ParticipantResponses = participantResponses,
            AllowedActions = actions,
        };
    }
}
