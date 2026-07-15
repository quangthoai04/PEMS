using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Shared;

namespace PEMS.Application.Reports.Queries.GetHoReportV2;

/// <summary>
/// Tổng hợp báo cáo hệ thống 3 phần cho HO từ dữ liệu thật (mọi campus).
/// Nhóm theo bucket thời gian được thực hiện in-memory sau khi đã lọc theo kỳ.
/// </summary>
public sealed class GetHoReportV2QueryHandler : IRequestHandler<GetHoReportV2Query, HoReportV2Dto>
{
    private const int PartnerRowLimit = 50;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetHoReportV2QueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<HoReportV2Dto> Handle(GetHoReportV2Query request, CancellationToken cancellationToken)
    {
        HoReportV2Guard.RequireHo(_currentUser);
        var nowVn = VietnamTime.Now();
        var preset = HoReportV2Guard.NormalizePreset(request.Preset);
        var (fromVn, toVnExclusive) = HoReportV2Guard.ResolvePeriodVn(preset, request.FromDate, request.ToDate, nowVn);
        var granularity = HoReportV2Guard.ResolveGranularity(fromVn, toVnExclusive);
        var buckets = HoReportV2Guard.BuildBuckets(fromVn, toVnExclusive, granularity);

        var instances = _db.VisitRequestCampuses.AsNoTracking()
            .Where(ci => ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive);

        // ═══ Phần 2: tổng quan toàn hệ thống ════════════════════════════════
        var campuses = await _db.Campuses.AsNoTracking()
            .OrderBy(c => c.CampusId)
            .Select(c => new HoV2CampusInfo { CampusId = c.CampusId, Name = c.Name })
            .ToListAsync(cancellationToken);

        // Từng instance: campus + thời điểm + trạng thái — dùng cho cả KPI, trend, bảng campus.
        var instanceRows = await instances
            .Select(ci => new { ci.CampusId, ci.PlannedStartAt, ci.Status })
            .ToListAsync(cancellationToken);

        // Đơn theo phạm vi (liên cơ sở / một cơ sở) — tính theo visit request có instance trong kỳ.
        var requestScopes = await _db.VisitRequests.AsNoTracking()
            .Where(r => r.CampusInstances.Any(ci => ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive))
            .Select(r => r.VisitScope)
            .ToListAsync(cancellationToken);

        var totalGuests = await _db.VisitRequests.AsNoTracking()
            .Where(r => r.CampusInstances.Any(ci => ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive))
            .SelectMany(r => r.GuestMembers)
            .CountAsync(cancellationToken);

        // Feedback visitor cho các đoàn trong kỳ — kèm campus để tính bảng + trung bình hệ thống.
        var feedbackRows = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "VISITOR_OVERALL" && f.VisitInstanceId != null
                join ci in instances on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                select new { ci.CampusId, Rating = (int)f.Rating })
            .ToListAsync(cancellationToken);

