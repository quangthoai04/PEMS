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
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

using PEMS.Application.Common;
namespace PEMS.Application.Reports.Queries.GetDeptLeaderReportOverview;

/// <summary>
/// Aggregates the Department Leader operation report from live data (no mock).
/// Scope is always the leader's own department (visit_logistics_items.requested_to_department_id);
/// every query is AsNoTracking and aggregation happens in the database wherever EF can translate it.
/// Period sections (pipeline, trend, staff, handover, incidents, feedback) use the selected period;
/// action queues (KPI workflow counts, attention, pending tasks, proposals) use the current state,
/// mirroring the Staff Leader report convention.
/// The controller enforces the role, this handler re-checks as defense in depth.
/// </summary>
public sealed class GetDeptLeaderReportOverviewQueryHandler
    : IRequestHandler<GetDeptLeaderReportOverviewQuery, DeptLeaderReportOverviewDto>
{
    private const int VnUtcOffsetHours = 7;
    private const int PreviewLimit = 10;

    private static readonly string[] OpenStatuses =
    {
        LogisticsItemStatus.Requested,
        LogisticsItemStatus.ChangeProposed,
        LogisticsItemStatus.Assigned,
        LogisticsItemStatus.Accepted,
        LogisticsItemStatus.InProgress,
    };

    private static readonly (string Status, string LabelVi)[] PipelineStatuses =
    {
        (LogisticsItemStatus.Requested, "Yêu cầu mới"),
        (LogisticsItemStatus.Assigned, "Chờ phản hồi"),
        (LogisticsItemStatus.Accepted, "Đã nhận"),
        (LogisticsItemStatus.InProgress, "Đang xử lý"),
        (LogisticsItemStatus.ChangeProposed, "Đề xuất thay đổi"),
        (LogisticsItemStatus.Done, "Hoàn thành"),
        (LogisticsItemStatus.Rejected, "PB từ chối"),
        (LogisticsItemStatus.Declined, "NS từ chối"),
        (LogisticsItemStatus.Cancelled, "Đã hủy"),
    };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDeptLeaderReportOverviewQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DeptLeaderReportOverviewDto> Handle(GetDeptLeaderReportOverviewQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            throw new ForbiddenException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
        if (!string.Equals(_currentUser.RoleCode, "DEPARTMENT", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(_currentUser.SubRole, "LEADER", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Bạn không có quyền xem báo cáo Department Leader.");

        var deptId = _currentUser.DepartmentId
            ?? throw new ForbiddenException("Tài khoản chưa được gán phòng ban.");

        var nowVn = VietnamTime.Now();

        var pending24hUtc = nowVn.AddHours(-24);

        var preset = NormalizePreset(request.Preset);
        var (fromVn, toVnExclusive) = ResolvePeriodVn(preset, request.FromDate, request.ToDate, nowVn);


        var logisticsStatus = NormalizeFilter(request.LogisticsStatus);
        var itemType = NormalizeFilter(request.ItemType);
        var priority = NormalizeFilter(request.Priority);
        var dueStatus = NormalizeFilter(request.DueStatus);
        var handoverStatus = NormalizeFilter(request.HandoverStatus);
        var feedbackRating = NormalizeFilter(request.FeedbackRating);
        ulong? assignedUserId = ulong.TryParse(request.AssignedUserId, out var parsedUser) && parsedUser > 0 ? parsedUser : null;

        // Non-time filters shared by both bases (period aggregates + current action queues).
        IQueryable<VisitLogisticsItem> ApplyItemFilters(IQueryable<VisitLogisticsItem> q)
        {
            if (logisticsStatus != null) q = q.Where(li => li.Status == logisticsStatus);
            if (itemType != null) q = q.Where(li => li.ItemType == itemType);
            if (priority != null) q = q.Where(li => li.Priority == priority);
            if (assignedUserId != null) q = q.Where(li => li.AssignedToUserId == assignedUserId);
            if (dueStatus == "OVERDUE")
                q = q.Where(li => (li.DueAt ?? li.VisitInstance.PlannedEndAt) < nowVn && OpenStatuses.Contains(li.Status));
            else if (dueStatus == "DUE_SOON")
                q = q.Where(li => (li.DueAt ?? li.VisitInstance.PlannedEndAt) >= nowVn
                                  && (li.DueAt ?? li.VisitInstance.PlannedEndAt) < nowVn.AddHours(72)
                                  && OpenStatuses.Contains(li.Status));
            if (handoverStatus == "COMPLETE")
                q = q.Where(li => li.Handovers.Any() && li.Handovers.All(h => h.BorrowerSignedAt != null && h.ProviderSignedAt != null));
            else if (handoverStatus == "MISSING_SIGNATURE")
                q = q.Where(li => li.Handovers.Any(h => h.BorrowerSignedAt == null || h.ProviderSignedAt == null));
            else if (handoverStatus == "DAMAGED")
                q = q.Where(li => li.Handovers.Any(h => h.ItemCondition == "DAMAGED"));
            else if (handoverStatus == "MISSING")
                q = q.Where(li => li.Handovers.Any(h => h.ItemCondition == "MISSING"));
            return q;
        }

        // ---- Base query 1: items requested to this department, visit planned in the period. ----
        var periodItems = ApplyItemFilters(_db.VisitLogisticsItems.AsNoTracking()
            .Where(li => li.RequestedToDepartmentId == deptId
                         && li.VisitInstance.PlannedStartAt >= fromVn
                         && li.VisitInstance.PlannedStartAt < toVnExclusive));

        // ---- Base query 2: current operational state (ignores period on purpose — action queues). ----
        var currentItems = ApplyItemFilters(_db.VisitLogisticsItems.AsNoTracking()
            .Where(li => li.RequestedToDepartmentId == deptId));

        // ---- Current-state workflow counts (KPI strip + attention). ----
        var curStatusCounts = await currentItems
            .GroupBy(li => li.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        int CurCount(string status) => curStatusCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var waitingAssignment = await currentItems.CountAsync(
            li => li.Status == LogisticsItemStatus.Requested && li.AssignedToUserId == null, cancellationToken);
        var overdueCurrent = await currentItems.CountAsync(
            li => (li.DueAt ?? li.VisitInstance.PlannedEndAt) < nowVn && OpenStatuses.Contains(li.Status), cancellationToken);
        var pendingResponseOver24h = await currentItems.CountAsync(
            li => li.Status == LogisticsItemStatus.Assigned && li.AssignedAt != null && li.AssignedAt < pending24hUtc, cancellationToken);
        var missingSignatureCurrent = await currentItems
            .SelectMany(li => li.Handovers)
            .CountAsync(h => h.BorrowerSignedAt == null || h.ProviderSignedAt == null, cancellationToken);
        var damagedOrMissingCurrent = await currentItems
            .SelectMany(li => li.Handovers)
            .CountAsync(h => h.ItemCondition == "DAMAGED" || h.ItemCondition == "MISSING", cancellationToken);

        // ---- Period aggregates: task pipeline + work type distribution. ----
        var periodStatusCounts = await periodItems
            .GroupBy(li => li.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var totalPeriodItems = periodStatusCounts.Sum(x => x.Count);
        int PeriodCount(string status) => periodStatusCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var taskStatusPipeline = PipelineStatuses
            .Select(p => new DeptLeaderTaskPipelineItem
            {
                Status = p.Status,
                LabelVi = p.LabelVi,
                Count = PeriodCount(p.Status),
                Percentage = totalPeriodItems > 0
                    ? Math.Round(PeriodCount(p.Status) * 100.0 / totalPeriodItems, 1)
                    : 0,
            })
            .ToList();

        var workTypeRaw = await periodItems
            .GroupBy(li => li.ItemType)
            .Select(g => new { ItemType = g.Key, Count = g.Count(), QuantityTotal = g.Sum(li => li.Quantity ?? 0) })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);
        var workTypeDistribution = workTypeRaw
            .Select(x => new DeptLeaderWorkTypeItem
            {
                ItemType = x.ItemType,
                LabelVi = DeptLeaderReportLabels.ItemTypeLabelVi(x.ItemType),
                Count = x.Count,
                QuantityTotal = x.QuantityTotal,
                Percentage = totalPeriodItems > 0 ? Math.Round(x.Count * 100.0 / totalPeriodItems, 1) : 0,
            })
            .ToList();

        // ---- Monthly trend (grouped by Vietnam-local month of the visit's planned start). ----
        var trendRaw = await periodItems
            .GroupBy(li => new
            {
                li.VisitInstance.PlannedStartAt.Year,
                li.VisitInstance.PlannedStartAt.Month,
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Count(),
                Completed = g.Count(li => li.Status == LogisticsItemStatus.Done),
                Overdue = g.Count(li => (li.DueAt ?? li.VisitInstance.PlannedEndAt) < nowVn && OpenStatuses.Contains(li.Status)),
            })
            .ToListAsync(cancellationToken);

        var monthlyTrend = new List<DeptLeaderMonthlyTrend>();
        var lastMonthVn = toVnExclusive.AddDays(-1);
        for (var cursor = new DateTime(fromVn.Year, fromVn.Month, 1);
             cursor <= new DateTime(lastMonthVn.Year, lastMonthVn.Month, 1);
             cursor = cursor.AddMonths(1))
        {
            var row = trendRaw.FirstOrDefault(t => t.Year == cursor.Year && t.Month == cursor.Month);
            monthlyTrend.Add(new DeptLeaderMonthlyTrend
            {
                Month = cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                MonthLabel = $"T{cursor.Month}/{cursor.Year}",
                TotalTasks = row?.Total ?? 0,
                CompletedTasks = row?.Completed ?? 0,
                OverdueTasks = row?.Overdue ?? 0,
            });
        }

        // ---- Assignment attempts in the period (staff performance + response time). ----
        // Minimal projection to memory: a department's attempts within a period stay small.
        var attemptRows = await _db.VisitLogisticsAssignmentAttempts.AsNoTracking()
            .Where(a => a.LogisticsItem.RequestedToDepartmentId == deptId
                        && a.LogisticsItem.VisitInstance.PlannedStartAt >= fromVn
                        && a.LogisticsItem.VisitInstance.PlannedStartAt < toVnExclusive)
            .Select(a => new { a.AssigneeUserId, a.Status, a.AssignedAt, a.RespondedAt })
            .ToListAsync(cancellationToken);

        var respondedHours = attemptRows
            .Where(a => a.RespondedAt != null)
            .Select(a => Math.Max(0, (a.RespondedAt!.Value - a.AssignedAt).TotalHours))
            .ToList();
        double? averageResponseHours = respondedHours.Count > 0 ? Math.Round(respondedHours.Average(), 1) : null;

        var itemsByAssignee = await periodItems
            .Where(li => li.AssignedToUserId != null)
            .GroupBy(li => li.AssignedToUserId!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                Total = g.Count(),
                InProgress = g.Count(li => li.Status == LogisticsItemStatus.InProgress),
                Completed = g.Count(li => li.Status == LogisticsItemStatus.Done),
                Overdue = g.Count(li => (li.DueAt ?? li.VisitInstance.PlannedEndAt) < nowVn && OpenStatuses.Contains(li.Status)),
            })
            .ToListAsync(cancellationToken);

        var staffIds = attemptRows.Select(a => a.AssigneeUserId)
            .Concat(itemsByAssignee.Select(x => x.UserId))
            .Distinct()
            .ToList();

        // ---- Pending tasks (current state, oldest wait first). ----
        var pendingTasksQuery = currentItems.Where(li => OpenStatuses.Contains(li.Status));
        var pendingTasksTotal = await pendingTasksQuery.CountAsync(cancellationToken);
        var pendingRows = await pendingTasksQuery
            .OrderBy(li => li.DueAt ?? li.VisitInstance.PlannedEndAt)
            .Take(PreviewLimit)
            .Select(li => new
            {
                li.LogisticsItemId,
                li.VisitInstanceId,
                li.VisitInstance.VisitRequest.RequestCode,
                // Instance row: mixed v2 shows THIS instance's detail name.
                DelegationName = li.VisitInstance.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus
                                 && li.VisitInstance.VisitRequest.HasMixedCampusDetails
                    ? (li.VisitInstance.FormDetail != null ? li.VisitInstance.FormDetail.DelegationName : null)
                    : li.VisitInstance.VisitRequest.DelegationName,
                li.Title,
                li.ItemType,
                li.Quantity,
                li.Priority,
                li.Status,
                DueAt = li.DueAt ?? (DateTime?)li.VisitInstance.PlannedEndAt,
                li.AssignedToUserId,
                WaitingSince = li.AssignedAt ?? li.RequestedAt ?? li.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        // ---- Change proposals waiting for a decision (current state). ----
        var proposalRows = await currentItems
            .Where(li => li.Status == LogisticsItemStatus.ChangeProposed)
            .OrderByDescending(li => li.ProposedAt ?? li.CreatedAt)
            .Take(PreviewLimit)
            .Select(li => new
            {
                li.LogisticsItemId,
                li.Title,
                li.ProposedBy,
                li.ProposedQuantity,
                li.ProposedUsageStartAt,
                li.ProposedUsageEndAt,
                li.ProposalNote,
                CreatedAt = li.ProposedAt ?? li.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        var changeProposalsWaiting = CurCount(LogisticsItemStatus.ChangeProposed);

        // ---- Handovers of period items (detail preview + incident aggregation, client-side shaping). ----
        var handoverRows = await periodItems
            .SelectMany(li => li.Handovers.Select(h => new
            {
                li.LogisticsItemId,
                li.Title,
                li.ItemType,
                li.Quantity,
                li.VisitInstance.VisitRequest.RequestCode,
                // Instance row: mixed v2 shows THIS instance's detail name.
                DelegationName = li.VisitInstance.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus
                                 && li.VisitInstance.VisitRequest.HasMixedCampusDetails
                    ? (li.VisitInstance.FormDetail != null ? li.VisitInstance.FormDetail.DelegationName : null)
                    : li.VisitInstance.VisitRequest.DelegationName,
                h.HandoverType,
                h.BorrowerSignedAt,
                h.ProviderSignedAt,
                h.ItemCondition,
                h.ConditionNote,
                h.AttachmentFileId,
                h.CreatedAt,
            }))
            .ToListAsync(cancellationToken);

        var handoverTotal = handoverRows.Count;
        var handoverSummary = handoverRows
            .OrderBy(h => h.BorrowerSignedAt != null && h.ProviderSignedAt != null) // missing signature first
            .ThenByDescending(h => h.CreatedAt)
            .Take(PreviewLimit)
            .Select(h => new DeptLeaderHandoverItem
            {
                LogisticsItemId = h.LogisticsItemId,
                ItemName = h.Title,
                VisitCode = h.RequestCode,
                DelegationName = h.DelegationName,
                HandoverType = h.HandoverType,
                BorrowerSigned = h.BorrowerSignedAt != null,
                ProviderSigned = h.ProviderSignedAt != null,
                ItemCondition = h.ItemCondition,
                ConditionNote = h.ConditionNote,
                AttachmentFileId = h.AttachmentFileId,
                StatusLabel = HandoverStatusLabel(h.BorrowerSignedAt != null, h.ProviderSignedAt != null, h.ItemCondition),
            })
            .ToList();

        var incidentSummary = handoverRows
            .GroupBy(h => h.ItemType)
            .Select(g =>
            {
                var incidents = g
                    .Where(h => h.ItemCondition == "DAMAGED" || h.ItemCondition == "MISSING"
                                || h.BorrowerSignedAt == null || h.ProviderSignedAt == null)
                    .OrderByDescending(h => h.CreatedAt)
                    .ToList();
                var latest = incidents.FirstOrDefault(h => !string.IsNullOrWhiteSpace(h.ConditionNote))
                             ?? g.OrderByDescending(h => h.CreatedAt).FirstOrDefault(h => !string.IsNullOrWhiteSpace(h.ConditionNote));
                return new DeptLeaderIncidentItem
                {
                    ItemType = g.Key,
                    ItemTypeLabelVi = DeptLeaderReportLabels.ItemTypeLabelVi(g.Key),
                    ItemName = incidents.FirstOrDefault()?.Title ?? g.First().Title,
                    TotalQuantity = g.GroupBy(h => h.LogisticsItemId).Sum(item => item.First().Quantity ?? 0),
                    DamagedCount = g.Count(h => h.ItemCondition == "DAMAGED"),
                    MissingCount = g.Count(h => h.ItemCondition == "MISSING"),
                    NeedActionCount = incidents.Count,
                    LatestNote = latest?.ConditionNote,
                };
            })
            .OrderByDescending(x => x.NeedActionCount)
            .ThenByDescending(x => x.TotalQuantity)
            .ToList();

        // ---- Feedback about this department / its logistics items, submitted in the period. ----
        var feedbackBase = _db.Feedbacks.AsNoTracking().Where(f =>
            f.SubmittedAt >= fromVn && f.SubmittedAt < toVnExclusive
            && (f.TargetDepartmentId == deptId
                || (f.TargetLogisticsItemId != null && _db.VisitLogisticsItems.Any(li =>
                        li.LogisticsItemId == f.TargetLogisticsItemId && li.RequestedToDepartmentId == deptId))
                || (f.TargetHandoverId != null && _db.VisitLogisticsItemHandovers.Any(h =>
                        h.HandoverId == f.TargetHandoverId && h.LogisticsItem.RequestedToDepartmentId == deptId))));
        if (feedbackRating == "LOW") feedbackBase = feedbackBase.Where(f => f.Rating <= 2);
        else if (feedbackRating == "HIGH") feedbackBase = feedbackBase.Where(f => f.Rating >= 4);

        var fbRows = await feedbackBase
            .Select(f => new { f.Rating, f.TargetLogisticsItemId, f.TargetHandoverId })
            .ToListAsync(cancellationToken);

        var fbItemIds = fbRows.Where(f => f.TargetLogisticsItemId != null).Select(f => f.TargetLogisticsItemId!.Value).Distinct().ToList();
        var fbHandoverIds = fbRows.Where(f => f.TargetLogisticsItemId == null && f.TargetHandoverId != null)
            .Select(f => f.TargetHandoverId!.Value).Distinct().ToList();
        var fbItemTypes = await _db.VisitLogisticsItems.AsNoTracking()
            .Where(li => fbItemIds.Contains(li.LogisticsItemId))
            .Select(li => new { li.LogisticsItemId, li.ItemType })
            .ToListAsync(cancellationToken);
        var fbHandoverTypes = await _db.VisitLogisticsItemHandovers.AsNoTracking()
            .Where(h => fbHandoverIds.Contains(h.HandoverId))
            .Select(h => new { h.HandoverId, h.LogisticsItem.ItemType })
            .ToListAsync(cancellationToken);

        string FbItemType(ulong? logisticsItemId, ulong? handoverId) =>
            logisticsItemId != null
                ? fbItemTypes.FirstOrDefault(x => x.LogisticsItemId == logisticsItemId)?.ItemType ?? "OTHER"
                : handoverId != null
                    ? fbHandoverTypes.FirstOrDefault(x => x.HandoverId == handoverId)?.ItemType ?? "OTHER"
                    : "DEPARTMENT";

        var feedbackByItemType = fbRows
            .GroupBy(f => FbItemType(f.TargetLogisticsItemId, f.TargetHandoverId))
            .Select(g => new DeptLeaderFeedbackByType
            {
                ItemType = g.Key,
                LabelVi = DeptLeaderReportLabels.ItemTypeLabelVi(g.Key),
                AverageRating = Math.Round(g.Average(f => (double)f.Rating), 1),
                FeedbackCount = g.Count(),
            })
            .OrderBy(g => g.AverageRating)
            .ToList();

        var lowRatedItems = await FeedbackEntries(
            feedbackBase.Where(f => f.Rating <= 2).OrderBy(f => f.Rating).ThenByDescending(f => f.SubmittedAt),
            cancellationToken);
        var recentFeedbacks = await FeedbackEntries(
            feedbackBase.OrderByDescending(f => f.SubmittedAt),
            cancellationToken);

        var feedbackSummary = new DeptLeaderFeedbackSummary
        {
            AverageRating = fbRows.Count > 0 ? Math.Round(fbRows.Average(f => (double)f.Rating), 1) : null,
            TotalFeedbacks = fbRows.Count,
            LowFeedbackCount = fbRows.Count(f => f.Rating <= 2),
            FeedbackByItemType = feedbackByItemType,
            LowRatedItems = lowRatedItems,
            RecentFeedbacks = recentFeedbacks,
        };

        // ---- Resolve user names in one query. ----
        var userIds = staffIds
            .Concat(pendingRows.Where(r => r.AssignedToUserId != null).Select(r => r.AssignedToUserId!.Value))
            .Concat(proposalRows.Where(r => r.ProposedBy != null).Select(r => r.ProposedBy!.Value))
            .Distinct()
            .ToList();
        if (assignedUserId != null) userIds.Add(assignedUserId.Value);
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FullName })
            .ToListAsync(cancellationToken);
        string? UserName(ulong? id) => id == null ? null
            : userNames.FirstOrDefault(u => u.UserId == id)?.FullName ?? $"User #{id}";

        var staffPerformance = staffIds
            .Select(userId =>
            {
                var attempts = attemptRows.Where(a => a.AssigneeUserId == userId).ToList();
                var items = itemsByAssignee.FirstOrDefault(x => x.UserId == userId);
                var responded = attempts
                    .Where(a => a.RespondedAt != null)
                    .Select(a => Math.Max(0, (a.RespondedAt!.Value - a.AssignedAt).TotalHours))
                    .ToList();
                // Items có thể được gán trực tiếp không qua assignment attempt — lấy max hai nguồn
                // để "Được giao" và tỷ lệ hoàn thành không bị 0 khi vẫn có việc đã gán/hoàn thành.
                var assignedCount = Math.Max(attempts.Count, items?.Total ?? 0);
                var completedCount = items?.Completed ?? 0;
                return new DeptLeaderStaffPerformance
                {
                    UserId = userId,
                    FullName = UserName(userId) ?? $"User #{userId}",
                    AssignedCount = assignedCount,
                    PendingResponseCount = attempts.Count(a => a.Status == "PENDING"),
                    AcceptedCount = attempts.Count(a => a.Status == "ACCEPTED"),
                    InProgressCount = items?.InProgress ?? 0,
                    CompletedCount = completedCount,
                    DeclinedCount = attempts.Count(a => a.Status == "DECLINED"),
                    OverdueCount = items?.Overdue ?? 0,
                    CompletionRate = assignedCount > 0 ? Math.Round(completedCount * 100.0 / assignedCount, 1) : 0,
                    AverageResponseHours = responded.Count > 0 ? Math.Round(responded.Average(), 1) : null,
                };
            })
            .OrderByDescending(s => s.CompletedCount).ThenByDescending(s => s.AssignedCount)
            .ToList();

        var pendingTasks = pendingRows
            .Select(r => new DeptLeaderPendingTask
            {
                LogisticsItemId = r.LogisticsItemId,
                VisitInstanceId = r.VisitInstanceId,
                RequestCode = r.RequestCode,
                DelegationName = r.DelegationName,
                ItemName = r.Title,
                ItemType = r.ItemType,
                Quantity = r.Quantity ?? 0,
                Unit = null, // schema has no unit column; UI renders "—"
                Priority = r.Priority,
                Status = r.Status,
                DueAt = r.DueAt,
                AssignedToName = UserName(r.AssignedToUserId),
                WaitingHours = Math.Max(0, Math.Round((nowVn - r.WaitingSince).TotalHours, 1)),
                ActionLabel = r.Status == LogisticsItemStatus.Requested && r.AssignedToUserId == null
                    ? "Phân công"
                    : r.Status == LogisticsItemStatus.Assigned
                        ? "Nhắc phản hồi"
                        : "Xem chi tiết",
                DetailUrl = null,
            })
            .ToList();

        var proposalChanges = proposalRows
            .Select(r => new DeptLeaderProposalChange
            {
                LogisticsItemId = r.LogisticsItemId,
                ItemName = r.Title,
                ProposedByName = UserName(r.ProposedBy) ?? "—",
                ProposedQuantity = r.ProposedQuantity,
                ProposedUsageStartAt = r.ProposedUsageStartAt,
                ProposedUsageEndAt = r.ProposedUsageEndAt,
                ProposalNote = r.ProposalNote,
                ProposalStatus = "Chờ phản hồi",
                CreatedAt = r.CreatedAt,
            })
            .ToList();

        // ---- Attention items ("cần Department Leader xử lý ngay" — current state unless noted). ----
        var attentionItems = new List<DeptLeaderAttentionItem>
        {
            new()
            {
                Key = "UNASSIGNED_REQUESTS",
                Label = "Yêu cầu chưa phân công",
                Count = waitingAssignment,
                Severity = waitingAssignment > 0 ? "WARNING" : "SUCCESS",
                Description = "Yêu cầu logistics mới chưa gán nhân sự xử lý",
                TargetSection = "TASKS",
            },
            new()
            {
                Key = "PENDING_RESPONSE_24H",
                Label = "Chờ nhân sự phản hồi > 24h",
                Count = pendingResponseOver24h,
                Severity = pendingResponseOver24h > 0 ? "WARNING" : "SUCCESS",
                Description = "Nhiệm vụ đã gán nhưng nhân sự chưa nhận/từ chối quá 24 giờ",
                TargetSection = "TASKS",
            },
            new()
            {
                Key = "OVERDUE_TASKS",
                Label = "Nhiệm vụ quá hạn",
                Count = overdueCurrent,
                Severity = overdueCurrent > 0 ? "DANGER" : "SUCCESS",
                Description = "Nhiệm vụ đã quá deadline nhưng chưa hoàn thành",
                TargetSection = "TASKS",
            },
            new()
            {
                Key = "CHANGE_PROPOSALS",
                Label = "Đề xuất thay đổi chờ xử lý",
                Count = changeProposalsWaiting,
                Severity = changeProposalsWaiting > 0 ? "INFO" : "SUCCESS",
                Description = "Đề xuất thay đổi số lượng/thời gian đang chờ phản hồi",
                TargetSection = "TASKS",
            },
            new()
            {
                Key = "MISSING_SIGNATURE",
                Label = "Thiếu chữ ký bàn giao",
                Count = missingSignatureCurrent,
                Severity = missingSignatureCurrent > 0 ? "WARNING" : "SUCCESS",
                Description = "Biên bản mượn/trả thiếu chữ ký một trong hai bên",
                TargetSection = "HANDOVER",
            },
            new()
            {
                Key = "DAMAGED_OR_MISSING",
                Label = "Hư hỏng / thiếu mất",
                Count = damagedOrMissingCurrent,
                Severity = damagedOrMissingCurrent > 0 ? "DANGER" : "SUCCESS",
                Description = "Bàn giao ghi nhận đồ hư hỏng hoặc thiếu/mất",
                TargetSection = "INCIDENTS",
            },
            new()
            {
                Key = "LOW_FEEDBACK",
                Label = "Feedback thấp (≤ 2 sao)",
                Count = feedbackSummary.LowFeedbackCount,
                Severity = feedbackSummary.LowFeedbackCount > 0 ? "DANGER" : "SUCCESS",
                Description = "Feedback thấp về phòng ban/hậu cần trong kỳ",
                TargetSection = "INCIDENTS",
            },
        };

        // ---- Header/filter metadata. ----
        var deptInfo = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == deptId)
            .Select(d => new { d.Name, CampusName = d.Campus.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var generatedByName = _currentUser.UserId != null
            ? await _db.Users.AsNoTracking()
                .Where(u => u.UserId == _currentUser.UserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new DeptLeaderReportOverviewDto
        {
            GeneratedAt = nowVn,
            FilterSummary = new DeptLeaderFilterSummary
            {
                Preset = preset,
                FromDate = fromVn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ToDate = toVnExclusive.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                LogisticsStatus = logisticsStatus ?? "ALL",
                ItemType = itemType ?? "ALL",
                Priority = priority ?? "ALL",
                AssignedUserId = assignedUserId?.ToString() ?? "ALL",
                AssignedUserName = UserName(assignedUserId),
                DueStatus = dueStatus ?? "ALL",
                HandoverStatus = handoverStatus ?? "ALL",
                FeedbackRating = feedbackRating ?? "ALL",
                DepartmentName = deptInfo?.Name ?? $"Phòng ban #{deptId}",
                CampusName = deptInfo?.CampusName ?? string.Empty,
                GeneratedByName = generatedByName,
            },
            Kpis = new DeptLeaderKpis
            {
                NewRequests = CurCount(LogisticsItemStatus.Requested),
                WaitingAssignment = waitingAssignment,
                WaitingStaffResponse = CurCount(LogisticsItemStatus.Assigned),
                InProgress = CurCount(LogisticsItemStatus.InProgress),
                Completed = PeriodCount(LogisticsItemStatus.Done),
                Declined = PeriodCount(LogisticsItemStatus.Declined),
                Overdue = overdueCurrent,
                MissingHandoverSignature = missingSignatureCurrent,
                AverageResponseHours = averageResponseHours,
                AverageFeedbackRating = feedbackSummary.AverageRating,
            },
            AttentionItems = attentionItems,
            TaskStatusPipeline = taskStatusPipeline,
            WorkTypeDistribution = workTypeDistribution,
            MonthlyTrend = monthlyTrend,
            StaffPerformance = staffPerformance,
            PendingTasks = pendingTasks,
            PendingTasksTotal = pendingTasksTotal,
            ProposalChanges = proposalChanges,
            HandoverSummary = handoverSummary,
            HandoverTotal = handoverTotal,
            IncidentSummary = incidentSummary,
            FeedbackSummary = feedbackSummary,
        };
    }

    private async Task<List<DeptLeaderFeedbackEntry>> FeedbackEntries(
        IQueryable<PEMS.Domain.Entities.Feedbacks.Feedback> source, CancellationToken cancellationToken)
    {
        var rows = await source
            .Take(PreviewLimit)
            .Select(f => new
            {
                f.FeedbackId,
                f.VisitInstanceId,
                f.TargetNameSnapshot,
                f.Rating,
                f.Comment,
                f.SubmittedAt,
                DelegationName = _db.VisitRequestCampuses
                    .Where(ci => (ulong?)ci.VisitInstanceId == f.VisitInstanceId)
                    .Select(ci => ci.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus
                                  && ci.VisitRequest.HasMixedCampusDetails
                        ? (ci.FormDetail != null ? ci.FormDetail.DelegationName : null)
                        : ci.VisitRequest.DelegationName)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new DeptLeaderFeedbackEntry
        {
            FeedbackId = r.FeedbackId,
            VisitInstanceId = r.VisitInstanceId ?? 0,
            DelegationName = r.DelegationName ?? "—",
            ItemName = r.TargetNameSnapshot,
            Rating = r.Rating,
            Comment = r.Comment,
            SubmittedAt = r.SubmittedAt,
        }).ToList();
    }

    private static string HandoverStatusLabel(bool borrowerSigned, bool providerSigned, string? condition)
    {
        var c = condition?.Trim().ToUpperInvariant();
        if (c == "DAMAGED") return "Có hư hỏng";
        if (c == "MISSING") return "Thiếu/mất";
        if (!borrowerSigned && !providerSigned) return "Thiếu cả hai chữ ký";
        if (!borrowerSigned) return "Thiếu bên mượn ký";
        if (!providerSigned) return "Thiếu bên giao ký";
        return "Đủ chữ ký";
    }

    private static string NormalizePreset(string? preset)
    {
        var p = preset?.Trim().ToUpperInvariant();
        return p is "THIS_MONTH" or "THIS_QUARTER" or "THIS_YEAR" or "CUSTOM" ? p : "THIS_MONTH";
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
            case "THIS_QUARTER":
                var quarterStartMonth = ((nowVn.Month - 1) / 3) * 3 + 1;
                var quarterStart = new DateTime(nowVn.Year, quarterStartMonth, 1);
                return (quarterStart, quarterStart.AddMonths(3));
            case "THIS_YEAR":
                return (new DateTime(nowVn.Year, 1, 1), new DateTime(nowVn.Year + 1, 1, 1));
            case "CUSTOM":
                var from = (fromDate ?? new DateTime(nowVn.Year, 1, 1)).Date;
                var to = (toDate ?? nowVn).Date.AddDays(1);
                if (to <= from) to = from.AddDays(1);
                return (from, to);
            default: // THIS_MONTH
                var monthStart = new DateTime(nowVn.Year, nowVn.Month, 1);
                return (monthStart, monthStart.AddMonths(1));
        }
    }
}
