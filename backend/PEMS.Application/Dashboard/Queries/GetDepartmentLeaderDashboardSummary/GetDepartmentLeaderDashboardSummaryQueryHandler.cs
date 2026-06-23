using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Exceptions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PEMS.Application.Dashboard.Queries.GetDepartmentLeaderDashboardSummary;

public class GetDepartmentLeaderDashboardSummaryQueryHandler : IRequestHandler<GetDepartmentLeaderDashboardSummaryQuery, DepartmentLeaderDashboardSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetDepartmentLeaderDashboardSummaryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<DepartmentLeaderDashboardSummaryDto> Handle(GetDepartmentLeaderDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        if (_currentUserService.RoleCode != "DEPARTMENT" || _currentUserService.DepartmentId == null)
        {
            throw new ForbiddenException("Only department users can access this dashboard.");
        }

        ulong departmentId = _currentUserService.DepartmentId.Value;
        var now = DateTime.UtcNow;
        var dto = new DepartmentLeaderDashboardSummaryDto
        {
            ServerNow = now.ToString("O")
        };

        // KPI 1: Chờ phân công
        dto.PendingAssignmentCount = await _context.VisitLogisticsItems
            .Where(li => li.RequestedToDepartmentId == departmentId 
                         && li.AssignedToUserId == null 
                         && new[] { "REQUESTED", "RECEIVED", "PLANNED" }.Contains(li.Status)
                         && li.VisitInstance.Status != "CANCELLED" && li.VisitInstance.Status != "CLOSED"
                         && li.VisitInstance.VisitRequest.Status == "APPROVED")
            .Select(li => li.VisitInstanceId)
            .Distinct()
            .CountAsync(cancellationToken);

        // KPI 2: Đoàn sắp tới
        dto.UpcomingDelegationCount = await _context.VisitLogisticsItems
            .Where(li => li.RequestedToDepartmentId == departmentId 
                         && li.AssignedToUserId != null
                         && li.VisitInstance.PlannedStartAt > now
                         && new[] { "ASSIGNED", "BEFORE_VISIT" }.Contains(li.VisitInstance.Status)
                         && li.VisitInstance.VisitRequest.Status == "APPROVED")
            .Select(li => li.VisitInstanceId)
            .Distinct()
            .CountAsync(cancellationToken);

        // KPI 3: Đang xử lý
        dto.ProcessingDelegationCount = await _context.VisitLogisticsItems
            .Where(li => li.RequestedToDepartmentId == departmentId 
                         && li.AssignedToUserId != null
                         && li.VisitInstance.PlannedStartAt <= now
                         && li.VisitInstance.PlannedEndAt >= now
                         && new[] { "ASSIGNED", "BEFORE_VISIT", "DURING_VISIT" }.Contains(li.VisitInstance.Status)
                         && li.VisitInstance.VisitRequest.Status == "APPROVED")
            .Select(li => li.VisitInstanceId)
            .Distinct()
            .CountAsync(cancellationToken);

        // KPI 4: Nhân sự
        dto.ActivePersonnelCount = await _context.Users
            .Where(u => u.DepartmentId == departmentId && u.Status == "ACTIVE")
            .CountAsync(cancellationToken);

        // Tác vụ cần xử lý nhanh (Quick Tasks)
        var quickTasks = await _context.VisitLogisticsItems
            .Where(li => li.RequestedToDepartmentId == departmentId 
                         && new[] { "REQUESTED", "RECEIVED", "PLANNED" }.Contains(li.Status)
                         && li.VisitInstance.Status != "CANCELLED" && li.VisitInstance.Status != "CLOSED"
                         && li.VisitInstance.VisitRequest.Status == "APPROVED")
            .OrderBy(li => li.DueAt ?? DateTime.MaxValue)
            .Take(5)
            .Select(li => new DepartmentLeaderQuickTaskDto
            {
                LogisticsItemId = li.LogisticsItemId,
                VisitInstanceId = li.VisitInstanceId,
                VisitRequestId = li.VisitInstance.VisitRequestId,
                DelegationName = li.VisitInstance.VisitRequest.DelegationName,
                TaskTitle = li.Title,
                DueAt = li.DueAt.HasValue ? li.DueAt.Value.ToString("O") : null,
                Status = li.Status,
                AssignedToUserId = li.AssignedToUserId,
                AssignedToName = li.AssignedToUserId != null ? _context.Users.FirstOrDefault(u => u.UserId == li.AssignedToUserId).FullName : null
            })
            .ToListAsync(cancellationToken);
        
        dto.QuickTasks = quickTasks;

        // Lịch tiếp đón sắp tới
        var upcomingSchedules = await _context.VisitRequestCampuses
            .Where(vc => vc.LogisticsItems.Any(li => li.RequestedToDepartmentId == departmentId)
                         && vc.PlannedStartAt > now
                         && new[] { "ASSIGNED", "BEFORE_VISIT" }.Contains(vc.Status)
                         && vc.VisitRequest.Status == "APPROVED")
            .OrderBy(vc => vc.PlannedStartAt)
            .Take(5)
            .Select(vc => new DepartmentLeaderUpcomingScheduleDto
            {
                VisitInstanceId = vc.VisitInstanceId,
                VisitRequestId = vc.VisitRequestId,
                DelegationName = vc.VisitRequest.DelegationName,
                OrganizationName = vc.VisitRequest.RegistrantOrganization,
                PlannedStartAt = vc.PlannedStartAt.ToString("O"),
                PlannedEndAt = vc.PlannedEndAt.ToString("O"),
                CampusName = _context.Campuses.FirstOrDefault(c => c.CampusId == vc.CampusId).Name ?? "Unknown Campus",
                Location = null, // Can fetch from agenda if needed
                Status = vc.Status
            })
            .ToListAsync(cancellationToken);

        dto.UpcomingSchedules = upcomingSchedules;

        return dto;
    }
}
