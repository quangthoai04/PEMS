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

namespace PEMS.Application.Reports.Queries.GetStaffLeaderReportV2;

/// <summary>
/// Tổng hợp báo cáo campus 3 phần cho Staff Leader từ dữ liệu thật.
/// Các phép cộng theo thời lượng/nhóm nhỏ được thực hiện in-memory sau khi
/// đã lọc đúng campus + kỳ báo cáo (khối lượng dữ liệu 1 campus/kỳ là nhỏ).
/// </summary>
public sealed class GetStaffLeaderReportV2QueryHandler
    : IRequestHandler<GetStaffLeaderReportV2Query, StaffLeaderReportV2Dto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStaffLeaderReportV2QueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StaffLeaderReportV2Dto> Handle(GetStaffLeaderReportV2Query request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderReportV2Guard.RequireStaffLeaderCampus(_currentUser);
        var nowVn = VietnamTime.Now();
        var preset = StaffLeaderReportV2Guard.NormalizePreset(request.Preset);
        var (fromVn, toVnExclusive) = StaffLeaderReportV2Guard.ResolvePeriodVn(preset, request.FromDate, request.ToDate, nowVn);

        var instances = _db.VisitRequestCampuses.AsNoTracking()
            .Where(ci => ci.CampusId == campusId && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive);

        // ═══ Phần 1: đoàn tiếp khách ═══════════════════════════════════════
        var statusCounts = await instances
            .GroupBy(ci => ci.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        int StatusCount(string s) => statusCounts.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        var totalVisits = statusCounts.Sum(x => x.Count);
        var completed = StatusCount(VisitInstanceStatus.Closed);
        var rejected = StatusCount(VisitInstanceStatus.Rejected);
        var cancelled = StatusCount(VisitInstanceStatus.Cancelled);

        var totalGuests = await instances
            .SelectMany(ci => ci.VisitRequest.GuestMembers)
            .CountAsync(cancellationToken);

        var visitFeedback = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "VISITOR_OVERALL" && f.VisitInstanceId != null
                join ci in instances on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                select (int)f.Rating)
            .ToListAsync(cancellationToken);

        // Đối tác của campus + tiến độ hợp tác theo tháng (đường lũy kế + chuyến gắn đối tác).
        var campusPartners = _db.Partners.AsNoTracking()
            .Where(p => p.OwnerCampusId == campusId && p.ProfileStatus == "APPROVED");
        var partnerCreatedDates = await campusPartners
            .Select(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var directPartnerVisits = await instances
            .Where(ci => ci.VisitRequest.PartnerId != null)
            .Select(ci => new { ci.VisitInstanceId, ci.PlannedStartAt })
            .ToListAsync(cancellationToken);
        var linkedPartnerVisits = await (
                from l in _db.VisitGuestPartnerLinks.AsNoTracking()
                where l.VisitInstanceId != null && l.MatchStatus == "CONFIRMED"
                join ci in instances on l.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                select new { ci.VisitInstanceId, ci.PlannedStartAt })
            .ToListAsync(cancellationToken);
        var partnerVisitMonths = directPartnerVisits.Concat(linkedPartnerVisits)
            .GroupBy(x => x.VisitInstanceId)
            .Select(g => g.First().PlannedStartAt)
            .ToList();

        // Độ chi tiết trục thời gian theo độ dài kỳ lọc: ≥3 năm → năm; >3 tháng → tháng;
        // ≤3 tháng → tuần; ≤2 tuần → ngày; trong 1 ngày → giờ (mốc nhỏ nhất là 1 giờ).
        var periodDays = (toVnExclusive - fromVn).TotalDays;
        var granularity = periodDays >= 3 * 365 ? "YEAR"
            : periodDays > 92 ? "MONTH"
            : periodDays > 14 ? "WEEK"
            : periodDays > 1 ? "DAY"
            : "HOUR";

        var partnerTrend = new List<StaffLeaderV2PartnerTrendPoint>();
        var cursor = granularity switch
        {
            "YEAR" => new DateTime(fromVn.Year, 1, 1),
            "MONTH" => new DateTime(fromVn.Year, fromVn.Month, 1),
            _ => fromVn, // WEEK/DAY/HOUR chạy đúng từ đầu kỳ lọc
        };
        while (cursor < toVnExclusive)
        {
            var next = granularity switch
            {
                "YEAR" => cursor.AddYears(1),
                "MONTH" => cursor.AddMonths(1),
                "WEEK" => cursor.AddDays(7),
                "DAY" => cursor.AddDays(1),
                _ => cursor.AddHours(1),
            };
            var label = granularity switch
            {
                "YEAR" => cursor.Year.ToString(CultureInfo.InvariantCulture),
                "MONTH" => $"T{cursor.Month}/{cursor.Year}",
                "WEEK" => cursor.ToString("dd/MM", CultureInfo.InvariantCulture),
                "DAY" => cursor.ToString("dd/MM", CultureInfo.InvariantCulture),
                _ => cursor.ToString("HH:00", CultureInfo.InvariantCulture),
            };
            partnerTrend.Add(new StaffLeaderV2PartnerTrendPoint
            {
                Month = cursor.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                MonthLabel = label,
                VisitsWithPartner = partnerVisitMonths.Count(d => d >= cursor && d < next),
                NewPartners = partnerCreatedDates.Count(d => d >= cursor && d < next),
                CumulativePartners = partnerCreatedDates.Count(d => d < next),
            });
            cursor = next;
        }

        var visits = new StaffLeaderV2Visits
        {
            TotalVisits = totalVisits,
            TotalGuests = totalGuests,
            Completed = completed,
            Rejected = rejected,
            Cancelled = cancelled,
            NotCompleted = Math.Max(0, totalVisits - completed - rejected - cancelled),
            FeedbackCount = visitFeedback.Count,
            FeedbackTotalStars = visitFeedback.Sum(),
            FeedbackAverage = visitFeedback.Count > 0 ? Math.Round(visitFeedback.Average(), 1) : null,
            TotalPartners = partnerCreatedDates.Count,
            TrendGranularity = granularity,
            PartnerTrend = partnerTrend,
        };

        // ═══ Phần 2: nhân sự IC (gồm Staff Leader) + student ═══════════════
        var personnelUsers = await _db.Users.AsNoTracking()
            .Where(u => u.PrimaryCampusId == campusId && u.Status == "ACTIVE"
                        && (u.Role.RoleCode == "STAFF" || u.Role.RoleCode == "STUDENT"))
            .Select(u => new { u.UserId, u.FullName, u.Email, RoleCode = u.Role.RoleCode, u.SubRole })
            .ToListAsync(cancellationToken);

        // Đoàn host trong kỳ (staff) — giờ làm việc tính theo thời lượng kế hoạch của đoàn.
        var hostedRows = await instances
            .Where(ci => ci.CurrentHostUserId != null)
            .Select(ci => new { UserId = ci.CurrentHostUserId!.Value, ci.PlannedStartAt, ci.PlannedEndAt, ci.Status })
            .ToListAsync(cancellationToken);

        // Tham gia trong kỳ (student + lượt từ chối của mọi nhân sự).
        var participantRows = await (
                from p in _db.VisitParticipants.AsNoTracking()
                join ci in instances on p.VisitInstanceId equals ci.VisitInstanceId
                where p.Status != "REMOVED"
                select new { p.UserId, p.Status, ci.VisitInstanceId, ci.PlannedStartAt, ci.PlannedEndAt, InstanceStatus = ci.Status })
            .ToListAsync(cancellationToken);

        // Feedback visitor theo host (staff) — đúng các đoàn trong kỳ.
        var visitorFbByHost = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "VISITOR_OVERALL" && f.VisitInstanceId != null
                join ci in instances on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                where ci.CurrentHostUserId != null
                select new { HostId = ci.CurrentHostUserId!.Value, Rating = (int)f.Rating })
            .ToListAsync(cancellationToken);

        // Feedback host đánh giá người tham gia (student) — chia trung bình theo SỐ LẦN feedback.
        var participantFb = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "HOST_PARTICIPANT" && f.TargetUserId != null && f.VisitInstanceId != null
                join ci in instances on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                select new { UserId = f.TargetUserId!.Value, Rating = (int)f.Rating })
            .ToListAsync(cancellationToken);

        static bool CountsForHours(string instanceStatus) =>
            instanceStatus != VisitInstanceStatus.Cancelled && instanceStatus != VisitInstanceStatus.Rejected;

        var personnelRows = new List<StaffLeaderV2PersonnelRow>();
        foreach (var u in personnelUsers)
        {
            var isStudent = string.Equals(u.RoleCode, "STUDENT", StringComparison.OrdinalIgnoreCase);
            int visitCount;
            double totalHours;
            List<int> ratings;
            if (isStudent)
            {
                var joined = participantRows
                    .Where(p => p.UserId == u.UserId && p.Status == "ACCEPTED")
                    .GroupBy(p => p.VisitInstanceId)
                    .Select(g => g.First())
                    .ToList();
                visitCount = joined.Count;
                totalHours = joined.Where(p => CountsForHours(p.InstanceStatus))
                    .Sum(p => Math.Max(0, (p.PlannedEndAt - p.PlannedStartAt).TotalHours));
                ratings = participantFb.Where(f => f.UserId == u.UserId).Select(f => f.Rating).ToList();
            }
            else
            {
                var hosted = hostedRows.Where(h => h.UserId == u.UserId).ToList();
                visitCount = hosted.Count;
                totalHours = hosted.Where(h => CountsForHours(h.Status))
                    .Sum(h => Math.Max(0, (h.PlannedEndAt - h.PlannedStartAt).TotalHours));
                ratings = visitorFbByHost.Where(f => f.HostId == u.UserId).Select(f => f.Rating).ToList();
            }

            personnelRows.Add(new StaffLeaderV2PersonnelRow
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Role = isStudent ? "STUDENT"
                    : string.Equals(u.SubRole, "LEADER", StringComparison.OrdinalIgnoreCase) ? "STAFF_LEADER" : "STAFF",
                VisitCount = visitCount,
                TotalHours = Math.Round(totalHours, 1),
                FeedbackAverage = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : null,
                FeedbackCount = ratings.Count,
                DeclinedCount = participantRows.Count(p => p.UserId == u.UserId && p.Status == "DECLINED"),
            });
        }
        personnelRows = personnelRows
            .OrderBy(r => r.Role == "STAFF_LEADER" ? 0 : r.Role == "STAFF" ? 1 : 2)
            .ThenByDescending(r => r.VisitCount)
            .ThenBy(r => r.FullName)
            .ToList();

        var allPersonnelRatings = personnelRows.SelectMany(r =>
            r.FeedbackAverage != null ? Enumerable.Repeat(r.FeedbackAverage.Value, r.FeedbackCount) : Enumerable.Empty<double>())
            .ToList();
        var personnel = new StaffLeaderV2Personnel
        {
            TotalStaff = personnelRows.Count(r => r.Role != "STUDENT"),
            TotalStudents = personnelRows.Count(r => r.Role == "STUDENT"),
            AverageFeedback = allPersonnelRatings.Count > 0 ? Math.Round(allPersonnelRatings.Average(), 1) : null,
            Rows = personnelRows,
        };

        // ═══ Phần 3: các phòng ban khác (không tính IC) ═════════════════════
        var departments = await _db.Departments.AsNoTracking()
            .Where(d => d.CampusId == campusId && d.DepartmentType != "IC" && d.Status == "ACTIVE")
            .Select(d => new { d.DepartmentId, d.Name })
            .ToListAsync(cancellationToken);

        var logisticsAgg = await (
                from li in _db.VisitLogisticsItems.AsNoTracking()
                join ci in instances on li.VisitInstanceId equals ci.VisitInstanceId
                where li.RequestedToDepartmentId != null
                group li by li.RequestedToDepartmentId!.Value into g
                select new
                {
                    DeptId = g.Key,
                    Total = g.Count(),
                    Done = g.Count(li => li.Status == LogisticsItemStatus.Done),
                    Rejected = g.Count(li => li.Status == LogisticsItemStatus.Rejected || li.Status == LogisticsItemStatus.Declined),
                })
            .ToListAsync(cancellationToken);

        // Thư mời tham gia gửi cho nhân sự phòng ban — đếm theo đoàn (visit instance).
        var deptParticipantRows = await (
                from p in _db.VisitParticipants.AsNoTracking()
                join ci in instances on p.VisitInstanceId equals ci.VisitInstanceId
                join u in _db.Users.AsNoTracking() on p.UserId equals u.UserId
                where u.DepartmentId != null && u.Role.RoleCode == "DEPARTMENT" && p.Status != "REMOVED"
                select new { DeptId = u.DepartmentId!.Value, ci.VisitInstanceId, p.Status })
            .ToListAsync(cancellationToken);

        // Feedback host cho phòng ban: HOST_LOGISTICS (đơn) + HOST_PARTICIPANT tới người của phòng ban (thư).
        var hostLogisticsFb = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "HOST_LOGISTICS" && f.VisitInstanceId != null
                join ci in instances on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                select new { f.TargetDepartmentId, f.TargetLogisticsItemId, Rating = (int)f.Rating })
            .ToListAsync(cancellationToken);
        var fbItemIds = hostLogisticsFb.Where(x => x.TargetDepartmentId == null && x.TargetLogisticsItemId != null)
            .Select(x => x.TargetLogisticsItemId!.Value).Distinct().ToList();
        var itemDeptMap = fbItemIds.Count == 0
            ? new Dictionary<ulong, ulong?>()
            : await _db.VisitLogisticsItems.AsNoTracking()
                .Where(li => fbItemIds.Contains(li.LogisticsItemId))
                .Select(li => new { li.LogisticsItemId, li.RequestedToDepartmentId })
                .ToDictionaryAsync(x => x.LogisticsItemId, x => x.RequestedToDepartmentId, cancellationToken);

        var deptPartFb = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "HOST_PARTICIPANT" && f.TargetUserId != null && f.VisitInstanceId != null
                join ci in instances on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                join u in _db.Users.AsNoTracking() on f.TargetUserId equals (ulong?)u.UserId
                where u.DepartmentId != null && u.Role.RoleCode == "DEPARTMENT"
                select new { DeptId = u.DepartmentId!.Value, Rating = (int)f.Rating })
            .ToListAsync(cancellationToken);

        List<int> DeptRatings(ulong deptId)
        {
            var fromLogistics = hostLogisticsFb
                .Where(x => x.TargetDepartmentId == deptId
                            || (x.TargetDepartmentId == null && x.TargetLogisticsItemId != null
                                && itemDeptMap.TryGetValue(x.TargetLogisticsItemId.Value, out var d) && d == deptId))
                .Select(x => x.Rating);
            var fromParticipants = deptPartFb.Where(x => x.DeptId == deptId).Select(x => x.Rating);
            return fromLogistics.Concat(fromParticipants).ToList();
        }

        var deptRows = new List<StaffLeaderV2DepartmentRow>();
        foreach (var d in departments)
        {
            var log = logisticsAgg.FirstOrDefault(x => x.DeptId == d.DepartmentId);
            var invitationGroups = deptParticipantRows
                .Where(p => p.DeptId == d.DepartmentId)
                .GroupBy(p => p.VisitInstanceId)
                .ToList();
            var invitationTotal = invitationGroups.Count;
            var invitationAccepted = invitationGroups.Count(g => g.Any(p => p.Status == "ACCEPTED"));
            var invitationDeclined = invitationGroups.Count(g => g.All(p => p.Status != "ACCEPTED") && g.Any(p => p.Status == "DECLINED"));
            var ratings = DeptRatings(d.DepartmentId);
            deptRows.Add(new StaffLeaderV2DepartmentRow
            {
                DepartmentId = d.DepartmentId,
                Name = d.Name,
                TotalRequests = (log?.Total ?? 0) + invitationTotal,
                Completed = (log?.Done ?? 0) + invitationAccepted,
                Rejected = (log?.Rejected ?? 0) + invitationDeclined,
                FeedbackAverage = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : null,
                FeedbackCount = ratings.Count,
            });
        }
        deptRows = deptRows.OrderByDescending(r => r.TotalRequests).ThenBy(r => r.Name).ToList();

        var allDeptRatings = deptRows.SelectMany(r =>
            r.FeedbackAverage != null ? Enumerable.Repeat(r.FeedbackAverage.Value, r.FeedbackCount) : Enumerable.Empty<double>())
            .ToList();
        var deptSection = new StaffLeaderV2Departments
        {
            TotalDepartments = deptRows.Count,
            CompletedTotal = deptRows.Sum(r => r.Completed),
            RejectedTotal = deptRows.Sum(r => r.Rejected),
            AverageFeedback = allDeptRatings.Count > 0 ? Math.Round(allDeptRatings.Average(), 1) : null,
            Rows = deptRows,
        };

        var campusName = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == campusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"Campus #{campusId}";

        // ═══ Phần 4: Thống kê chi phí ═══════════════════════════════════════
        var expenseReports = await (
                from r in _db.VisitExpenseReports.AsNoTracking()
                join ci in instances on r.VisitInstanceId equals ci.VisitInstanceId
                where r.Status != "CANCELLED"
                join item in _db.VisitExpenseItems.AsNoTracking() on r.ExpenseReportId equals item.ExpenseReportId
                group new { r, item } by new { r.VisitInstanceId, r.ReportScope, r.Status } into g
                select new
                {
                    VisitInstanceId = g.Key.VisitInstanceId,
                    ReportScope = g.Key.ReportScope,
                    Status = g.Key.Status,
                    Total = g.Sum(x => x.item.Quantity * x.item.UnitPrice)
                }
            ).ToListAsync(cancellationToken);

        var expenseInstanceIds = expenseReports.Select(e => e.VisitInstanceId).Distinct().ToList();
        
        var expenseInstances = await instances
            .Where(ci => expenseInstanceIds.Contains(ci.VisitInstanceId))
            .Select(ci => new { ci.VisitInstanceId, GroupCode = ci.VisitRequest.RequestCode, ci.VisitRequest.DelegationName, ci.PlannedStartAt })
            .ToListAsync(cancellationToken);

        var expenseRows = new List<StaffLeaderV2ExpenseRow>();
        foreach (var exInst in expenseInstances)
        {
            var instReports = expenseReports.Where(r => r.VisitInstanceId == exInst.VisitInstanceId).ToList();
            var generalAmount = instReports.Where(r => r.ReportScope == "GENERAL").Sum(r => r.Total);
            var logisticsAmount = instReports.Where(r => r.ReportScope == "LOGISTICS").Sum(r => r.Total);
            var totalAmount = generalAmount + logisticsAmount;

            var statusStr = "CHƯA GHI NHẬN";
            if (instReports.Any(r => r.Status == "FINALIZED")) statusStr = "ĐÃ CHỐT";
            else if (instReports.Any(r => r.Status == "SAVED")) statusStr = "ĐÃ LƯU";
            else if (instReports.Any(r => r.Status == "DRAFT")) statusStr = "BẢN NHÁP";

            expenseRows.Add(new StaffLeaderV2ExpenseRow
            {
                VisitInstanceId = exInst.VisitInstanceId,
                GroupCode = exInst.GroupCode,
                DelegationName = exInst.DelegationName,
                VisitDate = exInst.PlannedStartAt,
                GeneralExpense = generalAmount,
                LogisticsExpense = logisticsAmount,
                TotalExpense = totalAmount,
                Status = statusStr
            });
        }
        
        expenseRows = expenseRows.OrderByDescending(r => r.VisitDate).ToList();

        var expensesSection = new StaffLeaderV2Expenses
        {
            TotalAmount = expenseRows.Sum(r => r.TotalExpense),
            TotalGeneral = expenseRows.Sum(r => r.GeneralExpense),
            TotalLogistics = expenseRows.Sum(r => r.LogisticsExpense),
            Rows = expenseRows
        };

        return new StaffLeaderReportV2Dto
        {
            GeneratedAt = nowVn,
            CampusName = campusName,
            Preset = preset,
            FromDate = fromVn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ToDate = toVnExclusive.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Visits = visits,
            Personnel = personnel,
            Departments = deptSection,
            Expenses = expensesSection
        };
    }
}
