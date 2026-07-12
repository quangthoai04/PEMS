using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

using PEMS.Application.Common;
namespace PEMS.Application.DepartmentReceptionTasks.Queries.GetAssignmentsProgressList;

public sealed class GetAssignmentsProgressListQuery : IRequest<AssignmentsProgressListDto>
{
    public string? Search { get; set; }
    public string? ItemType { get; set; }
    public string? Status { get; set; }
    public string? StatusGroup { get; set; }
    public string? OwnerScope { get; set; }
    /// <summary>Optional filter on logistics priority: LOW | MEDIUM | HIGH | URGENT (null/empty = all).</summary>
    public string? Priority { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class AssignmentsProgressListDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public List<AssignmentsProgressItemDto> Items { get; set; } = new();
}

public sealed class AssignmentsProgressItemDto
{
    public string ItemType { get; set; } = "";
    public ulong ItemId { get; set; }
    public ulong VisitRequestId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public ulong? LogisticsItemId { get; set; }
    public ulong? ParticipantId { get; set; }
    public string DelegationName { get; set; } = "";
    public string RequestCode { get; set; } = "";
    public string? OrganizationName { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public ulong? CurrentResponsibleUserId { get; set; }
    public string? CurrentResponsibleName { get; set; }
    public string? CurrentResponsibleRole { get; set; }  // "Leader" hoặc "Nhân viên"
    public bool IsCurrentUserResponsible { get; set; }
    public bool IsLeaderSelfAccepted { get; set; }
    public string RawStatus { get; set; } = "";
    public string UiStatus { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    /// <summary>Logistics priority (REQUEST items only): LOW | MEDIUM | HIGH | URGENT. Null for invitations.</summary>
    public string? Priority { get; set; }
    public string? DueAt { get; set; }   // "yyyy-MM-ddTHH:mm:ss" wall-clock, null for invitations / no deadline
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool CanViewDetail { get; set; }
    public bool CanViewDelegationDetail { get; set; }
    public bool CanAssign { get; set; }
    public bool CanAccept { get; set; }
    public bool CanDecline { get; set; }
    public bool CanRejectRequest { get; set; }
    public bool CanProposeChange { get; set; }
    public bool CanSignBorrow { get; set; }
    public bool CanSignReturn { get; set; }
    public string? LatestDeclineReason { get; set; }
    public string? LatestDeclinedByName { get; set; }
    public string? LatestDeclinedAt { get; set; }
    public string? LatestAssignmentAttemptStatus { get; set; }
    public bool NeedsAttention { get; set; }
    public string? AttentionReason { get; set; }
    public string? CancelReason { get; set; }
    // Audit trail
    public string? ActionByName { get; set; }     // Người thực hiện hành động gần nhất
    public string? ActionByRole { get; set; }
    public DateTime? ActionAt { get; set; }
    /// <summary>True nếu chính current user là người đã xử lý (đồng ý / từ chối) đơn này.</summary>
    public bool IsActedByCurrentUser { get; set; }
}

public sealed class GetAssignmentsProgressListQueryHandler
    : IRequestHandler<GetAssignmentsProgressListQuery, AssignmentsProgressListDto>
{
    private sealed record UserListInfo(string FullName, string? SubRole);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAssignmentsProgressListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AssignmentsProgressListDto> Handle(GetAssignmentsProgressListQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null || _currentUser.DepartmentId is null)
            throw new ForbiddenException();

        var currentUserId = _currentUser.UserId.Value;
        var departmentId = _currentUser.DepartmentId.Value;
        var now = VietnamTime.Now();
        var isDepartmentStaff = string.Equals(_currentUser.RoleCode, RoleCodes.Department, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_currentUser.SubRole, UserSubRoles.Staff, StringComparison.OrdinalIgnoreCase);

        var department = await _db.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartmentId == departmentId, cancellationToken);
        var leaderUserId = department?.HeadUserId;

        var userDetails = await _db.Users
            .AsNoTracking()
            .Where(u => u.DepartmentId == departmentId)
            .Select(u => new { u.UserId, u.FullName, u.SubRole })
            .ToDictionaryAsync(u => u.UserId, u => new UserListInfo(u.FullName, u.SubRole), cancellationToken);
        var userNames = userDetails;

        var requestRows = await (
            from li in _db.VisitLogisticsItems.AsNoTracking()
            join inst in _db.VisitRequestCampuses.AsNoTracking() on li.VisitInstanceId equals inst.VisitInstanceId
            join vr in _db.VisitRequests.AsNoTracking() on inst.VisitRequestId equals vr.VisitRequestId
            where li.RequestedToDepartmentId == departmentId
                  && (!isDepartmentStaff || li.AssignedToUserId == currentUserId)
            let latestAttempt = _db.VisitLogisticsAssignmentAttempts
                .Where(a => a.LogisticsItemId == li.LogisticsItemId)
                .OrderByDescending(a => a.AssignedAt)
                .Select(a => new { a.Status, a.ResponseNote })
                .FirstOrDefault()
            let borrowSigned = _db.VisitLogisticsItemHandovers
                .Any(h => h.LogisticsItemId == li.LogisticsItemId
                          && h.HandoverType == "BORROW"
                          && h.BorrowerSignedAt != null
                          && h.ProviderSignedAt != null)
            let returnSigned = _db.VisitLogisticsItemHandovers
                .Any(h => h.LogisticsItemId == li.LogisticsItemId
                          && h.HandoverType == "RETURN"
                          && h.BorrowerSignedAt != null
                          && h.ProviderSignedAt != null)
            select new
            {
                li,
                inst,
                vr,
                LatestStatus = latestAttempt == null ? null : latestAttempt.Status,
                LatestNote = latestAttempt == null ? null : latestAttempt.ResponseNote,
                BorrowSigned = borrowSigned,
                ReturnSigned = returnSigned
            })
            .ToListAsync(cancellationToken);

        // Fetch latest attempt with actor info for audit trail
        var logisticsIds = requestRows.Select(r => r.li.LogisticsItemId).ToList();
        var latestAttemptDetails = await _db.VisitLogisticsAssignmentAttempts
            .AsNoTracking()
            .Where(a => logisticsIds.Contains(a.LogisticsItemId))
            .GroupBy(a => a.LogisticsItemId)
            .Select(g => g.OrderByDescending(a => a.AssignedAt).First())
            .ToListAsync(cancellationToken);
        var attemptByItem = latestAttemptDetails.ToDictionary(a => a.LogisticsItemId);

        // Fetch assignee user IDs to get names outside the department (e.g. declined by staff)
        var allAssigneeIds = requestRows
            .Where(r => r.li.AssignedToUserId.HasValue)
            .Select(r => r.li.AssignedToUserId!.Value)
            .Distinct().ToList();
        var assigneeNames = await _db.Users
            .AsNoTracking()
            .Where(u => allAssigneeIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FullName, u.SubRole })
            .ToDictionaryAsync(u => u.UserId, u => new UserListInfo(u.FullName, u.SubRole), cancellationToken);

