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
namespace PEMS.Application.Reports.Queries.GetHoReportOverview;

/// <summary>
/// Aggregates the Head Office report from live data (no mock). Read-only; every query is
/// AsNoTracking and aggregation happens in the database wherever EF can translate it.
/// HO-only: the controller enforces the role, this handler re-checks as defense in depth.
/// </summary>
public sealed class GetHoReportOverviewQueryHandler
    : IRequestHandler<GetHoReportOverviewQuery, HoReportOverviewDto>
{
    private const int VnUtcOffsetHours = 7;
    private const int PendingAttentionThresholdHours = 48;
    private const int PreviewLimit = 10;
    private const int RatedVisitLimit = 5;

    private static readonly string[] OverdueCloseStatuses =
    {
        VisitInstanceStatus.Assigned,
        VisitInstanceStatus.BeforeVisit,
        VisitInstanceStatus.DuringVisit,
        VisitInstanceStatus.AfterVisit,
    };

    private static readonly (string Status, string LabelVi)[] PipelineStatuses =
    {
        (VisitInstanceStatus.WaitingRequestApproval, "Chờ xử lý tại campus"),
        (VisitInstanceStatus.Assigned, "Đã duyệt & gán host"),
        (VisitInstanceStatus.BeforeVisit, "Trước tiếp khách"),
        (VisitInstanceStatus.DuringVisit, "Đang tiếp"),
        (VisitInstanceStatus.AfterVisit, "Sau tiếp khách"),
        (VisitInstanceStatus.Closed, "Đã đóng"),
        (VisitInstanceStatus.Cancelled, "Đã hủy"),
        (VisitInstanceStatus.Rejected, "Đã từ chối"),
    };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetHoReportOverviewQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<HoReportOverviewDto> Handle(GetHoReportOverviewQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            throw new ForbiddenException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
        if (!string.Equals(_currentUser.RoleCode, "HO", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Bạn không có quyền xem báo cáo Head Office.");

        // All DATETIME columns store Vietnam wall-clock — query bounds need no offset shifting.
        var nowVn = VietnamTime.Now();

        var preset = NormalizePreset(request.Preset);
        var (fromVn, toVnExclusive) = ResolvePeriodVn(preset, request.FromDate, request.ToDate, nowVn);

        var campusId = request.CampusId is > 0 ? request.CampusId : null;
        var visitScope = NormalizeFilter(request.VisitScope);
        var requestStatus = NormalizeFilter(request.RequestStatus);
        var instanceStatus = NormalizeFilter(request.CampusInstanceStatus);
        var visitType = NormalizeFilter(request.VisitType);

        // ---- Base query 1: requests submitted in the period (approval funnel & trend). ----
        var requests = _db.VisitRequests.AsNoTracking()
            .Where(r => r.SubmittedAt >= fromVn && r.SubmittedAt < toVnExclusive);
        if (visitScope != null) requests = requests.Where(r => r.VisitScope == visitScope);
        // visit_type filter: a request matches when ANY of its campus details matches, whether or not
        // its campuses agree. visit_requests carries no visit type, so there is nothing else to match
        // on — and gating this on HasMixedCampusDetails would silently drop uniform requests.
        if (visitType != null) requests = requests.Where(r => r.CampusInstances.Any(ci => ci.FormDetail != null && ci.FormDetail.VisitType == visitType));
        if (requestStatus != null) requests = requests.Where(r => r.Status == requestStatus);
        if (campusId != null) requests = requests.Where(r => r.CampusInstances.Any(ci => ci.CampusId == campusId));

        // ---- Base query 2: campus instances planned in the period (lifecycle & campus performance). ----
        var instances = _db.VisitRequestCampuses.AsNoTracking()
            .Where(ci => ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive);
        if (campusId != null) instances = instances.Where(ci => ci.CampusId == campusId);
        if (visitScope != null) instances = instances.Where(ci => ci.VisitRequest.VisitScope == visitScope);
        // Instance-scoped: any v2 instance is filtered by ITS OWN campus detail, never the projection.
        if (visitType != null) instances = instances.Where(ci => ci.FormDetail != null && ci.FormDetail.VisitType == visitType);
        if (requestStatus != null) instances = instances.Where(ci => ci.VisitRequest.Status == requestStatus);
        if (instanceStatus != null) instances = instances.Where(ci => ci.Status == instanceStatus);

        // ---- Base query 3: current operational state (ignores time & status filters on purpose). ----
        var opInstances = _db.VisitRequestCampuses.AsNoTracking().AsQueryable();
        if (campusId != null) opInstances = opInstances.Where(ci => ci.CampusId == campusId);
        if (visitScope != null) opInstances = opInstances.Where(ci => ci.VisitRequest.VisitScope == visitScope);
        if (visitType != null) opInstances = opInstances.Where(ci => ci.FormDetail != null && ci.FormDetail.VisitType == visitType);

        // HO monitor-only (campus-independent approval): multi-campus requests that still have
        // at least one campus instance waiting for its Staff Leader's decision.
        var pendingMultiCampus = _db.VisitRequests.AsNoTracking()
            .Where(r => r.VisitScope == "MULTI_CAMPUS"
                        && r.Status != VisitRequestStatus.Cancelled
                        && r.CampusInstances.Any(ci => ci.Status == VisitInstanceStatus.WaitingRequestApproval));
        if (visitType != null) pendingMultiCampus = pendingMultiCampus.Where(r => r.CampusInstances.Any(ci => ci.FormDetail != null && ci.FormDetail.VisitType == visitType));
        if (campusId != null) pendingMultiCampus = pendingMultiCampus.Where(r => r.CampusInstances.Any(ci => ci.CampusId == campusId));

        // ---- Request status counts + guests + decision time. ----
        var requestStatusCounts = await requests
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountStatus(string status) => requestStatusCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
        var totalRequests = requestStatusCounts.Sum(x => x.Count);
        var approvedRequests = CountStatus(VisitRequestStatus.Approved);
        var partiallyApprovedRequests = CountStatus(VisitRequestStatus.PartiallyApproved);
        var rejectedRequests = CountStatus(VisitRequestStatus.Rejected);
        var cancelledRequests = CountStatus(VisitRequestStatus.Cancelled);
        var pendingRequests = CountStatus(VisitRequestStatus.PendingApproval);

        var totalGuests = await requests.SelectMany(r => r.GuestMembers).CountAsync(cancellationToken);

        // Decision time is per campus instance now (campus-independent approval):
        // submitted_at của request → decided_at của từng campus instance.
        var decisionPairs = await instances
            .Where(ci => ci.DecidedAt != null)
            .Select(ci => new { ci.VisitRequest.SubmittedAt, ci.DecidedAt })
            .ToListAsync(cancellationToken);
        double? averageDecisionHours = decisionPairs.Count > 0
            ? Math.Round(decisionPairs.Average(p => (p.DecidedAt!.Value - p.SubmittedAt).TotalHours), 1)
            : null;

        // ---- Monthly trend (grouped by Vietnam-local month of submitted_at). ----
        var trendRaw = await requests
            .GroupBy(r => new { r.SubmittedAt.Year, r.SubmittedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Count(),
                Single = g.Count(r => r.VisitScope == "SINGLE_CAMPUS"),
                Multi = g.Count(r => r.VisitScope == "MULTI_CAMPUS"),
                Approved = g.Count(r => r.Status == VisitRequestStatus.Approved),
                PartiallyApproved = g.Count(r => r.Status == VisitRequestStatus.PartiallyApproved),
                Rejected = g.Count(r => r.Status == VisitRequestStatus.Rejected),
                Cancelled = g.Count(r => r.Status == VisitRequestStatus.Cancelled),
                Guests = g.Sum(r => r.GuestMembers.Count),
            })
            .ToListAsync(cancellationToken);

        var monthlyTrend = new List<HoMonthlyTrendDto>();
        var lastMonthVn = toVnExclusive.AddDays(-1);
        for (var cursor = new DateTime(fromVn.Year, fromVn.Month, 1);
             cursor <= new DateTime(lastMonthVn.Year, lastMonthVn.Month, 1);
             cursor = cursor.AddMonths(1))
        {
            var row = trendRaw.FirstOrDefault(t => t.Year == cursor.Year && t.Month == cursor.Month);
            monthlyTrend.Add(new HoMonthlyTrendDto
            {
                Month = cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                MonthLabel = $"T{cursor.Month}/{cursor.Year}",
                TotalRequests = row?.Total ?? 0,
                SingleCampusRequests = row?.Single ?? 0,
                MultiCampusRequests = row?.Multi ?? 0,
                Approved = row?.Approved ?? 0,
                PartiallyApproved = row?.PartiallyApproved ?? 0,
                Rejected = row?.Rejected ?? 0,
                Cancelled = row?.Cancelled ?? 0,
                TotalGuests = row?.Guests ?? 0,
            });
        }

        // ---- Lifecycle pipeline + instance KPIs. ----
        var instanceStatusCounts = await instances
            .GroupBy(ci => ci.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var totalInstances = instanceStatusCounts.Sum(x => x.Count);
        int CountInstanceStatus(string status) => instanceStatusCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var lifecyclePipeline = PipelineStatuses
            .Select(p => new HoLifecyclePipelineItemDto
            {
                Status = p.Status,
                LabelVi = p.LabelVi,
                Count = CountInstanceStatus(p.Status),
                Percentage = totalInstances > 0
                    ? Math.Round(CountInstanceStatus(p.Status) * 100.0 / totalInstances, 1)
                    : 0,
            })
            .ToList();

        var closedInstances = CountInstanceStatus(VisitInstanceStatus.Closed);
        var cancelledInstances = CountInstanceStatus(VisitInstanceStatus.Cancelled);
        var activeInstances = totalInstances - closedInstances - cancelledInstances;

        var overdueCloseInstances = await instances.CountAsync(
            ci => ci.PlannedEndAt < nowVn && OverdueCloseStatuses.Contains(ci.Status), cancellationToken);

        // ---- Campus performance. ----
        var campuses = await _db.Campuses.AsNoTracking()
            .OrderBy(c => c.CampusCode)
            .Select(c => new { c.CampusId, c.CampusCode, c.Name })
            .ToListAsync(cancellationToken);

        var campusAgg = await instances
            .GroupBy(ci => ci.CampusId)
            .Select(g => new
            {
                CampusId = g.Key,
                Total = g.Count(),
                WaitingRequestApproval = g.Count(ci => ci.Status == VisitInstanceStatus.WaitingRequestApproval),
                Rejected = g.Count(ci => ci.Status == VisitInstanceStatus.Rejected),
                Assigned = g.Count(ci => ci.Status == VisitInstanceStatus.Assigned),
                BeforeVisit = g.Count(ci => ci.Status == VisitInstanceStatus.BeforeVisit),
                DuringVisit = g.Count(ci => ci.Status == VisitInstanceStatus.DuringVisit),
                AfterVisit = g.Count(ci => ci.Status == VisitInstanceStatus.AfterVisit),
                Closed = g.Count(ci => ci.Status == VisitInstanceStatus.Closed),
                Cancelled = g.Count(ci => ci.Status == VisitInstanceStatus.Cancelled),
                Overdue = g.Count(ci => ci.PlannedEndAt < nowVn && OverdueCloseStatuses.Contains(ci.Status)),
                Guests = g.Sum(ci => ci.VisitRequest.GuestMembers.Count),
            })
            .ToListAsync(cancellationToken);

        var campusFeedback = await (
                from ci in instances
                join f in _db.Feedbacks.AsNoTracking() on (ulong?)ci.VisitInstanceId equals f.VisitInstanceId
                group f by ci.CampusId into g
                select new { CampusId = g.Key, Avg = g.Average(f => (double)f.Rating), Count = g.Count() })
            .ToListAsync(cancellationToken);

        var campusPerformance = campuses
            .Where(c => campusId == null || c.CampusId == campusId)
            .Select(c =>
            {
                var agg = campusAgg.FirstOrDefault(x => x.CampusId == c.CampusId);
                var fb = campusFeedback.FirstOrDefault(x => x.CampusId == c.CampusId);
                return new HoCampusPerformanceDto
                {
                    CampusId = c.CampusId,
                    CampusCode = c.CampusCode,
                    CampusName = c.Name,
                    TotalInstances = agg?.Total ?? 0,
                    WaitingRequestApproval = agg?.WaitingRequestApproval ?? 0,
                    Rejected = agg?.Rejected ?? 0,
                    Assigned = agg?.Assigned ?? 0,
                    BeforeVisit = agg?.BeforeVisit ?? 0,
                    DuringVisit = agg?.DuringVisit ?? 0,
                    AfterVisit = agg?.AfterVisit ?? 0,
                    Closed = agg?.Closed ?? 0,
                    Cancelled = agg?.Cancelled ?? 0,
                    OverdueCloseCount = agg?.Overdue ?? 0,
                    GuestCount = agg?.Guests ?? 0,
                    AverageFeedbackRating = fb != null ? Math.Round(fb.Avg, 1) : null,
                };
            })
            .ToList();

        // ---- Pending multi-campus requests needing HO action (current state). ----
        var multiCampusPendingTotal = await pendingMultiCampus.CountAsync(cancellationToken);
        var pendingOver48h = await pendingMultiCampus.CountAsync(
            r => r.SubmittedAt <= nowVn.AddHours(-PendingAttentionThresholdHours), cancellationToken);

        var pendingRows = await pendingMultiCampus
            .OrderBy(r => r.SubmittedAt)
            .Take(PreviewLimit)
            .Select(r => new
            {
                r.VisitRequestId,
                r.RequestCode,
                // Request-level row: a MIXED v2 request has no single business name (plan §8.3).
                DelegationName = r.HasMixedCampusDetails ? "Khác nhau theo cơ sở" : (r.CampusInstances.Select(ci => ci.FormDetail.DelegationName).FirstOrDefault() ?? r.RequestCode ?? "Đoàn khách"),
                r.RegistrantOrganization,
                r.SubmittedAt,
                PlannedStartAt = r.CampusInstances.Min(ci => (DateTime?)ci.PlannedStartAt),
                PlannedEndAt = r.CampusInstances.Max(ci => (DateTime?)ci.PlannedEndAt),
                CampusCount = r.CampusInstances.Count,
                GuestCount = r.GuestMembers.Count,
                r.Status,
            })
            .ToListAsync(cancellationToken);

        var multiCampusPendingRequests = pendingRows
            .Select(r => new HoPendingMultiCampusRequestDto
            {
                RequestId = r.VisitRequestId,
                RequestCode = r.RequestCode,
                DelegationName = r.DelegationName,
                OrganizationName = r.RegistrantOrganization,
                SubmittedAt = r.SubmittedAt,
                PlannedStartAt = r.PlannedStartAt,
                PlannedEndAt = r.PlannedEndAt,
                RequestedCampusCount = r.CampusCount,
                GuestCount = r.GuestCount,
                WaitingHours = Math.Round((nowVn - r.SubmittedAt).TotalHours, 1),
                Status = r.Status,
            })
            .ToList();

        // ---- Close readiness: AFTER_VISIT instances, mirroring the CompleteVisitStage close rule. ----
        var closeReadinessTotal = await opInstances
            .CountAsync(ci => ci.Status == VisitInstanceStatus.AfterVisit, cancellationToken);

        var closeRows = await opInstances
            .Where(ci => ci.Status == VisitInstanceStatus.AfterVisit)
            .OrderBy(ci => ci.PlannedEndAt)
            .Take(PreviewLimit)
            .Select(ci => new
            {
                ci.VisitInstanceId,
                ci.VisitRequestId,
                ci.VisitRequest.RequestCode,
                // Instance row: every instance titles from ITS OWN detail, whether or not the request's
                // campuses agree — a sibling campus is never a stand-in for this one.
                DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
                ci.CampusId,
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

        var hostIds = closeRows.Where(r => r.CurrentHostUserId != null).Select(r => r.CurrentHostUserId!.Value).Distinct().ToList();
        var hostNames = await _db.Users.AsNoTracking()
            .Where(u => hostIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FullName })
            .ToListAsync(cancellationToken);

        var campusNameById = campuses.ToDictionary(c => c.CampusId, c => c.Name);
        var closeReadiness = closeRows.Select(r =>
        {
            var blockers = new List<string>();
            if (r.PlannedEndAt > nowVn) blockers.Add("PLANNED_END_NOT_REACHED");
            if (r.LogisticsOpenCount > 0) blockers.Add("LOGISTICS_OPEN");
            if (r.MissingHandoverSignatureCount > 0) blockers.Add("HANDOVER_SIGNATURE_MISSING");
            if (r.OpenActionItemCount > 0) blockers.Add("ACTION_ITEMS_OPEN");
            if (!r.NewsNotRequired && !r.HasPublishedNews) blockers.Add("NEWS_MISSING");
            return new HoCloseReadinessDto
            {
                VisitInstanceId = r.VisitInstanceId,
                RequestId = r.VisitRequestId,
                RequestCode = r.RequestCode,
                DelegationName = r.DelegationName,
                CampusName = campusNameById.TryGetValue(r.CampusId, out var name) ? name : $"Campus #{r.CampusId}",
                PlannedEndAt = r.PlannedEndAt,
                HostName = r.CurrentHostUserId != null
                    ? hostNames.FirstOrDefault(h => h.UserId == r.CurrentHostUserId)?.FullName
                    : null,
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

        // ---- Feedback summary (feedbacks submitted in the period; campus filter via instance). ----
        var feedbacks = _db.Feedbacks.AsNoTracking()
            .Where(f => f.SubmittedAt >= fromVn && f.SubmittedAt < toVnExclusive);
        if (campusId != null)
        {
            feedbacks = feedbacks.Where(f =>
                f.VisitInstanceId != null
                && _db.VisitRequestCampuses.Any(ci => ci.VisitInstanceId == f.VisitInstanceId && ci.CampusId == campusId));
        }

        var feedbackStats = await feedbacks
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Avg = g.Average(f => (double)f.Rating),
                Low = g.Count(f => f.Rating <= 2),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var ratedVisitsRaw = await (
                from f in feedbacks
                where f.VisitInstanceId != null
                join ci in _db.VisitRequestCampuses.AsNoTracking() on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                group new { f, ci } by new
                {
                    ci.VisitInstanceId,
                    // Instance group: EVERY v2 instance titles from THIS instance's canonical detail.
                    DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
                    ci.CampusId,
                    ci.PlannedStartAt,
                } into g
                select new
                {
                    g.Key.VisitInstanceId,
                    g.Key.DelegationName,
                    g.Key.CampusId,
                    g.Key.PlannedStartAt,
                    Avg = g.Average(x => (double)x.f.Rating),
                    Count = g.Count(),
                })
            .ToListAsync(cancellationToken);

        var ratedVisits = ratedVisitsRaw
            .Select(v => new
            {
                v.Avg,
                v.Count,
                v.CampusId,
                Dto = new HoRatedVisitDto
                {
                    VisitInstanceId = v.VisitInstanceId,
                    DelegationName = v.DelegationName,
                    CampusName = campusNameById.TryGetValue(v.CampusId, out var ratedCampusName)
                        ? ratedCampusName
                        : $"Campus #{v.CampusId}",
                    AverageRating = Math.Round(v.Avg, 1),
                    FeedbackCount = v.Count,
                    PlannedStartAt = v.PlannedStartAt,
                },
            })
            .ToList();

        var feedbackSummary = new HoFeedbackSummaryDto
        {
            AverageRating = feedbackStats != null ? Math.Round(feedbackStats.Avg, 1) : null,
            TotalFeedbacks = feedbackStats?.Count ?? 0,
            LowFeedbackCount = feedbackStats?.Low ?? 0,
            TopRatedVisits = ratedVisits.OrderByDescending(v => v.Avg).ThenByDescending(v => v.Count)
                .Take(RatedVisitLimit).Select(v => v.Dto).ToList(),
            LowRatedVisits = ratedVisits.OrderBy(v => v.Avg).ThenByDescending(v => v.Count)
                .Take(RatedVisitLimit).Select(v => v.Dto).ToList(),
            RatingByCampus = ratedVisits
                .GroupBy(v => v.CampusId)
                .Select(g => new HoCampusRatingDto
                {
                    CampusId = g.Key,
                    CampusName = campusNameById.TryGetValue(g.Key, out var cn) ? cn : $"Campus #{g.Key}",
                    AverageRating = Math.Round(g.Sum(x => x.Avg * x.Count) / g.Sum(x => x.Count), 1),
                    FeedbackCount = g.Sum(x => x.Count),
                })
                .OrderBy(c => c.CampusName)
                .ToList(),
        };

        // ---- Content & email effectiveness. ----
        var newsBase = _db.News.AsNoTracking().AsQueryable();
        if (campusId != null) newsBase = newsBase.Where(n => n.CampusId == campusId);

        var publishedNewsCount = await newsBase.CountAsync(
            n => n.PublishedAt != null && n.PublishedAt >= fromVn && n.PublishedAt < toVnExclusive, cancellationToken);
        var pendingNewsCount = await newsBase.CountAsync(n => n.Status == "PENDING_REVIEW", cancellationToken);

        var instancesMissingNews = await opInstances.CountAsync(ci =>
            (ci.Status == VisitInstanceStatus.AfterVisit || ci.Status == VisitInstanceStatus.Closed)
            && !ci.NewsNotRequired
            && !_db.News.Any(n => n.VisitInstanceId == ci.VisitInstanceId && n.Status == "PUBLISHED"),
            cancellationToken);

        var emailStats = await _db.SentEmails.AsNoTracking()
            .Where(e => e.CreatedAt >= fromVn && e.CreatedAt < toVnExclusive)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Sent = g.Count(e => e.Status == "SENT"),
                Failed = g.Count(e => e.Status == "FAILED"),
            })
            .FirstOrDefaultAsync(cancellationToken);
        var emailSent = emailStats?.Sent ?? 0;
        var emailFailed = emailStats?.Failed ?? 0;

        var tokenStats = await _db.EmailActionTokens.AsNoTracking()
            .Where(t => t.CreatedAt >= fromVn && t.CreatedAt < toVnExclusive)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Responded = g.Count(t => t.UsedAt != null),
                Expired = g.Count(t => t.UsedAt == null && t.ExpiresAt < nowVn),
                Pending = g.Count(t => t.UsedAt == null && t.ExpiresAt >= nowVn),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var contentEmailSummary = new HoContentEmailSummaryDto
        {
            PublishedNewsCount = publishedNewsCount,
            PendingNewsCount = pendingNewsCount,
            InstancesMissingNewsCount = instancesMissingNews,
            EmailSentCount = emailSent,
            EmailFailedCount = emailFailed,
            EmailDeliveredRate = emailSent + emailFailed > 0
                ? Math.Round(emailSent * 100.0 / (emailSent + emailFailed), 1)
                : null,
            ActionTokenRespondedCount = tokenStats?.Responded ?? 0,
            ActionTokenExpiredCount = tokenStats?.Expired ?? 0,
            ActionTokenPendingCount = tokenStats?.Pending ?? 0,
        };

        // ---- Partner engagement (current-state profile counts + visits tied to partners in the period). ----
        var partnersBase = _db.Partners.AsNoTracking().AsQueryable();
        if (campusId != null) partnersBase = partnersBase.Where(p => p.OwnerCampusId == campusId);

        var partnerProfileCounts = await partnersBase
            .GroupBy(p => p.ProfileStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        int PartnerProfileCount(string status) => partnerProfileCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var activePartners = await partnersBase.CountAsync(
            p => p.ProfileStatus == "APPROVED" && p.CooperationStatus == "ACTIVE", cancellationToken);
        var newPartnersInPeriod = await partnersBase.CountAsync(
            p => p.CreatedAt >= fromVn && p.CreatedAt < toVnExclusive, cancellationToken);

        var partnersByType = await partnersBase
            .Where(p => p.ProfileStatus == "APPROVED")
            .GroupBy(p => p.PartnerType)
            .Select(g => new HoPartnerTypeCountDto { PartnerType = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        var partnersByCampusRaw = await partnersBase
            .GroupBy(p => p.OwnerCampusId)
            .Select(g => new
            {
                CampusId = g.Key,
                Approved = g.Count(p => p.ProfileStatus == "APPROVED"),
                Pending = g.Count(p => p.ProfileStatus == "PENDING_APPROVAL"),
                NewInPeriod = g.Count(p => p.CreatedAt >= fromVn && p.CreatedAt < toVnExclusive),
            })
            .ToListAsync(cancellationToken);
        var partnersByCampus = partnersByCampusRaw
            .Select(x => new HoPartnerCampusCountDto
            {
                CampusId = x.CampusId,
                CampusName = campusNameById.TryGetValue(x.CampusId, out var pcName) ? pcName : $"Campus #{x.CampusId}",
                ApprovedCount = x.Approved,
                PendingCount = x.Pending,
                NewInPeriod = x.NewInPeriod,
            })
            .OrderBy(x => x.CampusName)
            .ToList();

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
            .Select(p => new { p.PartnerId, p.Name, p.PartnerType, p.Country, p.OwnerCampusId, p.CooperationStatus })
            .ToListAsync(cancellationToken);

        var topPartners = topPartnerIds
            .Select(id =>
            {
                var info = topPartnerInfos.FirstOrDefault(p => p.PartnerId == id);
                return new HoTopPartnerDto
                {
                    PartnerId = id,
                    Name = info?.Name ?? $"Partner #{id}",
                    PartnerType = info?.PartnerType ?? "OTHER",
                    Country = info?.Country,
                    OwnerCampusName = info != null && campusNameById.TryGetValue(info.OwnerCampusId, out var ocName)
                        ? ocName
                        : "—",
                    CooperationStatus = info?.CooperationStatus ?? "",
                    VisitCount = visitsByPartner.TryGetValue(id, out var vc) ? vc : 0,
                    LinkedGuestCount = guestLinksByPartner.TryGetValue(id, out var gc) ? gc : 0,
                };
            })
            .ToList();

        var partnerSummary = new HoPartnerSummaryDto
        {
            TotalPartners = PartnerProfileCount("APPROVED"),
            ActivePartners = activePartners,
            PendingApprovalPartners = PartnerProfileCount("PENDING_APPROVAL"),
            NewPartnersInPeriod = newPartnersInPeriod,
            VisitsWithPartner = visitsWithPartner,
            PartnersByType = partnersByType,
            PartnersByCampus = partnersByCampus,
            TopPartners = topPartners,
        };

        // ---- Attention items ("needs HO attention" — current state unless noted). ----
        var afterVisitNotClosed = closeReadinessTotal;
        var closedWithoutFeedback = await instances.CountAsync(ci =>
            ci.Status == VisitInstanceStatus.Closed
            && !_db.Feedbacks.Any(f => f.VisitInstanceId == ci.VisitInstanceId), cancellationToken);
        var expiredTokens = contentEmailSummary.ActionTokenExpiredCount;
        var lowFeedbackCount = feedbackSummary.LowFeedbackCount;

        var attentionItems = new List<HoAttentionItemDto>
        {
            new()
            {
                Key = "pendingCampusProcessingOver48h",
                Label = $"Đơn liên cơ sở còn campus chờ xử lý quá {PendingAttentionThresholdHours}h",
                Count = pendingOver48h,
                Severity = pendingOver48h > 0 ? "DANGER" : "SUCCESS",
                Description = "Đơn MULTI_CAMPUS còn campus instance chờ Staff Leader của campus xử lý lâu hơn 48 giờ (HO chỉ theo dõi, không duyệt).",
                TargetSection = "pending-requests",
            },
            new()
            {
                Key = "afterVisitNotClosed",
                Label = "Sau tiếp khách chưa đóng hồ sơ",
                Count = afterVisitNotClosed,
                Severity = afterVisitNotClosed > 0 ? "WARNING" : "SUCCESS",
                Description = "Campus instance đang ở AFTER_VISIT, cần hoàn tất điều kiện để đóng.",
                TargetSection = "close-readiness",
            },
            new()
            {
                Key = "closedWithoutFeedback",
                Label = "Đã đóng nhưng thiếu feedback",
                Count = closedWithoutFeedback,
                Severity = closedWithoutFeedback > 0 ? "INFO" : "SUCCESS",
                Description = "Instance CLOSED trong kỳ báo cáo nhưng không có feedback nào.",
                TargetSection = "feedback-content",
            },
            new()
            {
                Key = "missingNewsOrNewsNotConfirmed",
                Label = "Thiếu bài tin tức",
                Count = instancesMissingNews,
                Severity = instancesMissingNews > 0 ? "WARNING" : "SUCCESS",
                Description = "Instance sau tiếp khách/đã đóng chưa có news PUBLISHED và Host chưa xác nhận không cần tin.",
                TargetSection = "feedback-content",
            },
            new()
            {
                Key = "emailActionExpiredOrNoResponse",
                Label = "Action email hết hạn không phản hồi",
                Count = expiredTokens,
                Severity = expiredTokens > 0 ? "WARNING" : "SUCCESS",
                Description = "Token hành động qua email trong kỳ đã hết hạn mà không được phản hồi.",
                TargetSection = "feedback-content",
            },
            new()
            {
                Key = "lowFeedbackCount",
                Label = "Feedback thấp (≤ 2 sao)",
                Count = lowFeedbackCount,
                Severity = lowFeedbackCount > 0 ? "DANGER" : "SUCCESS",
                Description = "Feedback có điểm từ 2 trở xuống trong kỳ báo cáo.",
                TargetSection = "feedback-content",
            },
        };

        // ---- Feedback KPI: average rating in the period (all feedback types, real ratings only). ----
        var generatedByName = _currentUser.UserId != null
            ? await _db.Users.AsNoTracking()
                .Where(u => u.UserId == _currentUser.UserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var campusName = campusId != null
            ? campusNameById.TryGetValue(campusId.Value, out var selectedName) ? selectedName : $"Campus #{campusId}"
            : "Tất cả cơ sở";

        return new HoReportOverviewDto
        {
            GeneratedAt = nowVn,
            FilterSummary = new HoReportFilterSummaryDto
            {
                Preset = preset,
                FromDate = fromVn,
                ToDate = toVnExclusive.AddDays(-1),
                CampusId = campusId,
                CampusName = campusName,
                VisitScope = visitScope ?? "ALL",
                RequestStatus = requestStatus ?? "ALL",
                CampusInstanceStatus = instanceStatus ?? "ALL",
                VisitType = visitType ?? "ALL",
                GeneratedByUserId = _currentUser.UserId,
                GeneratedByName = generatedByName,
            },
            Kpis = new HoReportKpisDto
            {
                TotalRequests = totalRequests,
                MultiCampusPending = multiCampusPendingTotal,
                PendingRequests = pendingRequests,
                ApprovedRequests = approvedRequests,
                PartiallyApprovedRequests = partiallyApprovedRequests,
                RejectedRequests = rejectedRequests,
                CancelledRequests = cancelledRequests,
                ActiveCampusInstances = activeInstances,
                ClosedCampusInstances = closedInstances,
                OverdueCloseInstances = overdueCloseInstances,
                AverageDecisionHours = averageDecisionHours,
                AverageFeedbackRating = feedbackSummary.AverageRating,
                TotalGuests = totalGuests,
            },
            AttentionItems = attentionItems,
            MonthlyTrend = monthlyTrend,
            ApprovalBreakdown = new HoApprovalBreakdownDto
            {
                Approved = approvedRequests,
                PartiallyApproved = partiallyApprovedRequests,
                Rejected = rejectedRequests,
                Pending = pendingRequests,
                Cancelled = cancelledRequests,
                ApprovalRate = totalRequests > 0 ? Math.Round(approvedRequests * 100.0 / totalRequests, 1) : 0,
                RejectionRate = totalRequests > 0 ? Math.Round(rejectedRequests * 100.0 / totalRequests, 1) : 0,
                AverageDecisionHours = averageDecisionHours,
            },
            CampusPerformance = campusPerformance,
            LifecyclePipeline = lifecyclePipeline,
            MultiCampusPendingRequests = multiCampusPendingRequests,
            MultiCampusPendingTotal = multiCampusPendingTotal,
            CloseReadiness = closeReadiness,
            CloseReadinessTotal = closeReadinessTotal,
            FeedbackSummary = feedbackSummary,
            ContentAndEmailSummary = contentEmailSummary,
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
