using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Dashboard.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Dashboard.Queries.GetStaffCalendar;

/// <summary>
/// Bảng lịch dashboard cho STAFF + LEADER (Staff Leader) và STAFF + STAFF (Staff thường).
///   • office: mọi campus instance thuộc primary campus của user (SINGLE_CAMPUS mọi trạng thái;
///     MULTI_CAMPUS chỉ sau khi HO duyệt — trước đó chưa thuộc phạm vi của campus).
///   • mine:   chỉ instance mà user là host chính thức (current_host_user_id = userId).
/// Backend lọc toàn bộ theo scope; kèm action flags cho từng item (single source of truth).
/// </summary>
public sealed class GetStaffCalendarQueryHandler
    : IRequestHandler<GetStaffCalendarQuery, StaffCalendarResponse>
{
    private const int MaxRangeDays = 93; // tối đa ~3 tháng / lần tải để bảo vệ performance

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public GetStaffCalendarQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<StaffCalendarResponse> Handle(
        GetStaffCalendarQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var isStaffLeader = _currentUser.RoleCode == RoleCodes.Staff
            && _currentUser.SubRole == UserSubRoles.Leader;
        var isStaffMember = _currentUser.RoleCode == RoleCodes.Staff
            && _currentUser.SubRole == UserSubRoles.Staff;
        if (!isStaffLeader && !isStaffMember)
            throw new ForbiddenException("Chỉ Staff hoặc Staff Leader mới xem được bảng lịch này.");

        var userId = _currentUser.UserId.Value;
        var primaryCampusId = _currentUser.PrimaryCampusId
            ?? throw new ForbiddenException("Tài khoản chưa được gán campus chính.");

        // ── Validate khoảng thời gian ──
        var from = request.From.Date;
        var to = request.To.Date.AddDays(1).AddTicks(-1);
        if (to < from)
            throw new ValidationException("Khoảng thời gian không hợp lệ: 'to' phải sau 'from'.");
        if ((to - from).TotalDays > MaxRangeDays)
            throw new ValidationException($"Khoảng thời gian tải lịch tối đa {MaxRangeDays} ngày.");

        var viewMode = string.Equals(request.ViewMode?.Trim(), "mine", StringComparison.OrdinalIgnoreCase)
            ? "mine"
            : "office";

        var q = from c in _db.VisitRequestCampuses
                join vr in _db.VisitRequests on c.VisitRequestId equals vr.VisitRequestId
                join camp in _db.Campuses on c.CampusId equals camp.CampusId
                select new { c, vr, camp };

        if (viewMode == "mine")
        {
            // Lịch của tôi: chỉ item mà tôi là host chính thức.
            q = q.Where(x => x.c.CurrentHostUserId == userId);
        }
        else
        {
            // Lịch văn phòng: mọi yêu cầu đến thăm thuộc campus của tôi. Đơn liên cơ sở chỉ
            // xuất hiện sau khi HO duyệt (chưa duyệt thì chưa thuộc phạm vi của campus).
            q = q.Where(x => x.c.CampusId == primaryCampusId
                && (x.vr.VisitScope == VisitScopes.SingleCampus
                    || x.vr.Status == VisitRequestStatuses.Approved
                    || x.c.CurrentHostUserId != null));
        }

        // Giao với khung thời gian đang hiển thị.
        q = q.Where(x => x.c.PlannedStartAt <= to && x.c.PlannedEndAt >= from);

        var rows = await q
            .OrderBy(x => x.c.PlannedStartAt)
            .Select(x => new
            {
                x.vr.VisitRequestId,
                x.c.VisitInstanceId,
                x.vr.RequestCode,
                x.vr.DelegationName,
                x.vr.RegistrantFullName,
                x.vr.RegistrantOrganization,
                x.c.CampusId,
                CampusName = x.camp.Name,
                x.c.PlannedStartAt,
                x.c.PlannedEndAt,
                RequestStatus = x.vr.Status,
                CampusStatus = x.c.Status,
                x.vr.VisitScope,
                x.c.CurrentHostUserId,
            })
            .ToListAsync(cancellationToken);

        // Tên host: nạp 1 lần cho mọi host xuất hiện trong trang lịch.
        var hostIds = rows.Where(r => r.CurrentHostUserId.HasValue)
            .Select(r => r.CurrentHostUserId!.Value).Distinct().ToList();
        var hostNames = hostIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.Where(u => hostIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        var now = _clock.UtcNow;
        var viewer = new StaffCalendarLogic.ViewerContext(userId, isStaffLeader, primaryCampusId);

        var items = rows.Select(r =>
        {
            var snapshot = new StaffCalendarLogic.InstanceSnapshot(
                r.RequestStatus, r.CampusStatus, r.VisitScope, r.CampusId,
                r.CurrentHostUserId, r.PlannedStartAt, r.PlannedEndAt);

            var actions = StaffCalendarLogic.BuildAllowedActions(snapshot, viewer, now);
            var (displayStatus, colorType, isCancelled, isExpired, isPast) =
                StaffCalendarLogic.ResolveStatus(snapshot, viewer, actions, now);

            return new StaffCalendarItemDto
            {
                VisitRequestId = r.VisitRequestId,
                VisitInstanceId = r.VisitInstanceId,
                RequestCode = r.RequestCode,
                Title = r.DelegationName ?? r.RequestCode ?? "Yêu cầu đến thăm",
                DelegationName = r.DelegationName,
                RegistrantFullName = r.RegistrantFullName,
                RegistrantOrganization = r.RegistrantOrganization,
                CampusId = r.CampusId,
                CampusName = r.CampusName,
                PlannedStartAt = r.PlannedStartAt,
                PlannedEndAt = r.PlannedEndAt,
                RequestStatus = r.RequestStatus,
                CampusStatus = r.CampusStatus,
                VisitScope = r.VisitScope,
                CurrentHostUserId = r.CurrentHostUserId,
                CurrentHostName = r.CurrentHostUserId.HasValue
                    && hostNames.TryGetValue(r.CurrentHostUserId.Value, out var hn) ? hn : null,
                IsCurrentHost = r.CurrentHostUserId == userId,
                IsPast = isPast,
                IsCancelled = isCancelled,
                IsExpired = isExpired,
                DisplayStatus = displayStatus,
                ColorType = colorType,
                AllowedActions = actions,
            };
        }).ToList();

        return new StaffCalendarResponse
        {
            ViewMode = viewMode,
            From = from,
            To = to,
            Items = items,
        };
    }
}