        var items = requestRows.Select(row =>
        {
            var startAt = row.li.UsageStartAt ?? row.inst.PlannedStartAt;
            var endAt = row.li.UsageEndAt ?? row.inst.PlannedEndAt;
            var uiStatus = NormalizeRequestStatus(row.li.Status, row.inst.Status, row.vr.Status, row.BorrowSigned, row.ReturnSigned);
            var isSelfAccepted = row.li.AssignedToUserId == currentUserId && row.li.Status == "ACCEPTED";
            var hasActiveAssignee = row.li.AssignedToUserId.HasValue
                && row.li.AssignedAt.HasValue
                && row.li.Status != "REQUESTED"
                && row.li.Status != "REJECTED"
                && row.LatestStatus != "REJECTED";

            var responsibleUserId = row.li.AssignedToUserId;
            var responsibleInfo = responsibleUserId.HasValue && userNames.TryGetValue(responsibleUserId.Value, out var aInfo)
                ? aInfo
                : (responsibleUserId.HasValue && assigneeNames.TryGetValue(responsibleUserId.Value, out var aInfo2) ? aInfo2 : null);
            var responsibleName = responsibleInfo?.FullName;
            var responsibleRole = ToDepartmentRoleLabel(responsibleInfo?.SubRole, responsibleUserId, leaderUserId);

            // Audit trail: latest attempt actor
            attemptByItem.TryGetValue(row.li.LogisticsItemId, out var latestAttemptDetail);
            string? declinedByName = null;
            string? declinedAt = null;
            if (row.LatestStatus == "REJECTED" && latestAttemptDetail != null)
            {
                var declinedUserId2 = latestAttemptDetail.AssigneeUserId;
                declinedByName = userNames.TryGetValue(declinedUserId2, out var dn) ? dn.FullName
                    : (assigneeNames.TryGetValue(declinedUserId2, out var dn2) ? dn2.FullName : null);
                declinedAt = latestAttemptDetail.AssignedAt.ToString("HH:mm dd/MM/yyyy");
            }

            return new AssignmentsProgressItemDto
            {
                ItemType = "REQUEST",
                ItemId = row.li.LogisticsItemId,
                LogisticsItemId = row.li.LogisticsItemId,
                VisitInstanceId = row.inst.VisitInstanceId,
                VisitRequestId = row.vr.VisitRequestId,
                DelegationName = row.vr.DelegationName ?? "",
                RequestCode = row.vr.RequestCode ?? "",
                OrganizationName = row.vr.RegistrantOrganization,
                Title = row.li.Title,
                Description = row.li.Description,
                CurrentResponsibleUserId = responsibleUserId,
                CurrentResponsibleName = hasActiveAssignee ? responsibleName : null,
                CurrentResponsibleRole = hasActiveAssignee ? responsibleRole : null,
                IsCurrentUserResponsible = row.li.AssignedToUserId == currentUserId,
                IsLeaderSelfAccepted = isSelfAccepted && leaderUserId == currentUserId,
                // True khi leader đã chính tay từ chối (được ghi vào UpdatedBy khi REJECTED)
                IsActedByCurrentUser = (row.li.Status == "REJECTED" && row.li.UpdatedBy == currentUserId)
                    || row.li.ProposedBy == currentUserId,
                RawStatus = row.li.Status,
                UiStatus = uiStatus,
                StatusLabel = ToStatusLabel(uiStatus),
                Priority = row.li.Priority,
                DueAt = row.li.DueAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
                StartAt = startAt,
                EndAt = endAt,
                CanViewDetail = true,
                CanViewDelegationDetail = true,
                CanAssign = !isDepartmentStaff && (uiStatus is "REQUESTED"),
                // Leader có thể tự Xác nhận hoặc Từ chối cả khi REQUESTED và DECLINED
                CanAccept = isDepartmentStaff
                    ? row.li.AssignedToUserId == currentUserId && row.li.Status == "ASSIGNED"
                    : !hasActiveAssignee && (row.li.Status == "REQUESTED"),
                CanDecline = row.li.AssignedToUserId == currentUserId && row.li.Status == "ASSIGNED",
                CanRejectRequest = !isDepartmentStaff && (uiStatus is "REQUESTED"),
                CanProposeChange = row.li.AssignedToUserId == currentUserId && row.li.Status == "ASSIGNED",
                CanSignBorrow = row.li.AssignedToUserId == currentUserId && row.li.Status == "ACCEPTED",
                CanSignReturn = row.li.AssignedToUserId == currentUserId && row.li.Status == "IN_PROGRESS",
                LatestDeclineReason = row.LatestStatus == "REJECTED" ? row.LatestNote ?? row.li.AssigneeResponseNote : null,
                LatestDeclinedByName = declinedByName,
                LatestDeclinedAt = declinedAt,
                LatestAssignmentAttemptStatus = row.LatestStatus,
                NeedsAttention = IsRequestNeedsAttention(row.li.Status, row.li.AssignedToUserId, currentUserId, startAt, now),
                AttentionReason = uiStatus == "CANCELLED" ? "Đơn đã hủy vì đoàn khách đã hủy" : BuildAttentionReason(row.li.Status, startAt, now),
                CancelReason = row.inst.CancellationReason ?? row.vr.CancellationReason
            };
        }).ToList();

