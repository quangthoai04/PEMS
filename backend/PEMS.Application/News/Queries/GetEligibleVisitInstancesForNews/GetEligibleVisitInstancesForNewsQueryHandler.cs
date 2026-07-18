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

        // Step 1: Find visit instances current user has ACCEPTED (participant) hoặc đang là Host
        var acceptedInstanceIds = await _dbContext.VisitParticipants
            .AsNoTracking()
            .Where(vp => vp.UserId == currentUserId && vp.Status == ParticipantStatuses.Accepted)
            .Select(vp => vp.VisitInstanceId)
            .ToListAsync(cancellationToken);

        var hostedInstanceIds = await _dbContext.VisitRequestCampuses
            .AsNoTracking()
            .Where(vrc => vrc.CurrentHostUserId == currentUserId)
            .Select(vrc => vrc.VisitInstanceId)
            .ToListAsync(cancellationToken);

        var relatedInstanceIds = acceptedInstanceIds.Union(hostedInstanceIds).ToList();
        if (relatedInstanceIds.Count == 0)
            return new GetEligibleVisitInstancesResponse();

        // Step 2: Writing window — AFTER_VISIT hoặc CLOSED (bài viết sau tiếp khách; PUBLISHED là
        // điều kiện đóng đoàn nên phải viết được TRƯỚC khi đóng). Bỏ chuyến không yêu cầu tin tức
        // và chuyến khách không đồng ý truyền thông (backend create cũng chặn).
        var eligibleInstances = await _dbContext.VisitRequestCampuses
            .AsNoTracking()
            .Where(vrc => relatedInstanceIds.Contains(vrc.VisitInstanceId)
                       && (vrc.Status == VisitInstanceStatuses.AfterVisit
                           || vrc.Status == VisitInstanceStatuses.Closed)
                       && !vrc.NewsNotRequired
                       && vrc.VisitRequest.MediaConsentStatus == PEMS.Shared.MediaConsentStatus.Agreed)
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

        if (eligibleInstances.Count == 0)
            return new GetEligibleVisitInstancesResponse();

        var instanceIds = eligibleInstances.Select(v => v.VisitInstanceId).ToList();
        var requestIds  = eligibleInstances.Select(v => v.VisitRequestId).Distinct().ToList();
        var campusIds   = eligibleInstances.Select(v => v.CampusId).Distinct().ToList();

        // Step 3: Batch fetch EFFECTIVE per-instance delegation names (mixed per-campus v2 rows use
        // THIS instance's detail; v1/non-mixed keep the global projection — byte-identical there).
        var delegationNames = await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
            .ForInstancesAsync(_dbContext, instanceIds, cancellationToken);

        // Step 4: Batch fetch campus names
        var campusNames = await _dbContext.Campuses
            .AsNoTracking()
            .Where(c => campusIds.Contains(c.CampusId))
            .Select(c => new { c.CampusId, c.Name })
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        // Step 5: "Đã có bài" tính THEO TÁC GIẢ — mỗi người một bài / chuyến; bài của người khác
        // không chặn quyền viết của current user.
        var instancesWithOwnNews = await _dbContext.News
            .AsNoTracking()
            .Where(n => n.VisitInstanceId.HasValue && instanceIds.Contains(n.VisitInstanceId!.Value)
                     && n.AuthorUserId == currentUserId)
            .Select(n => n.VisitInstanceId!.Value)
            .ToListAsync(cancellationToken);
        var newsSet = instancesWithOwnNews.ToHashSet();

        var items = new List<EligibleVisitInstanceDto>();
        foreach (var inst in eligibleInstances.OrderByDescending(v => v.PlannedStartAt))
        {
            var hasNews = newsSet.Contains(inst.VisitInstanceId);
            if (hasNews && !request.IncludeAlreadyHasNews)
                continue;

            delegationNames.TryGetValue(inst.VisitInstanceId, out var delegationName);
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
