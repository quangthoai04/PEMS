using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
using PEMS.Application.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Shared;

namespace PEMS.Application.Reports.Queries.GetDeptLeaderReportV2;

/// <summary>
/// Tổng hợp báo cáo phòng ban 2 phần (nhiệm vụ + nhân sự) cho Department Leader
/// từ dữ liệu thật. "Nhiệm vụ" = thư mời tham gia (visit_participants, người của phòng
/// ban) + đơn yêu cầu hậu cần (visit_logistics_items, requested_to_department_id).
/// </summary>
public sealed class GetDeptLeaderReportV2QueryHandler
    : IRequestHandler<GetDeptLeaderReportV2Query, DeptLeaderReportV2Dto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDeptLeaderReportV2QueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DeptLeaderReportV2Dto> Handle(GetDeptLeaderReportV2Query request, CancellationToken cancellationToken)
    {
        var (deptId, userId, isLeader) = DeptLeaderReportV2Guard.RequireDepartmentMember(_currentUser);
        var nowVn = VietnamTime.Now();
        var preset = DeptLeaderReportV2Guard.NormalizePreset(request.Preset);
        var (fromVn, toVnExclusive) = DeptLeaderReportV2Guard.ResolvePeriodVn(preset, request.FromDate, request.ToDate, nowVn);
        var granularity = DeptLeaderReportV2Guard.ResolveGranularity(fromVn, toVnExclusive);
        var buckets = DeptLeaderReportV2Guard.BuildBuckets(fromVn, toVnExclusive, granularity);

        // ═══ Nguồn dữ liệu chung: thư mời (INVITATION) + đơn hậu cần (REQUEST) ═════
        // Thư mời: chỉ tính bản ghi ĐÃ ỦY QUYỀN cho staff (assigned_by != null) — bản ghi
        // "leader" gốc trước khi giao việc không phải 1 nhiệm vụ cho nhân sự.
        var invitationRows = await (
                from p in _db.VisitParticipants.AsNoTracking()
                join ci in _db.VisitRequestCampuses.AsNoTracking() on p.VisitInstanceId equals ci.VisitInstanceId
                join u in _db.Users.AsNoTracking() on p.UserId equals u.UserId
                where u.DepartmentId == deptId && u.Role.RoleCode == "DEPARTMENT"
                      && p.AssignedBy != null && p.Status != "REMOVED"
                      && (isLeader || p.UserId == userId)
                      && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive
                select new
                {
                    Kind = "INVITATION",
                    UserId = p.UserId,
                    p.Status,
                    ci.VisitInstanceId,
                    ci.PlannedStartAt,
                    ci.PlannedEndAt,
                    ci.VisitRequest.RequestCode,
                    // Instance row: mixed v2 shows THIS instance's detail name.
                    DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
                })
            .ToListAsync(cancellationToken);

        var logisticsRows = await (
                from li in _db.VisitLogisticsItems.AsNoTracking()
                join ci in _db.VisitRequestCampuses.AsNoTracking() on li.VisitInstanceId equals ci.VisitInstanceId
                let startAt = li.UsageStartAt ?? ci.PlannedStartAt
                let endAt = li.UsageEndAt ?? ci.PlannedEndAt
                where li.RequestedToDepartmentId == deptId
                      && (isLeader || li.AssignedToUserId == userId)
                      && startAt >= fromVn && startAt < toVnExclusive
                select new
                {
                    Kind = "REQUEST",
                    UserId = li.AssignedToUserId,
                    li.Status,
                    li.LogisticsItemId,
                    ci.VisitInstanceId,
                    PlannedStartAt = startAt,
                    PlannedEndAt = endAt,
                    ci.VisitRequest.RequestCode,
                    // Instance row: mixed v2 shows THIS instance's detail name.
                    DelegationName = ci.FormDetail != null ? ci.FormDetail.DelegationName : null,
                })
            .ToListAsync(cancellationToken);

        // ═══ Phần 2: báo cáo nhiệm vụ ════════════════════════════════════════
        static bool IsCompleted(string kind, string status) =>
            kind == "INVITATION" ? (status == "ACCEPTED") : status == LogisticsItemStatus.Done;
        static bool IsRejected(string kind, string status) =>
            kind == "INVITATION" ? status == "DECLINED" : (status == LogisticsItemStatus.Rejected || status == LogisticsItemStatus.Declined);

        var totalTasks = invitationRows.Count + logisticsRows.Count;
        var completed = invitationRows.Count(r => IsCompleted("INVITATION", r.Status)) + logisticsRows.Count(r => IsCompleted("REQUEST", r.Status));
        var rejected = invitationRows.Count(r => IsRejected("INVITATION", r.Status)) + logisticsRows.Count(r => IsRejected("REQUEST", r.Status));

        // Feedback: host đánh giá người tham gia (HOST_PARTICIPANT, target là nhân sự phòng ban)
        // + host đánh giá hạng mục hậu cần (HOST_LOGISTICS, target_department hoặc target_logistics_item).
        var deptUserIds = isLeader
            ? await _db.Users.AsNoTracking()
                .Where(u => u.DepartmentId == deptId && u.Role.RoleCode == "DEPARTMENT")
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken)
            : new List<ulong> { userId };

        var participantFbRows = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "HOST_PARTICIPANT" && f.TargetUserId != null && f.VisitInstanceId != null
                join ci in _db.VisitRequestCampuses.AsNoTracking() on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                where deptUserIds.Contains(f.TargetUserId!.Value)
                      && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive
                select new { UserId = f.TargetUserId!.Value, Rating = (int)f.Rating })
            .ToListAsync(cancellationToken);

        var logisticsFbRows = await (
                from f in _db.Feedbacks.AsNoTracking()
                where f.FeedbackType == "HOST_LOGISTICS" && f.VisitInstanceId != null
                join ci in _db.VisitRequestCampuses.AsNoTracking() on f.VisitInstanceId equals (ulong?)ci.VisitInstanceId
                where ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive
                select new { f.TargetDepartmentId, f.TargetLogisticsItemId, Rating = (int)f.Rating })
            .ToListAsync(cancellationToken);
        var fbItemIds = logisticsFbRows.Where(x => x.TargetDepartmentId == null && x.TargetLogisticsItemId != null)
            .Select(x => x.TargetLogisticsItemId!.Value).Distinct().ToList();
        var itemAssigneeMap = fbItemIds.Count == 0
            ? new Dictionary<ulong, ulong?>()
            : await _db.VisitLogisticsItems.AsNoTracking()
                .Where(li => fbItemIds.Contains(li.LogisticsItemId))
                .Select(li => new { li.LogisticsItemId, li.AssignedToUserId })
                .ToDictionaryAsync(x => x.LogisticsItemId, x => x.AssignedToUserId, cancellationToken);
        var deptLogisticsFbRatings = isLeader
            ? logisticsFbRows
                .Where(x => x.TargetDepartmentId == deptId
                            || (x.TargetDepartmentId == null && x.TargetLogisticsItemId != null))
                .Select(x => x.Rating)
                .ToList()
            : logisticsFbRows
                .Where(x => x.TargetLogisticsItemId != null
                            && itemAssigneeMap.TryGetValue(x.TargetLogisticsItemId.Value, out var assignee)
                            && assignee == userId)
                .Select(x => x.Rating)
                .ToList();

        var allTaskFbRatings = participantFbRows.Select(x => x.Rating)
            .Concat(deptLogisticsFbRatings)
            .ToList();

        var trend = buckets.Select(b => new DeptLeaderV2TrendPoint
        {
            Month = b.Start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            MonthLabel = b.Label,
            TotalTasks = invitationRows.Count(r => r.PlannedStartAt >= b.Start && r.PlannedStartAt < b.End)
                         + logisticsRows.Count(r => r.PlannedStartAt >= b.Start && r.PlannedStartAt < b.End),
            Completed = invitationRows.Count(r => r.PlannedStartAt >= b.Start && r.PlannedStartAt < b.End && IsCompleted("INVITATION", r.Status))
                        + logisticsRows.Count(r => r.PlannedStartAt >= b.Start && r.PlannedStartAt < b.End && IsCompleted("REQUEST", r.Status)),
        }).ToList();

        var tasks = new DeptLeaderV2Tasks
        {
            TotalTasks = totalTasks,
            Completed = completed,
            Rejected = rejected,
            NotCompleted = Math.Max(0, totalTasks - completed - rejected),
            FeedbackCount = allTaskFbRatings.Count,
            FeedbackTotalStars = allTaskFbRatings.Sum(),
            FeedbackAverage = allTaskFbRatings.Count > 0 ? Math.Round(allTaskFbRatings.Average(), 1) : null,
            TrendGranularity = granularity,
            Trend = trend,
        };

        // ═══ Phần 3: nhân sự (Dept Leader + Dept Staff) ═════════════════════
        var personnelUsers = await _db.Users.AsNoTracking()
            .Where(u => u.DepartmentId == deptId && u.Role.RoleCode == "DEPARTMENT" && u.Status == "ACTIVE")
            .Select(u => new { u.UserId, u.FullName, u.Email, u.SubRole })
            .ToListAsync(cancellationToken);

        static bool CountsForHours(string kind, string status) =>
            kind == "INVITATION" ? (status != "DECLINED" && status != "REMOVED")
                : (status != LogisticsItemStatus.Cancelled && status != LogisticsItemStatus.Rejected && status != LogisticsItemStatus.Declined);

        var declinedByUser = await (
                from p in _db.VisitParticipants.AsNoTracking()
                join ci in _db.VisitRequestCampuses.AsNoTracking() on p.VisitInstanceId equals ci.VisitInstanceId
                where p.Status == "DECLINED" && p.AssignedBy != null
                      && ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive
                select p.UserId)
            .ToListAsync(cancellationToken);
        var declinedLogisticsByUser = logisticsRows
            .Where(r => r.UserId != null && (r.Status == LogisticsItemStatus.Rejected || r.Status == LogisticsItemStatus.Declined))
            .Select(r => r.UserId!.Value)
            .ToList();

        var personnelRows = new List<DeptLeaderV2PersonnelRow>();
        foreach (var u in personnelUsers)
        {
            var myInvitations = invitationRows.Where(r => r.UserId == u.UserId).ToList();
            var myLogistics = logisticsRows.Where(r => r.UserId == u.UserId).ToList();
            var taskCount = myInvitations.Count(r => IsCompleted("INVITATION", r.Status) || r.Status == "ASSIGNED" || r.Status == "ACCEPTED")
                            + myLogistics.Count(r => r.Status != LogisticsItemStatus.Rejected && r.Status != LogisticsItemStatus.Declined);
            var totalHours = myInvitations.Where(r => CountsForHours("INVITATION", r.Status)).Sum(r => Math.Max(0, (r.PlannedEndAt - r.PlannedStartAt).TotalHours))
                              + myLogistics.Where(r => CountsForHours("REQUEST", r.Status)).Sum(r => Math.Max(0, (r.PlannedEndAt - r.PlannedStartAt).TotalHours));
            var myRatings = participantFbRows.Where(f => f.UserId == u.UserId).Select(f => f.Rating)
                .Concat(logisticsFbRows
                    .Where(x => x.TargetLogisticsItemId != null
                                && itemAssigneeMap.TryGetValue(x.TargetLogisticsItemId.Value, out var assignee)
                                && assignee == u.UserId)
                    .Select(x => x.Rating))
                .ToList();
            var declinedCount = declinedByUser.Count(id => id == u.UserId) + declinedLogisticsByUser.Count(id => id == u.UserId);

            personnelRows.Add(new DeptLeaderV2PersonnelRow
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Role = string.Equals(u.SubRole, "LEADER", StringComparison.OrdinalIgnoreCase) ? "DEPT_LEADER" : "DEPT_STAFF",
                TaskCount = taskCount,
                TotalHours = Math.Round(totalHours, 1),
                FeedbackAverage = myRatings.Count > 0 ? Math.Round(myRatings.Average(), 1) : null,
                FeedbackCount = myRatings.Count,
                DeclinedCount = declinedCount,
            });
        }
        personnelRows = personnelRows
            .OrderBy(r => r.Role == "DEPT_LEADER" ? 0 : 1)
            .ThenByDescending(r => r.TaskCount)
            .ThenBy(r => r.FullName)
            .ToList();

        var allPersonnelRatings = personnelRows.SelectMany(r =>
            r.FeedbackAverage != null ? Enumerable.Repeat(r.FeedbackAverage.Value, r.FeedbackCount) : Enumerable.Empty<double>())
            .ToList();
        var personnel = new DeptLeaderV2Personnel
        {
            TotalStaff = personnelRows.Count,
            AverageFeedback = allPersonnelRatings.Count > 0 ? Math.Round(allPersonnelRatings.Average(), 1) : null,
            Rows = personnelRows,
        };

        var deptName = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == deptId)
            .Select(d => d.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"Phòng ban #{deptId}";

        // ═══ Phần 4: Thống kê chi phí ═══════════════════════════════════════
        var expenseReports = await (
                from r in _db.VisitExpenseReports.AsNoTracking()
                where r.ReportScope == "LOGISTICS" && r.DepartmentId == deptId && r.Status != "CANCELLED"
                join ci in _db.VisitRequestCampuses.AsNoTracking() on r.VisitInstanceId equals ci.VisitInstanceId
                where ci.PlannedStartAt >= fromVn && ci.PlannedStartAt < toVnExclusive
                join item in _db.VisitExpenseItems.AsNoTracking() on r.ExpenseReportId equals item.ExpenseReportId
                join li in _db.VisitLogisticsItems.AsNoTracking() on r.LogisticsItemId equals (ulong?)li.LogisticsItemId
                group new { r, item, ci, li } by new { r.LogisticsItemId, GroupCode = ci.VisitRequest.RequestCode, li.Title, ci.PlannedStartAt, r.Status } into g
                select new
                {
                    LogisticsItemId = g.Key.LogisticsItemId!.Value,
                    GroupCode = g.Key.GroupCode,
                    ItemName = g.Key.Title,
                    VisitDate = g.Key.PlannedStartAt,
                    Status = g.Key.Status,
                    Total = g.Sum(x => x.item.Quantity * x.item.UnitPrice)
                }
            ).ToListAsync(cancellationToken);

        var expenseRows = expenseReports.Select(e => new DeptLeaderV2ExpenseRow
        {
            LogisticsItemId = e.LogisticsItemId,
            GroupCode = e.GroupCode,
            ItemName = e.ItemName,
            VisitDate = e.VisitDate,
            TotalExpense = e.Total,
            Status = e.Status == "FINALIZED" ? "ĐÃ CHỐT" : e.Status == "SAVED" ? "ĐÃ LƯU" : "BẢN NHÁP"
        }).OrderByDescending(r => r.VisitDate).ToList();

        var expensesSection = new DeptLeaderV2Expenses
        {
            TotalAmount = expenseRows.Sum(r => r.TotalExpense),
            Rows = expenseRows
        };

        return new DeptLeaderReportV2Dto
        {
            GeneratedAt = nowVn,
            DepartmentName = deptName,
            Preset = preset,
            FromDate = fromVn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ToDate = toVnExclusive.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Tasks = tasks,
            Personnel = personnel,
            Expenses = expensesSection
        };
    }
}