        var invitationRows = await (
            from p in _db.VisitParticipants.AsNoTracking()
            join u in _db.Users.AsNoTracking() on p.UserId equals u.UserId
            join inst in _db.VisitRequestCampuses.AsNoTracking() on p.VisitInstanceId equals inst.VisitInstanceId
            join vr in _db.VisitRequests.AsNoTracking() on inst.VisitRequestId equals vr.VisitRequestId
            where u.DepartmentId == departmentId
                  && p.ParticipantRole == ParticipantRoles.DeptSupport
                  && p.Status != ParticipantStatuses.Removed
                  && (!isDepartmentStaff || (p.UserId == currentUserId && p.AssignedBy != null))
            select new { p, u, inst, vr })
            .ToListAsync(cancellationToken);

        var invitationGroups = invitationRows
            .GroupBy(x => x.inst.VisitInstanceId)
            .Select(g =>
            {
                var activeStaff = g
                    .Where(x => x.p.AssignedBy != null && x.p.Status != ParticipantStatuses.Declined && x.p.Status != ParticipantStatuses.Removed)
                    .OrderByDescending(x => x.p.AssignedAt ?? x.p.InvitedAt ?? x.p.CreatedAt)
                    .FirstOrDefault();
                var declinedStaff = g
                    .Where(x => x.p.AssignedBy != null && x.p.Status == ParticipantStatuses.Declined)
                    .OrderByDescending(x => x.p.RespondedAt ?? x.p.AssignedAt ?? x.p.CreatedAt)
                    .FirstOrDefault();
                var leaderRow = g
                    .Where(x => leaderUserId == null || x.p.UserId == leaderUserId.Value)
                    .OrderByDescending(x => x.p.InvitedAt ?? x.p.CreatedAt)
                    .FirstOrDefault();
                var selected = activeStaff ?? declinedStaff ?? leaderRow ?? g.First();
                return new { selected, activeStaff, declinedStaff, leaderRow };
            })
            .Where(x => x.selected != null)
            .ToList();

