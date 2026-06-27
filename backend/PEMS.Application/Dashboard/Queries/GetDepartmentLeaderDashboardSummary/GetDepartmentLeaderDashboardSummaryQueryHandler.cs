using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Dashboard.Queries.GetDepartmentLeaderDashboardSummary;

public class GetDepartmentLeaderDashboardSummaryQueryHandler
    : IRequestHandler<GetDepartmentLeaderDashboardSummaryQuery, DepartmentLeaderDashboardSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetDepartmentLeaderDashboardSummaryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<DepartmentLeaderDashboardSummaryDto> Handle(
        GetDepartmentLeaderDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.RoleCode != "DEPARTMENT" || _currentUserService.DepartmentId == null)
        {
            throw new ForbiddenException("Only department users can access this dashboard.");
        }

        var departmentId = _currentUserService.DepartmentId.Value;
        var now = DateTime.UtcNow;
        var todayStart = now.AddHours(7).Date.AddHours(-7);
        var closedInstanceStatuses = new[] { "CANCELLED", "CLOSED" };

        var dto = new DepartmentLeaderDashboardSummaryDto
        {
            ServerNow = now.ToString("O")
        };

        var requestItemsQuery = _context.VisitLogisticsItems
            .Where(li => li.RequestedToDepartmentId == departmentId
                         && !closedInstanceStatuses.Contains(li.VisitInstance.Status)
                         && li.VisitInstance.VisitRequest.Status == "APPROVED");

        var invitationItemsQuery =
            from p in _context.VisitParticipants
            join u in _context.Users on p.UserId equals u.UserId
            join c in _context.VisitRequestCampuses on p.VisitInstanceId equals c.VisitInstanceId
            where u.DepartmentId == departmentId
                  && p.ParticipantRole == ParticipantRoles.DeptSupport
                  && p.Status != ParticipantStatuses.Removed
                  && !closedInstanceStatuses.Contains(c.Status)
                  && c.VisitRequest.Status == "APPROVED"
            select new { p, c };

        var pendingRequestCount = await requestItemsQuery
            .Where(li => li.AssignedToUserId == null
                         && li.Status == "REQUESTED"
                         && (li.UsageStartAt ?? li.DueAt ?? li.VisitInstance.PlannedStartAt) >= now)
            .CountAsync(cancellationToken);

        var pendingInvitationCount = await invitationItemsQuery
            .Where(x => x.p.AssignedBy == null
                        && x.p.AssignedAt == null
                        && x.p.Status == ParticipantStatuses.Invited
                        && x.c.PlannedStartAt >= now)
            .CountAsync(cancellationToken);

        dto.PendingAssignmentCount = pendingRequestCount + pendingInvitationCount;

        var upcomingRequestCount = await requestItemsQuery
            .Where(li => (li.UsageStartAt ?? li.VisitInstance.PlannedStartAt) >= now
                         && li.Status == "ACCEPTED")
            .CountAsync(cancellationToken);

        var upcomingInvitationCount = await invitationItemsQuery
            .Where(x => x.c.PlannedStartAt >= now && x.p.Status == ParticipantStatuses.Accepted)
            .CountAsync(cancellationToken);

        dto.UpcomingDelegationCount = upcomingRequestCount + upcomingInvitationCount;

        dto.ProcessingDelegationCount = await requestItemsQuery
            .Where(li => (li.UsageStartAt ?? li.VisitInstance.PlannedStartAt) <= now
                         && (li.UsageEndAt ?? li.VisitInstance.PlannedEndAt) >= now
                         && li.Status == "IN_PROGRESS")
            .CountAsync(cancellationToken);

        dto.ProcessingDelegationCount += await invitationItemsQuery
            .Where(x => x.p.Status == ParticipantStatuses.Accepted
                        && x.c.PlannedStartAt <= now
                        && x.c.PlannedEndAt >= now)
            .CountAsync(cancellationToken);

        dto.ActivePersonnelCount = await _context.Users
            .Where(u => u.DepartmentId == departmentId && u.Status == "ACTIVE")
            .CountAsync(cancellationToken);

        var requestQuickTasks = await requestItemsQuery
            .Where(li => li.AssignedToUserId == null
                         && li.Status == "REQUESTED"
                         && (li.UsageStartAt ?? li.DueAt ?? li.VisitInstance.PlannedStartAt) >= now)
            .OrderBy(li => li.UsageStartAt ?? li.DueAt ?? li.VisitInstance.PlannedStartAt)
            .Select(li => new DepartmentLeaderQuickTaskDto
            {
                ItemType = "REQUEST",
                LogisticsItemId = li.LogisticsItemId,
                ParticipantId = null,
                VisitInstanceId = li.VisitInstanceId,
                VisitRequestId = li.VisitInstance.VisitRequestId,
                DelegationName = li.VisitInstance.VisitRequest.DelegationName,
                TaskTitle = li.Title,
                DueAt = (li.UsageStartAt ?? li.DueAt ?? li.VisitInstance.PlannedStartAt).ToString("O"),
                Status = li.Status,
                AssignedToUserId = li.AssignedToUserId,
                AssignedToName = li.AssignedToUserId != null
                    ? _context.Users.FirstOrDefault(u => u.UserId == li.AssignedToUserId)!.FullName
                    : null
            })
            .ToListAsync(cancellationToken);

        var invitationQuickTasks = await invitationItemsQuery
            .Where(x => x.p.AssignedBy == null
                        && x.p.AssignedAt == null
                        && x.p.Status == ParticipantStatuses.Invited
                        && x.c.PlannedStartAt >= now)
            .OrderBy(x => x.c.PlannedStartAt)
            .Select(x => new DepartmentLeaderQuickTaskDto
            {
                ItemType = "INVITATION",
                LogisticsItemId = 0,
                ParticipantId = x.p.ParticipantId,
                VisitInstanceId = x.c.VisitInstanceId,
                VisitRequestId = x.c.VisitRequestId,
                DelegationName = x.c.VisitRequest.DelegationName,
                TaskTitle = "Thu moi tham gia don tiep",
                DueAt = x.c.PlannedStartAt.ToString("O"),
                Status = x.p.Status,
                AssignedToUserId = null,
                AssignedToName = null
            })
            .ToListAsync(cancellationToken);

        dto.QuickTasks = requestQuickTasks
            .Concat(invitationQuickTasks)
            .OrderBy(t => t.DueAt ?? DateTime.MaxValue.ToString("O"))
            .ToList();

        var requestUpcomingSchedules = await requestItemsQuery
            .Where(li => (li.UsageStartAt ?? li.VisitInstance.PlannedStartAt) >= now
                         && li.Status == "ACCEPTED")
            .OrderBy(li => li.UsageStartAt ?? li.VisitInstance.PlannedStartAt)
            .Select(li => new DepartmentLeaderUpcomingScheduleDto
            {
                ItemType = "REQUEST",
                LogisticsItemId = li.LogisticsItemId,
                ParticipantId = null,
                VisitInstanceId = li.VisitInstanceId,
                VisitRequestId = li.VisitInstance.VisitRequestId,
                DelegationName = li.VisitInstance.VisitRequest.DelegationName,
                OrganizationName = li.VisitInstance.VisitRequest.RegistrantOrganization,
                PlannedStartAt = (li.UsageStartAt ?? li.VisitInstance.PlannedStartAt).ToString("O"),
                PlannedEndAt = (li.UsageEndAt ?? li.VisitInstance.PlannedEndAt).ToString("O"),
                CampusName = _context.Campuses.FirstOrDefault(c => c.CampusId == li.VisitInstance.CampusId)!.Name
                             ?? "Unknown Campus",
                Location = null,
                Status = li.VisitInstance.Status
            })
            .ToListAsync(cancellationToken);

        var invitationUpcomingSchedules = await invitationItemsQuery
            .Where(x => x.c.PlannedStartAt >= now && x.p.Status == ParticipantStatuses.Accepted)
            .OrderBy(x => x.c.PlannedStartAt)
            .Select(x => new DepartmentLeaderUpcomingScheduleDto
            {
                ItemType = "INVITATION",
                LogisticsItemId = null,
                ParticipantId = x.p.ParticipantId,
                VisitInstanceId = x.c.VisitInstanceId,
                VisitRequestId = x.c.VisitRequestId,
                DelegationName = x.c.VisitRequest.DelegationName,
                OrganizationName = x.c.VisitRequest.RegistrantOrganization,
                PlannedStartAt = x.c.PlannedStartAt.ToString("O"),
                PlannedEndAt = x.c.PlannedEndAt.ToString("O"),
                CampusName = _context.Campuses.FirstOrDefault(c => c.CampusId == x.c.CampusId)!.Name
                             ?? "Unknown Campus",
                Location = null,
                Status = x.p.Status
            })
            .ToListAsync(cancellationToken);

        dto.UpcomingSchedules = requestUpcomingSchedules
            .Concat(invitationUpcomingSchedules)
            .OrderBy(s => s.PlannedStartAt)
            .ToList();

        return dto;
    }
}
