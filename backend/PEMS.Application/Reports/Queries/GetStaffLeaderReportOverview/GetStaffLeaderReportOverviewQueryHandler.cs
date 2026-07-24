using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Shared;

using PEMS.Application.Common;
namespace PEMS.Application.Reports.Queries.GetStaffLeaderReportOverview;

/// <summary>
/// Aggregates the Staff Leader campus operation report from live data (no mock).
/// Scope is always the leader's primary campus; every query is AsNoTracking and
/// aggregation happens in the database wherever EF can translate it.
/// The controller enforces the role, this handler re-checks as defense in depth.
/// </summary>
public sealed class GetStaffLeaderReportOverviewQueryHandler
    : IRequestHandler<GetStaffLeaderReportOverviewQuery, StaffLeaderReportOverviewDto>
{
    private const int VnUtcOffsetHours = 7;
    private const int PreviewLimit = 10;
    private const int FeedbackEntryLimit = 10;

    private static readonly string[] OverdueCloseStatuses =
    {
        VisitInstanceStatus.Assigned,
        VisitInstanceStatus.BeforeVisit,
        VisitInstanceStatus.DuringVisit,
        VisitInstanceStatus.AfterVisit,
    };

    private static readonly string[] ActiveHostStatuses = OverdueCloseStatuses;

    private static readonly string[] OpenLogisticsStatuses =
    {
        LogisticsItemStatus.Requested,
        LogisticsItemStatus.ChangeProposed,
        LogisticsItemStatus.Assigned,
        LogisticsItemStatus.Accepted,
        LogisticsItemStatus.InProgress,
    };

    private static readonly (string Status, string LabelVi)[] PipelineStatuses =
    {
        (VisitInstanceStatus.WaitingRequestApproval, "Chờ xử lý tại campus"),
        (VisitInstanceStatus.Assigned, "Đã duyệt & gán host"),
        (VisitInstanceStatus.BeforeVisit, "Trước chuyến"),
        (VisitInstanceStatus.DuringVisit, "Đang diễn ra"),
        (VisitInstanceStatus.AfterVisit, "Sau chuyến"),
        (VisitInstanceStatus.Closed, "Đã đóng"),
        (VisitInstanceStatus.Cancelled, "Đã hủy"),
        (VisitInstanceStatus.Rejected, "Đã từ chối"),
    };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStaffLeaderReportOverviewQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StaffLeaderReportOverviewDto> Handle(GetStaffLeaderReportOverviewQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            throw new ForbiddenException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
        if (!string.Equals(_currentUser.RoleCode, "STAFF", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(_currentUser.SubRole, "LEADER", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Bạn không có quyền xem báo cáo vận hành campus.");

        var campusId = _currentUser.PrimaryCampusId
            ?? throw new ForbiddenException("Tài khoản chưa được gán campus chính.");

        var nowVn = VietnamTime.Now();

        var upcomingLimitUtc = nowVn.AddDays(7);

        var preset = NormalizePreset(request.Preset);
        var (fromVn, toVnExclusive) = ResolvePeriodVn(preset, request.FromDate, request.ToDate, nowVn);


        var visitStatus = NormalizeFilter(request.VisitStatus);
        var requestStatus = NormalizeFilter(request.RequestStatus);
        var logisticsStatus = NormalizeFilter(request.LogisticsStatus);
        var feedbackRating = NormalizeFilter(request.FeedbackRating);
        ulong? hostUserId = ulong.TryParse(request.HostUserId, out var parsedHost) && parsedHost > 0 ? parsedHost : null;
        ulong? departmentId = ulong.TryParse(request.DepartmentId, out var parsedDept) && parsedDept > 0 ? parsedDept : null;

        // ---- Base query 1: campus instances planned in the period (trend, pipeline, guests, logistics). ----
        var instances = _db.VisitRequestCampuses.AsNoTracking()
            .Where(ci => ci.CampusId == campusId && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive);
        if (visitStatus != null) instances = instances.Where(ci => ci.Status == visitStatus);
        if (requestStatus != null) instances = instances.Where(ci => ci.VisitRequest.Status == requestStatus);
        if (hostUserId != null) instances = instances.Where(ci => ci.CurrentHostUserId == hostUserId);

        // ---- Base query 2: current operational state (ignores period on purpose — action queues). ----
        var opInstances = _db.VisitRequestCampuses.AsNoTracking()
            .Where(ci => ci.CampusId == campusId);
        if (hostUserId != null) opInstances = opInstances.Where(ci => ci.CurrentHostUserId == hostUserId);

        // ---- Base query 3: campus instances waiting for THIS Staff Leader's decision (current
        // state). Campus-independent approval: single AND multi-campus instances route straight
        // to the campus — pending = instance WAITING_REQUEST_APPROVAL of my campus. ----
        var pendingApproval = _db.VisitRequests.AsNoTracking()
            .Where(r => r.Status != VisitRequestStatus.Cancelled
                        && r.CampusInstances.Any(ci => ci.CampusId == campusId
                            && ci.Status == VisitInstanceStatus.WaitingRequestApproval));

        // ---- Current-state workflow counts (KPI strip + attention). ----
        var opStatusCounts = await opInstances
            .GroupBy(ci => ci.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        int OpCount(string status) => opStatusCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var pendingApprovalCount = await pendingApproval.CountAsync(cancellationToken);
        var overdueOrNotClosed = await opInstances.CountAsync(
            ci => ci.PlannedEndAt < nowVn && OverdueCloseStatuses.Contains(ci.Status), cancellationToken);

        // ---- Period aggregates: lifecycle pipeline + closed count + guests. ----
        var instanceStatusCounts = await instances
            .GroupBy(ci => ci.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var totalInstances = instanceStatusCounts.Sum(x => x.Count);
        int PeriodCount(string status) => instanceStatusCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var lifecyclePipeline = PipelineStatuses
            .Select(p => new StaffLeaderLifecyclePipelineItem
            {
                Status = p.Status,
                LabelVi = p.LabelVi,
                Count = PeriodCount(p.Status),
                Percentage = totalInstances > 0
                    ? Math.Round(PeriodCount(p.Status) * 100.0 / totalInstances, 1)
                    : 0,
            })
            .ToList();

        var totalGuests = await instances
            .SelectMany(ci => ci.VisitRequest.GuestMembers)
            .CountAsync(cancellationToken);

        // ---- Monthly trend (grouped by Vietnam-local month of planned_start_at). ----
        var trendRaw = await instances
            .GroupBy(ci => new { ci.PlannedStartAt.Year, ci.PlannedStartAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Count(),
                Closed = g.Count(ci => ci.Status == VisitInstanceStatus.Closed),
                Cancelled = g.Count(ci => ci.Status == VisitInstanceStatus.Cancelled),
            })
            .ToListAsync(cancellationToken);

        var monthlyTrend = new List<StaffLeaderMonthlyTrend>();
        var lastMonthVn = toVnExclusive.AddDays(-1);
        for (var cursor = new DateTime(fromVn.Year, fromVn.Month, 1);
             cursor <= new DateTime(lastMonthVn.Year, lastMonthVn.Month, 1);
             cursor = cursor.AddMonths(1))
        {
            var row = trendRaw.FirstOrDefault(t => t.Year == cursor.Year && t.Month == cursor.Month);
            var total = row?.Total ?? 0;
            var closed = row?.Closed ?? 0;
            var cancelled = row?.Cancelled ?? 0;
            monthlyTrend.Add(new StaffLeaderMonthlyTrend
            {
                Month = cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                MonthLabel = $"T{cursor.Month}/{cursor.Year}",
                TotalInstances = total,
                ClosedInstances = closed,
                CancelledInstances = cancelled,
                ActiveInstances = total - closed - cancelled,
            });
        }

        // ---- Host workload (current active assignments, campus scope). ----
        var hostAgg = await opInstances
            .Where(ci => ci.CurrentHostUserId != null && ActiveHostStatuses.Contains(ci.Status))
            .GroupBy(ci => ci.CurrentHostUserId!.Value)
            .Select(g => new
            {
                HostUserId = g.Key,
                Assigned = g.Count(),
                Upcoming7 = g.Count(ci => ci.PlannedStartAt >= nowVn && ci.PlannedStartAt < upcomingLimitUtc),
                Before = g.Count(ci => ci.Status == VisitInstanceStatus.Assigned || ci.Status == VisitInstanceStatus.BeforeVisit),
                During = g.Count(ci => ci.Status == VisitInstanceStatus.DuringVisit),
                After = g.Count(ci => ci.Status == VisitInstanceStatus.AfterVisit),
            })
            .OrderByDescending(x => x.Assigned)
            .ToListAsync(cancellationToken);

        // ---- Feedback base: feedbacks submitted in the period for instances of this campus. ----
        var feedbackBase =
            from f in _db.Feedbacks.AsNoTracking()
            where f.SubmittedAt >= fromVn && f.SubmittedAt < toVnExclusive && f.VisitInstanceId != null
            join ci in _db.VisitRequestCampuses.AsNoTracking() on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
            where ci.CampusId == campusId
            select new { f, ci };
        if (hostUserId != null) feedbackBase = feedbackBase.Where(x => x.ci.CurrentHostUserId == hostUserId);
        if (feedbackRating == "LOW") feedbackBase = feedbackBase.Where(x => x.f.Rating <= 2);
        else if (feedbackRating == "HIGH") feedbackBase = feedbackBase.Where(x => x.f.Rating >= 4);
        else if (byte.TryParse(feedbackRating, out var exactRating) && exactRating is >= 1 and <= 5)
            feedbackBase = feedbackBase.Where(x => x.f.Rating == exactRating);

        var feedbackStats = await feedbackBase
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Avg = g.Average(x => (double)x.f.Rating),
                Low = g.Count(x => x.f.Rating <= 2),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var lowFeedbackRows = await feedbackBase
            .Where(x => x.f.Rating <= 2)
            .OrderBy(x => x.f.Rating).ThenByDescending(x => x.f.SubmittedAt)
            .Take(FeedbackEntryLimit)
            .Select(x => new
            {
                x.f.FeedbackId,
                VisitInstanceId = x.ci.VisitInstanceId,
                // Instance row: mixed v2 shows THIS instance's detail name.
                DelegationName = x.ci.FormDetail != null ? x.ci.FormDetail.DelegationName : null,
                x.ci.CurrentHostUserId,
                x.f.Rating,
                x.f.Comment,
                x.f.SubmittedAt,
                x.ci.PlannedStartAt,
            })
            .ToListAsync(cancellationToken);

        var goodFeedbackRows = await feedbackBase
            .Where(x => x.f.Rating >= 4)
            .OrderByDescending(x => x.f.SubmittedAt)
            .Take(FeedbackEntryLimit)
            .Select(x => new
            {
                x.f.FeedbackId,
                VisitInstanceId = x.ci.VisitInstanceId,
                // Instance row: mixed v2 shows THIS instance's detail name.
                DelegationName = x.ci.FormDetail != null ? x.ci.FormDetail.DelegationName : null,
                x.ci.CurrentHostUserId,
                x.f.Rating,
                x.f.Comment,
                x.f.SubmittedAt,
                x.ci.PlannedStartAt,
            })
            .ToListAsync(cancellationToken);

        var ratingByHostRaw = await feedbackBase
            .Where(x => x.ci.CurrentHostUserId != null)
            .GroupBy(x => x.ci.CurrentHostUserId!.Value)
            .Select(g => new { HostUserId = g.Key, Avg = g.Average(x => (double)x.f.Rating), Count = g.Count() })
            .ToListAsync(cancellationToken);

        // ---- Logistics by department (items of instances planned in the period). ----
        var logisticsBase =
            from li in _db.VisitLogisticsItems.AsNoTracking()
            join ci in _db.VisitRequestCampuses.AsNoTracking() on li.VisitInstanceId equals ci.VisitInstanceId
            where ci.CampusId == campusId && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive
            select new { li, ci };
        if (hostUserId != null) logisticsBase = logisticsBase.Where(x => x.ci.CurrentHostUserId == hostUserId);
        if (departmentId != null) logisticsBase = logisticsBase.Where(x => x.li.RequestedToDepartmentId == departmentId);
        if (logisticsStatus != null) logisticsBase = logisticsBase.Where(x => x.li.Status == logisticsStatus);

        var logisticsAgg = await logisticsBase
            .GroupBy(x => x.li.RequestedToDepartmentId)
            .Select(g => new
            {
                DepartmentId = g.Key,
                Total = g.Count(),
                Requested = g.Count(x => x.li.Status == LogisticsItemStatus.Requested
                                         || x.li.Status == LogisticsItemStatus.ChangeProposed
                                         || x.li.Status == LogisticsItemStatus.Assigned),
                Accepted = g.Count(x => x.li.Status == LogisticsItemStatus.Accepted),
                InProgress = g.Count(x => x.li.Status == LogisticsItemStatus.InProgress),
                Done = g.Count(x => x.li.Status == LogisticsItemStatus.Done),
                Rejected = g.Count(x => x.li.Status == LogisticsItemStatus.Rejected
                                        || x.li.Status == LogisticsItemStatus.Declined),
                Overdue = g.Count(x => OpenLogisticsStatuses.Contains(x.li.Status) && x.ci.PlannedEndAt < nowVn),
            })
            .ToListAsync(cancellationToken);

        var deptIds = logisticsAgg.Where(x => x.DepartmentId != null).Select(x => x.DepartmentId!.Value).Distinct().ToList();
        var deptNames = await _db.Departments.AsNoTracking()
            .Where(d => deptIds.Contains(d.DepartmentId))
            .Select(d => new { d.DepartmentId, d.Name })
            .ToListAsync(cancellationToken);

        var logisticsByDepartment = logisticsAgg
            .Select(x => new StaffLeaderLogisticsByDepartment
            {
                DepartmentId = x.DepartmentId ?? 0,
                DepartmentName = x.DepartmentId != null
                    ? deptNames.FirstOrDefault(d => d.DepartmentId == x.DepartmentId)?.Name ?? $"Phòng ban #{x.DepartmentId}"
                    : "Chưa gán phòng ban",
                TotalItems = x.Total,
                Requested = x.Requested,
                Accepted = x.Accepted,
                InProgress = x.InProgress,
                Done = x.Done,
                Rejected = x.Rejected,
                OverdueCount = x.Overdue,
            })
            .OrderByDescending(x => x.OverdueCount).ThenByDescending(x => x.TotalItems)
            .ToList();
        var overdueLogisticsTotal = logisticsByDepartment.Sum(x => x.OverdueCount);

        // ---- Pending actions: approvals + host assignments (current state, oldest first). ----
        var approvalRows = await pendingApproval
            .OrderBy(r => r.SubmittedAt)
            .Take(PreviewLimit)
            .Select(r => new
            {
                r.VisitRequestId,
                r.RequestCode,
                // The Staff Leader's view of a mixed v2 request = THEIR OWN campus's detail (never the
                // projection, never a sibling campus's content).
                DelegationName = r.CampusInstances
                        .Where(ci => ci.CampusId == campusId && ci.FormDetail != null)
                        .Select(ci => ci.FormDetail!.DelegationName)
                        .FirstOrDefault(),
                r.RegistrantOrganization,
                VisitType = r.CampusInstances
                        .Where(ci => ci.CampusId == campusId && ci.FormDetail != null)
                        .Select(ci => ci.FormDetail!.VisitType)
                        .FirstOrDefault(),
                r.Status,
                r.SubmittedAt,
                VisitInstanceId = r.CampusInstances
                    .Where(ci => ci.CampusId == campusId)
                    .Select(ci => (ulong?)ci.VisitInstanceId)
                    .FirstOrDefault(),
                PlannedStartAt = r.CampusInstances
                    .Where(ci => ci.CampusId == campusId)
                    .Min(ci => (DateTime?)ci.PlannedStartAt),
                PlannedEndAt = r.CampusInstances
                    .Where(ci => ci.CampusId == campusId)
                    .Max(ci => (DateTime?)ci.PlannedEndAt),
                GuestCount = r.GuestMembers.Count,
            })
            .ToListAsync(cancellationToken);

        // Campus-independent approval: there is no separate "assign host" queue anymore —
        // approving ALWAYS assigns the host in the same action.
        var pendingActionRequests = approvalRows
            .Select(r => new StaffLeaderPendingActionRequest
            {
                Type = "APPROVAL",
                RequestId = r.VisitRequestId,
                VisitInstanceId = r.VisitInstanceId,
                RequestCode = r.RequestCode,
                DelegationName = r.DelegationName,
                OrganizationName = r.RegistrantOrganization,
                VisitType = r.VisitType,
                PlannedStartAt = r.PlannedStartAt,
                PlannedEndAt = r.PlannedEndAt,
                GuestCount = r.GuestCount,
                Status = r.Status,
                WaitingHours = Math.Max(0, Math.Round((nowVn - r.SubmittedAt).TotalHours, 1)),
                ActionLabel = "Duyệt & gán host / Từ chối",
            })
            .OrderByDescending(x => x.WaitingHours)
            .ToList();
        var pendingActionTotal = pendingApprovalCount;

        // ---- Close readiness: AFTER_VISIT instances, mirroring the CompleteVisitStage close rule. ----
        var closeReadinessTotal = OpCount(VisitInstanceStatus.AfterVisit);
        var closeRows = await opInstances
            .Where(ci => ci.Status == VisitInstanceStatus.AfterVisit)
            .OrderBy(ci => ci.PlannedEndAt)
            .Take(PreviewLimit)
            .Select(ci => new
            {
                ci.VisitInstanceId,
                ci.VisitRequest.RequestCode,
                // Instance row: mixed v2 shows THIS instance's detail name.
                DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
                ci.PlannedEndAt,
                ci.CurrentHostUserId,
                ci.NewsNotRequired,
                LogisticsOpenCount = ci.LogisticsItems.Count(li =>
                    li.Status != LogisticsItemStatus.Done
                    && li.Status != LogisticsItemStatus.Rejected
                    && li.Status != LogisticsItemStatus.Declined
                    && li.Status != LogisticsItemStatus.Cancelled),
                MissingHandoverSignatureCount = _db.VisitLogisticsItemHandovers.Count(h =>
                    h.LogisticsItem.VisitInstanceId == ci.VisitInstanceId
                    && (h.BorrowerSignedAt == null || h.ProviderSignedAt == null)),
                OpenActionItemCount = _db.MinuteActionItems.Count(ai =>
                    ai.Minute.VisitInstanceId == ci.VisitInstanceId
                    && ai.Status != "DONE" && ai.Status != "CANCELLED"),
                HasMinutes = _db.Minutes.Any(m => m.VisitInstanceId == ci.VisitInstanceId),
                HasPublishedNews = _db.News.Any(n => n.VisitInstanceId == ci.VisitInstanceId && n.Status == "PUBLISHED"),
                FeedbackCount = _db.Feedbacks.Count(f => f.VisitInstanceId == ci.VisitInstanceId),
            })
            .ToListAsync(cancellationToken);

        // ---- Resolve user names in one query (hosts from workload, close readiness, feedback). ----
        var userIds = hostAgg.Select(h => h.HostUserId)
            .Concat(ratingByHostRaw.Select(h => h.HostUserId))
            .Concat(closeRows.Where(r => r.CurrentHostUserId != null).Select(r => r.CurrentHostUserId!.Value))
            .Concat(lowFeedbackRows.Where(r => r.CurrentHostUserId != null).Select(r => r.CurrentHostUserId!.Value))
            .Concat(goodFeedbackRows.Where(r => r.CurrentHostUserId != null).Select(r => r.CurrentHostUserId!.Value))
            .Distinct()
            .ToList();
        if (hostUserId != null) userIds.Add(hostUserId.Value);
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FullName })
            .ToListAsync(cancellationToken);
        string? UserName(ulong? id) => id == null ? null
            : userNames.FirstOrDefault(u => u.UserId == id)?.FullName ?? $"User #{id}";

        var hostWorkload = hostAgg
            .Select(h =>
            {
                var fb = ratingByHostRaw.FirstOrDefault(x => x.HostUserId == h.HostUserId);
                return new StaffLeaderHostWorkload
                {
                    HostUserId = h.HostUserId,
                    HostName = UserName(h.HostUserId) ?? $"User #{h.HostUserId}",
                    AssignedCount = h.Assigned,
                    Upcoming7Days = h.Upcoming7,
                    BeforeVisitCount = h.Before,
                    DuringVisitCount = h.During,
                    AfterVisitCount = h.After,
                    AverageFeedbackRating = fb != null ? Math.Round(fb.Avg, 1) : null,
                };
            })
            .ToList();

        var closeReadiness = closeRows.Select(r =>
        {
            var blockers = new List<string>();
            if (r.PlannedEndAt > nowVn) blockers.Add("PLANNED_END_NOT_REACHED");
            if (r.LogisticsOpenCount > 0) blockers.Add("LOGISTICS_OPEN");
            if (r.MissingHandoverSignatureCount > 0) blockers.Add("HANDOVER_SIGNATURE_MISSING");
            if (r.OpenActionItemCount > 0) blockers.Add("ACTION_ITEMS_OPEN");
            if (!r.NewsNotRequired && !r.HasPublishedNews) blockers.Add("NEWS_MISSING");
            return new StaffLeaderCloseReadiness
            {
                VisitInstanceId = r.VisitInstanceId,
                RequestCode = r.RequestCode,
                DelegationName = r.DelegationName,
                HostName = UserName(r.CurrentHostUserId),
                PlannedEndAt = r.PlannedEndAt,
                LogisticsOpenCount = r.LogisticsOpenCount,
                MissingHandoverSignatureCount = r.MissingHandoverSignatureCount,
                OpenActionItemCount = r.OpenActionItemCount,
                HasMinutes = r.HasMinutes,
                HasPublishedNews = r.HasPublishedNews,
                NewsNotRequired = r.NewsNotRequired,
                FeedbackCount = r.FeedbackCount,
                CanClose = blockers.Count == 0,
                Blockers = blockers,
            };
        }).ToList();

        var feedbackSummary = new StaffLeaderFeedbackSummary
        {
            AverageRating = feedbackStats != null ? Math.Round(feedbackStats.Avg, 1) : null,
            TotalFeedbacks = feedbackStats?.Count ?? 0,
            LowFeedbackCount = feedbackStats?.Low ?? 0,
            LowFeedbacks = lowFeedbackRows.Select(r => new StaffLeaderFeedbackEntry
            {
                FeedbackId = r.FeedbackId,
                VisitInstanceId = r.VisitInstanceId,
                DelegationName = r.DelegationName,
                HostName = UserName(r.CurrentHostUserId),
                Rating = r.Rating,
                Comment = r.Comment,
                SubmittedAt = r.SubmittedAt,
                PlannedStartAt = r.PlannedStartAt,
            }).ToList(),
            GoodFeedbacks = goodFeedbackRows.Select(r => new StaffLeaderFeedbackEntry
            {
                FeedbackId = r.FeedbackId,
                VisitInstanceId = r.VisitInstanceId,
                DelegationName = r.DelegationName,
                HostName = UserName(r.CurrentHostUserId),
                Rating = r.Rating,
                Comment = r.Comment,
                SubmittedAt = r.SubmittedAt,
                PlannedStartAt = r.PlannedStartAt,
            }).ToList(),
            RatingByHost = ratingByHostRaw
                .Select(h => new StaffLeaderRatingByHost
                {
                    HostUserId = h.HostUserId,
                    HostName = UserName(h.HostUserId) ?? $"User #{h.HostUserId}",
                    AverageRating = Math.Round(h.Avg, 1),
                    FeedbackCount = h.Count,
                })
                .OrderBy(h => h.AverageRating)
                .ToList(),
        };

        // ---- Partner engagement: campus-owned partners + partners tied to period visits. ----
        var campusPartners = _db.Partners.AsNoTracking().Where(p => p.OwnerCampusId == campusId);

        var partnerProfileCounts = await campusPartners
            .GroupBy(p => p.ProfileStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        int PartnerProfileCount(string status) => partnerProfileCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var activePartners = await campusPartners.CountAsync(
            p => p.ProfileStatus == "APPROVED" && p.CooperationStatus == "ACTIVE", cancellationToken);
        var newPartnersInPeriod = await campusPartners.CountAsync(
            p => p.CreatedAt >= fromVn && p.CreatedAt < toVnExclusive, cancellationToken);

        var partnersByType = await campusPartners
            .Where(p => p.ProfileStatus == "APPROVED")
            .GroupBy(p => p.PartnerType)
            .Select(g => new StaffLeaderPartnerTypeCount { PartnerType = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        // Chuyến trong kỳ gắn partner: trực tiếp qua visit_requests.partner_id
        // hoặc qua visit_guest_partner_links (CONFIRMED).
        var directPartnerPairs = await instances
            .Where(ci => ci.VisitRequest.PartnerId != null)
            .Select(ci => new { PartnerId = ci.VisitRequest.PartnerId!.Value, ci.VisitInstanceId })
            .ToListAsync(cancellationToken);

        var linkPairs = await (
                from l in _db.VisitGuestPartnerLinks.AsNoTracking()
                where l.VisitInstanceId != null && l.MatchStatus == "CONFIRMED"
                join ci in instances on l.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                select new { l.PartnerId, ci.VisitInstanceId, l.GuestMemberId })
            .ToListAsync(cancellationToken);

        var visitsByPartner = directPartnerPairs.Select(x => (x.PartnerId, x.VisitInstanceId))
            .Concat(linkPairs.Select(x => (x.PartnerId, x.VisitInstanceId)))
            .Distinct()
            .GroupBy(x => x.PartnerId)
            .ToDictionary(g => g.Key, g => g.Count());
        var guestLinksByPartner = linkPairs
            .Where(x => x.GuestMemberId != null)
            .GroupBy(x => x.PartnerId)
            .ToDictionary(g => g.Key, g => g.Count());
        var visitsWithPartner = directPartnerPairs.Select(x => x.VisitInstanceId)
            .Concat(linkPairs.Select(x => x.VisitInstanceId))
            .Distinct()
            .Count();

        var topPartnerIds = visitsByPartner
            .OrderByDescending(kv => kv.Value)
            .Take(PreviewLimit)
            .Select(kv => kv.Key)
            .ToList();
        var topPartnerInfos = await _db.Partners.AsNoTracking()
            .Where(p => topPartnerIds.Contains(p.PartnerId))
            .Select(p => new { p.PartnerId, p.Name, p.PartnerType, p.Country, p.CooperationStatus, p.ProfileStatus })
            .ToListAsync(cancellationToken);

        var topPartners = topPartnerIds
            .Select(id =>
            {
                var info = topPartnerInfos.FirstOrDefault(p => p.PartnerId == id);
                return new StaffLeaderTopPartner
                {
                    PartnerId = id,
                    Name = info?.Name ?? $"Partner #{id}",
                    PartnerType = info?.PartnerType ?? "OTHER",
                    Country = info?.Country,
                    CooperationStatus = info?.CooperationStatus ?? "",
                    ProfileStatus = info?.ProfileStatus ?? "",
                    VisitCount = visitsByPartner.TryGetValue(id, out var vc) ? vc : 0,
                    LinkedGuestCount = guestLinksByPartner.TryGetValue(id, out var gc) ? gc : 0,
                };
            })
            .ToList();

        var partnerSummary = new StaffLeaderPartnerSummary
        {
            TotalPartners = PartnerProfileCount("APPROVED"),
            ActivePartners = activePartners,
            PendingApprovalPartners = PartnerProfileCount("PENDING_APPROVAL"),
            NewPartnersInPeriod = newPartnersInPeriod,
            VisitsWithPartner = visitsWithPartner,
            PartnersByType = partnersByType,
            TopPartners = topPartners,
        };

        // ---- Attention items ("cần Staff Leader xử lý" — current state unless noted). ----
        var afterVisitCount = OpCount(VisitInstanceStatus.AfterVisit);
        var attentionItems = new List<StaffLeaderAttentionItem>
        {
            new()
            {
                Type = "APPROVAL",
                Label = "Đơn cần duyệt",
                Count = pendingApprovalCount,
                Severity = pendingApprovalCount > 0 ? "WARNING" : "SUCCESS",
                TargetSection = "pending-actions",
            },
            new()
            {
                Type = "LOGISTICS_OVERDUE",
                Label = "Logistics chậm",
                Count = overdueLogisticsTotal,
                Severity = overdueLogisticsTotal > 0 ? "WARNING" : "SUCCESS",
                TargetSection = "logistics",
            },
            new()
            {
                Type = "CLOSE_PENDING",
                Label = "Hồ sơ sau tiếp khách chưa hoàn tất",
                Count = afterVisitCount,
                Severity = afterVisitCount > 0 ? "WARNING" : "SUCCESS",
                TargetSection = "close-readiness",
            },
            new()
            {
                Type = "PARTNER_APPROVAL",
                Label = "Hồ sơ partner chờ duyệt",
                Count = partnerSummary.PendingApprovalPartners,
                Severity = partnerSummary.PendingApprovalPartners > 0 ? "WARNING" : "SUCCESS",
                TargetSection = "partners",
            },
            new()
            {
                Type = "LOW_FEEDBACK",
                Label = "Feedback thấp (≤ 2 sao)",
                Count = feedbackSummary.LowFeedbackCount,
                Severity = feedbackSummary.LowFeedbackCount > 0 ? "DANGER" : "SUCCESS",
                TargetSection = "feedback",
            },
        };

        // ---- Header/filter metadata. ----
        var campusName = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == campusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"Campus #{campusId}";

        var generatedByName = _currentUser.UserId != null
            ? await _db.Users.AsNoTracking()
                .Where(u => u.UserId == _currentUser.UserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var departmentFilterName = departmentId != null
            ? await _db.Departments.AsNoTracking()
                .Where(d => d.DepartmentId == departmentId)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new StaffLeaderReportOverviewDto
        {
            GeneratedAt = nowVn,
            FilterSummary = new StaffLeaderFilterSummary
            {
                Preset = preset,
                FromDate = fromVn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ToDate = toVnExclusive.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                VisitStatus = visitStatus ?? "ALL",
                RequestStatus = requestStatus ?? "ALL",
                HostUserId = hostUserId?.ToString() ?? "ALL",
                HostName = UserName(hostUserId),
                DepartmentId = departmentId?.ToString() ?? "ALL",
                DepartmentName = departmentFilterName,
                LogisticsStatus = logisticsStatus ?? "ALL",
                FeedbackRating = feedbackRating ?? "ALL",
                CampusName = campusName,
                GeneratedByName = generatedByName,
            },
            Kpis = new StaffLeaderKpis
            {
                PendingSingleCampusApproval = pendingApprovalCount,
                RejectedInstances = OpCount(VisitInstanceStatus.Rejected),
                AssignedVisits = OpCount(VisitInstanceStatus.Assigned),
                BeforeVisit = OpCount(VisitInstanceStatus.BeforeVisit),
                DuringVisit = OpCount(VisitInstanceStatus.DuringVisit),
                AfterVisit = afterVisitCount,
                ClosedVisits = PeriodCount(VisitInstanceStatus.Closed),
                OverdueOrNotClosed = overdueOrNotClosed,
                AverageFeedbackRating = feedbackSummary.AverageRating,
                TotalGuests = totalGuests,
            },
            AttentionItems = attentionItems,
            CampusLifecyclePipeline = lifecyclePipeline,
            MonthlyTrend = monthlyTrend,
            HostWorkload = hostWorkload,
            LogisticsByDepartment = logisticsByDepartment,
            PendingActionRequests = pendingActionRequests,
            PendingActionTotal = pendingActionTotal,
            CloseReadiness = closeReadiness,
            CloseReadinessTotal = closeReadinessTotal,
            FeedbackSummary = feedbackSummary,
            PartnerSummary = partnerSummary,
        };
    }

    private static string NormalizePreset(string? preset)
    {
        var p = preset?.Trim().ToUpperInvariant();
        return p is "THIS_MONTH" or "THIS_QUARTER" or "THIS_YEAR" or "CUSTOM" ? p : "THIS_YEAR";
    }

    private static string? NormalizeFilter(string? value)
    {
        var v = value?.Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(v) || v == "ALL" ? null : v;
    }

    /// <summary>Returns [from, toExclusive) in Vietnam local time.</summary>
    private static (DateTime FromVn, DateTime ToVnExclusive) ResolvePeriodVn(
        string preset, DateTime? fromDate, DateTime? toDate, DateTime nowVn)
    {
        switch (preset)
        {
            case "THIS_MONTH":
                var monthStart = new DateTime(nowVn.Year, nowVn.Month, 1);
                return (monthStart, monthStart.AddMonths(1));
            case "THIS_QUARTER":
                var quarterStartMonth = ((nowVn.Month - 1) / 3) * 3 + 1;
                var quarterStart = new DateTime(nowVn.Year, quarterStartMonth, 1);
                return (quarterStart, quarterStart.AddMonths(3));
            case "CUSTOM":
                var from = (fromDate ?? new DateTime(nowVn.Year, 1, 1)).Date;
                var to = (toDate ?? nowVn).Date.AddDays(1);
                if (to <= from) to = from.AddDays(1);
                return (from, to);
            default: // THIS_YEAR
                return (new DateTime(nowVn.Year, 1, 1), new DateTime(nowVn.Year + 1, 1, 1));
        }
    }
}