        foreach (var group in invitationGroups)
        {
            var row = group.selected!;
            var activeStaff = group.activeStaff;
            var leaderRow = group.leaderRow;
            var uiStatus = NormalizeInvitationStatus(row.p.Status, row.p.AssignedBy != null, row.inst.Status, row.inst.PlannedStartAt, row.inst.PlannedEndAt, now);
            var isLeaderSelfAccepted = row.p.UserId == currentUserId && row.p.Status == ParticipantStatuses.Accepted;
            var canLeaderHandle = uiStatus is "REQUESTED" or "DECLINED";

            items.Add(new AssignmentsProgressItemDto
            {
                ItemType = "INVITATION",
                ItemId = row.p.ParticipantId,
                ParticipantId = row.p.ParticipantId,
                VisitInstanceId = row.inst.VisitInstanceId,
                VisitRequestId = row.vr.VisitRequestId,
                DelegationName = row.vr.DelegationName ?? "",
                RequestCode = row.vr.RequestCode ?? "",
                OrganizationName = row.vr.RegistrantOrganization,
                Title = "Thư mời tham gia đón tiếp",
                Description = row.p.Note ?? row.vr.WorkingContent,
                CurrentResponsibleUserId = activeStaff?.p.UserId ?? row.p.UserId,
                CurrentResponsibleName = activeStaff?.u.FullName ?? row.u.FullName,
                CurrentResponsibleRole = ToDepartmentRoleLabel((activeStaff?.u ?? row.u).SubRole, activeStaff?.p.UserId ?? row.p.UserId, leaderUserId),
                IsCurrentUserResponsible = (activeStaff?.p.UserId ?? row.p.UserId) == currentUserId,
                IsLeaderSelfAccepted = isLeaderSelfAccepted,
                // True khi chính current user là participant (đã đồng ý hoặc từ chối thư mời)
                IsActedByCurrentUser = row.p.UserId == currentUserId,
                RawStatus = row.p.Status,
                UiStatus = uiStatus,
                StatusLabel = ToStatusLabel(uiStatus),
                StartAt = row.inst.PlannedStartAt,
                EndAt = row.inst.PlannedEndAt,
                CanViewDetail = true,
                CanViewDelegationDetail = true,
                CanAssign = !isDepartmentStaff && canLeaderHandle,
                CanAccept = row.p.UserId == currentUserId && row.p.Status is ParticipantStatuses.Invited or ParticipantStatuses.Assigned,
                CanDecline = row.p.UserId == currentUserId && row.p.Status is ParticipantStatuses.Invited or ParticipantStatuses.Assigned,
                CanRejectRequest = row.p.UserId == currentUserId && row.p.Status == ParticipantStatuses.Invited,
                CanProposeChange = false,
                CanSignBorrow = false,
                CanSignReturn = false,
                LatestDeclineReason = group.declinedStaff?.p.Note,
                LatestAssignmentAttemptStatus = activeStaff == null ? null : activeStaff.p.Status,
                NeedsAttention = IsInvitationNeedsAttention(uiStatus, row.p.UserId, currentUserId, row.inst.PlannedStartAt, now),
                AttentionReason = uiStatus == "REQUESTED" ? "Thư mời chưa được xử lý/phân công" : null,
                CancelReason = row.inst.CancellationReason ?? row.vr.CancellationReason
            });
        }