        // Đối tác đã duyệt theo campus sở hữu.
        var partnersByCampus = await _db.Partners.AsNoTracking()
            .Where(p => p.ProfileStatus == "APPROVED")
            .GroupBy(p => p.OwnerCampusId)
            .Select(g => new { CampusId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var totalPartners = partnersByCampus.Sum(x => x.Count);

        var trend = buckets.Select(b => new HoV2TrendPoint
        {
            Month = b.Start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            MonthLabel = b.Label,
            ByCampus = campuses.ToDictionary(
                c => c.CampusId.ToString(CultureInfo.InvariantCulture),
                c => instanceRows.Count(r => r.CampusId == c.CampusId && r.PlannedStartAt >= b.Start && r.PlannedStartAt < b.End)),
        }).ToList();

        var campusRows = campuses.Select(c =>
        {
            var ratings = feedbackRows.Where(f => f.CampusId == c.CampusId).Select(f => f.Rating).ToList();
            return new HoV2CampusRow
            {
                CampusId = c.CampusId,
                Name = c.Name,
                TotalVisits = instanceRows.Count(r => r.CampusId == c.CampusId),
                TotalPartners = partnersByCampus.FirstOrDefault(p => p.CampusId == c.CampusId)?.Count ?? 0,
                // Trung bình theo SỐ LƯỢT đánh giá (không chia theo số đoàn — có đoàn không đánh giá).
                FeedbackAverage = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : null,
                FeedbackCount = ratings.Count,
            };
        }).ToList();

        var overview = new HoV2Overview
        {
            CampusCount = campuses.Count,
            TotalVisits = instanceRows.Count,
            TotalGuests = totalGuests,
            TotalPartners = totalPartners,
            MultiCampusRequests = requestScopes.Count(s => s == "MULTI_CAMPUS"),
            SingleCampusRequests = requestScopes.Count(s => s != "MULTI_CAMPUS"),
            Completed = instanceRows.Count(r => r.Status == VisitInstanceStatus.Closed),
            Cancelled = instanceRows.Count(r => r.Status == VisitInstanceStatus.Cancelled),
            Rejected = instanceRows.Count(r => r.Status == VisitInstanceStatus.Rejected),
            FeedbackAverage = feedbackRows.Count > 0 ? Math.Round(feedbackRows.Average(f => (double)f.Rating), 1) : null,
            FeedbackCount = feedbackRows.Count,
            TrendGranularity = granularity,
            Campuses = campuses,
            Trend = trend,
            CampusRows = campusRows,
        };

        // ═══ Phần 3: đối tác toàn hệ thống ══════════════════════════════════
        var partnerCreatedDates = await _db.Partners.AsNoTracking()
            .Where(p => p.ProfileStatus == "APPROVED")
            .Select(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        // Chuyến trong kỳ gắn đối tác: trực tiếp qua partner_id hoặc qua guest link CONFIRMED.
        var directPairs = await instances
            .Where(ci => ci.VisitRequest.PartnerId != null)
            .Select(ci => new { PartnerId = ci.VisitRequest.PartnerId!.Value, ci.VisitInstanceId, ci.PlannedStartAt })
            .ToListAsync(cancellationToken);
        var linkPairs = await (
                from l in _db.VisitGuestPartnerLinks.AsNoTracking()
                where l.VisitInstanceId != null && l.MatchStatus == "CONFIRMED"
                join ci in instances on l.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                select new { l.PartnerId, ci.VisitInstanceId, ci.PlannedStartAt })
            .ToListAsync(cancellationToken);

        var partnerInstancePairs = directPairs.Select(x => (x.PartnerId, x.VisitInstanceId, x.PlannedStartAt))
            .Concat(linkPairs.Select(x => (x.PartnerId, x.VisitInstanceId, x.PlannedStartAt)))
            .Distinct()
            .ToList();
        var partnerVisitTimes = partnerInstancePairs
            .GroupBy(x => x.VisitInstanceId)
            .Select(g => g.First().PlannedStartAt)
            .ToList();

        var partnerTrend = buckets.Select(b => new HoV2PartnerTrendPoint
        {
            Month = b.Start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            MonthLabel = b.Label,
            VisitsWithPartner = partnerVisitTimes.Count(d => d >= b.Start && d < b.End),
            NewPartners = partnerCreatedDates.Count(d => d >= b.Start && d < b.End),
            CumulativePartners = partnerCreatedDates.Count(d => d < b.End),
        }).ToList();

        // Feedback visitor theo instance — để tính trung bình cho các đoàn có đối tác.
        var instanceIds = partnerInstancePairs.Select(x => x.VisitInstanceId).Distinct().ToList();
        var instanceRatings = instanceIds.Count == 0
            ? new List<(ulong InstanceId, int Rating)>()
            : (await _db.Feedbacks.AsNoTracking()
                .Where(f => f.FeedbackType == "VISITOR_OVERALL" && f.VisitInstanceId != null
                            && instanceIds.Contains(f.VisitInstanceId.Value))
                .Select(f => new { InstanceId = f.VisitInstanceId!.Value, Rating = (int)f.Rating })
                .ToListAsync(cancellationToken))
                .Select(x => (x.InstanceId, x.Rating))
                .ToList();

        var visitsByPartner = partnerInstancePairs
            .GroupBy(x => x.PartnerId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.VisitInstanceId).Distinct().ToList());

        var topPartnerIds = visitsByPartner
            .OrderByDescending(kv => kv.Value.Count)
            .Take(PartnerRowLimit)
            .Select(kv => kv.Key)
            .ToList();
        var partnerInfos = await _db.Partners.AsNoTracking()
            .Where(p => topPartnerIds.Contains(p.PartnerId))
            .Select(p => new { p.PartnerId, p.Name, p.PartnerType, p.Country })
            .ToListAsync(cancellationToken);

        var partnerRows = topPartnerIds.Select(id =>
        {
            var info = partnerInfos.FirstOrDefault(p => p.PartnerId == id);
            var visitIds = visitsByPartner[id];
            var ratings = instanceRatings.Where(r => visitIds.Contains(r.InstanceId)).Select(r => r.Rating).ToList();
            return new HoV2PartnerRow
            {
                PartnerId = id,
                Name = info?.Name ?? $"Partner #{id}",
                PartnerType = info?.PartnerType ?? "OTHER",
                Country = info?.Country,
                VisitCount = visitIds.Count,
                FeedbackAverage = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : null,
                FeedbackCount = ratings.Count,
            };
        }).ToList();

        return new HoReportV2Dto
        {
            GeneratedAt = nowVn,
            Preset = preset,
            FromDate = fromVn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ToDate = toVnExclusive.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Overview = overview,
            Partners = new HoV2Partners
            {
                TrendGranularity = granularity,
                Trend = partnerTrend,
                Rows = partnerRows,
            },
        };
    }
}
