using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using NewsEntity = PEMS.Domain.Entities.News.News;

namespace PEMS.Application.News.Queries.GetEligibleVisitInstancesForNews;

public sealed class GetEligibleVisitInstancesForNewsQueryHandler
    : IRequestHandler<GetEligibleVisitInstancesForNewsQuery, GetEligibleVisitInstancesResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public GetEligibleVisitInstancesForNewsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<GetEligibleVisitInstancesResponse> Handle(
        GetEligibleVisitInstancesForNewsQuery request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("You do not have permission.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;
        var subRole = _currentUser.SubRole ?? string.Empty;

        var isAllowed = (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff)
                     || roleCode == RoleCodes.Student;
        if (!isAllowed)
            throw new ForbiddenException("Only Staff and Student can create news.");

        // Step 1: Find visit instances current user has ACCEPTED
        var acceptedInstanceIds = await _dbContext.VisitParticipants
            .AsNoTracking()
            .Where(vp => vp.UserId == currentUserId && vp.Status == ParticipantStatuses.Accepted)
            .Select(vp => vp.VisitInstanceId)
            .ToListAsync(cancellationToken);

        if (acceptedInstanceIds.Count == 0)
            return new GetEligibleVisitInstancesResponse();

        // Step 2: Filter to CLOSED instances only
        var closedInstances = await _dbContext.VisitRequestCampuses
            .AsNoTracking()
            .Where(vrc => acceptedInstanceIds.Contains(vrc.VisitInstanceId)
                       && vrc.Status == VisitInstanceStatuses.Closed)
            .Select(vrc => new
            {
                vrc.VisitInstanceId,
                vrc.VisitRequestId,
                vrc.CampusId,
                vrc.PlannedStartAt,
                vrc.PlannedEndAt,
                vrc.ClosedAt,
                vrc.Status
            })
            .ToListAsync(cancellationToken);

        if (closedInstances.Count == 0)
            return new GetEligibleVisitInstancesResponse();

        var instanceIds = closedInstances.Select(v => v.VisitInstanceId).ToList();
        var requestIds  = closedInstances.Select(v => v.VisitRequestId).Distinct().ToList();
        var campusIds   = closedInstances.Select(v => v.CampusId).Distinct().ToList();

        // Step 3: Batch fetch delegation names
        var delegationNames = await _dbContext.VisitRequests
            .AsNoTracking()
            .Where(vr => requestIds.Contains(vr.VisitRequestId))
            .Select(vr => new { vr.VisitRequestId, vr.DelegationName })
            .ToDictionaryAsync(vr => vr.VisitRequestId, vr => vr.DelegationName, cancellationToken);

        // Step 4: Batch fetch campus names
        var campusNames = await _dbContext.Campuses
            .AsNoTracking()
            .Where(c => campusIds.Contains(c.CampusId))
            .Select(c => new { c.CampusId, c.Name })
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        // Step 5: Check which instances already have news
        var instancesWithNews = await _dbContext.News
            .AsNoTracking()
            .Where(n => n.VisitInstanceId.HasValue && instanceIds.Contains(n.VisitInstanceId!.Value))
            .Select(n => n.VisitInstanceId!.Value)
            .ToListAsync(cancellationToken);
        var newsSet = instancesWithNews.ToHashSet();

        var items = new List<EligibleVisitInstanceDto>();
        foreach (var inst in closedInstances.OrderByDescending(v => v.PlannedStartAt))
        {
            var hasNews = newsSet.Contains(inst.VisitInstanceId);
            if (hasNews && !request.IncludeAlreadyHasNews)
                continue;

            delegationNames.TryGetValue(inst.VisitRequestId, out var delegationName);
            campusNames.TryGetValue(inst.CampusId, out var campusName);

            var title = !string.IsNullOrEmpty(delegationName)
                ? $"{delegationName} tại {campusName ?? "FPT University"}"
                : $"Chuyến tiếp khách #{inst.VisitInstanceId}";

            items.Add(new EligibleVisitInstanceDto
            {
                VisitInstanceId = inst.VisitInstanceId,
                VisitTitle      = title,
                CampusName      = campusName ?? string.Empty,
                PlannedStartAt  = inst.PlannedStartAt,
                PlannedEndAt    = inst.PlannedEndAt,
                ClosedAt        = inst.ClosedAt,
                Status          = inst.Status,
                HasNews         = hasNews,
                CanSelect       = !hasNews
            });
        }

        return new GetEligibleVisitInstancesResponse { Items = items };
    }
}