        var query = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                Contains(x.DelegationName, keyword) ||
                Contains(x.RequestCode, keyword) ||
                Contains(x.OrganizationName, keyword) ||
                Contains(x.Title, keyword) ||
                Contains(x.Description, keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.ItemType) && !request.ItemType.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.ItemType.Equals(request.ItemType, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.UiStatus.Equals(request.Status, StringComparison.OrdinalIgnoreCase));
        else if (!string.IsNullOrWhiteSpace(request.StatusGroup) && request.StatusGroup.Equals("STAFF_HISTORY", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.UiStatus is "ACCEPTED" or "DECLINED" or "REJECTED" or "CHANGE_PROPOSED" or "IN_PROGRESS" or "DONE" or "CANCELLED");

        if (!string.IsNullOrWhiteSpace(request.OwnerScope) && request.OwnerScope.Equals("ME", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x =>
                x.CurrentResponsibleUserId == currentUserId ||
                x.IsLeaderSelfAccepted ||
                x.IsActedByCurrentUser);

        if (request.FromDate.HasValue)
            query = query.Where(x => x.EndAt >= request.FromDate.Value.Date);

        if (request.ToDate.HasValue)
            query = query.Where(x => x.StartAt < request.ToDate.Value.Date.AddDays(1));

        // Optional priority filter (REQUEST items only — invitations carry no priority). Invalid values
        // are rejected so the dropdown can't smuggle an out-of-enum value.
        if (!string.IsNullOrWhiteSpace(request.Priority) && !request.Priority.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var p = request.Priority.Trim().ToUpperInvariant();
            if (!ValidPriorities.Contains(p))
                throw new BusinessRuleException("Mức ưu tiên không hợp lệ.");
            query = query.Where(x => x.Priority == p);
        }

        // Sort: when the client passes no SortBy (or asks for PRIORITY), default to urgency first
        // (URGENT→HIGH→MEDIUM→LOW), then nearest deadline (due_at asc, nulls last), then soonest start.
        // Any explicit SortBy keeps the legacy start-time ordering so existing screens are unchanged.
        var sortBy = request.SortBy?.Trim().ToUpperInvariant();
        var desc = request.SortDirection?.Equals("DESC", StringComparison.OrdinalIgnoreCase) == true;
        if (string.IsNullOrEmpty(sortBy) || sortBy == "PRIORITY")
        {
            query = query
                .OrderByDescending(x => PriorityWeight(x.Priority))
                .ThenBy(x => DueSortKey(x.DueAt))
                .ThenBy(x => x.StartAt);
        }
        else
        {
            query = desc ? query.OrderByDescending(x => x.StartAt) : query.OrderBy(x => x.StartAt);
        }

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var total = query.Count();
        var pageItems = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new AssignmentsProgressListDto
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            Items = pageItems
        };
    }

    private static readonly HashSet<string> ValidPriorities = new() { "LOW", "MEDIUM", "HIGH", "URGENT" };

    /// <summary>Sort weight for urgency (higher = more urgent). Items without a real priority
    /// (invitations) get 0 so they sink below every logistics request when sorting by priority.</summary>
    private static int PriorityWeight(string? priority) => priority?.ToUpperInvariant() switch
    {
        "URGENT" => 4,
        "HIGH" => 3,
        "MEDIUM" => 2,
        "LOW" => 1,
        _ => 0,
    };

    /// <summary>Deadline sort key: parsed due_at, or DateTime.MaxValue so missing deadlines sort last.</summary>
    private static DateTime DueSortKey(string? dueAt)
        => DateTime.TryParse(dueAt, out var d) ? d : DateTime.MaxValue;

    private static bool Contains(string? value, string keyword)
        => !string.IsNullOrWhiteSpace(value) && value.ToLowerInvariant().Contains(keyword);

    private static string ToDepartmentRoleLabel(string? subRole, ulong? userId, ulong? leaderUserId)
    {
        if (string.Equals(subRole, "LEADER", StringComparison.OrdinalIgnoreCase) || userId == leaderUserId)
            return "Trưởng phòng";
        return "Nhân viên";
    }

    private static string NormalizeRequestStatus(string status, string instanceStatus, string requestStatus, bool borrowSigned, bool returnSigned)
    {
        if (requestStatus == "CANCELLED" || instanceStatus == "CANCELLED" || status == "CANCELLED") return "CANCELLED";
        if (returnSigned || status == "DONE") return "DONE";
        if (borrowSigned || status == "IN_PROGRESS") return "IN_PROGRESS";

        return status switch
        {
            "REQUESTED" => "REQUESTED",
            "ASSIGNED" => "ASSIGNED",
            "ACCEPTED" => "ACCEPTED",
            "DECLINED" => "REJECTED",
            "REJECTED" => "REJECTED",
            "CHANGE_PROPOSED" => "CHANGE_PROPOSED",
            _ => status
        };
    }

    private static string NormalizeInvitationStatus(
        string status,
        bool hasActiveStaffAssignment,
        string instanceStatus,
        DateTime startAt,
        DateTime endAt,
        DateTime now)
    {
        if (instanceStatus == "CANCELLED") return "CANCELLED";
        if (status == ParticipantStatuses.Declined) return "REJECTED";
        if (hasActiveStaffAssignment && status != ParticipantStatuses.Accepted) return "ASSIGNED";
        if (status == ParticipantStatuses.Assigned) return "ASSIGNED";
        if (status == ParticipantStatuses.Invited) return "REQUESTED";
        if (status == ParticipantStatuses.Accepted)
        {
            if (instanceStatus is "AFTER_VISIT" or "CLOSED" || now > endAt) return "DONE";
            if (instanceStatus == "DURING_VISIT" || (now >= startAt && now <= endAt)) return "IN_PROGRESS";
            return "ACCEPTED";
        }
        return status;
    }

    private static string ToStatusLabel(string status)
        => status switch
        {
            "REQUESTED" => "Chưa phân công",
            "ASSIGNED" => "Đã giao",
            "ACCEPTED" => "Chấp nhận",
            "REJECTED" => "Từ chối",
            "DECLINED" => "Từ chối phân công",
            "CHANGE_PROPOSED" => "Đang đề xuất",
            "IN_PROGRESS" => "Trong tiến trình",
            "DONE" => "Hoàn thành",
            "CANCELLED" => "Đã hủy",
            _ => status
        };

    private static bool IsRequestNeedsAttention(string status, ulong? assigneeId, ulong currentUserId, DateTime startAt, DateTime now)
        => assigneeId == currentUserId
           && status is "ASSIGNED" or "ACCEPTED" or "IN_PROGRESS"
           && startAt <= now.AddDays(2);

    private static bool IsInvitationNeedsAttention(string uiStatus, ulong responsibleId, ulong currentUserId, DateTime startAt, DateTime now)
        => responsibleId == currentUserId
           && uiStatus is "REQUESTED" or "ASSIGNED" or "ACCEPTED" or "IN_PROGRESS"
           && startAt <= now.AddDays(2);

    private static string? BuildAttentionReason(string status, DateTime startAt, DateTime now)
    {
        if (status == "IN_PROGRESS") return "Đang chờ nghiệm thu/hoàn tất";
        if (startAt <= now.AddHours(24)) return "Sắp đến giờ xử lý";
        return null;
    }
}
