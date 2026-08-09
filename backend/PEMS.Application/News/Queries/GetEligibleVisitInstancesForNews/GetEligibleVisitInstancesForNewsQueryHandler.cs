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

        // Any Staff (regular or Leader) or Student may reach this far — the Host/accepted-participant
        // filter below is the real gate. This lets a Staff Leader who self-hosts a delegation (see
        // ApproveCampusInstanceCommandHandler) see it as eligible; a Leader who is not hosting anything
        // simply gets an empty list, same as a Staff Leader always did.
        var isAllowed = roleCode == RoleCodes.Staff || roleCode == RoleCodes.Student;
        if (!isAllowed)
            throw new ForbiddenException("Only Staff and Student can create news.");

        // Step 1: Candidate instances — the ones this user has ANY writer-side relation to. Note this
        // is only a CANDIDATE set: which of them the user may actually write for is decided in step 2
        // by the canonical evaluator, not by predicates repeated here.
        var participantRoleByInstance = await _dbContext.VisitParticipants
            .AsNoTracking()
            .Where(vp => vp.UserId == currentUserId
                         && vp.Status == ParticipantStatuses.Accepted
                         && !vp.IsHost)
            .Select(vp => new { vp.VisitInstanceId, vp.ParticipantRole })
            .ToDictionaryAsync(x => x.VisitInstanceId, x => x.ParticipantRole, cancellationToken);

        var hostedInstanceIds = await _dbContext.VisitRequestCampuses
            .AsNoTracking()
            .Where(vrc => vrc.CurrentHostUserId == currentUserId)
            .Select(vrc => vrc.VisitInstanceId)
            .ToListAsync(cancellationToken);

        var relatedInstanceIds = participantRoleByInstance.Keys.Union(hostedInstanceIds).ToList();

        // Step 1b: the ONE campus the caller arrived with, answered separately and always — including
        // when the list below turns out empty, because "your list is empty" is not a reason either.
        var requested = request.VisitInstanceId is { } requestedInstanceId
            ? await EvaluateRequestedAsync(
                requestedInstanceId,
                currentUserId,
                participantRoleByInstance.TryGetValue(requestedInstanceId, out var requestedRole)
                    ? requestedRole
                    : null,
                cancellationToken)
            : null;

        if (relatedInstanceIds.Count == 0)
            return new GetEligibleVisitInstancesResponse { Requested = requested };

        // Step 2: ONE eligibility verdict per candidate, from the SAME evaluator the process list, the
        // process permissions and the create command use.
        //
        // This step used to re-implement the business rules in LINQ, and its copy had drifted: it
        // applied the writing window, news_not_required and media consent, but accepted ANY accepted
        // participant — so a DEPT_SUPPORT was offered a visit the create command would refuse. Going
        // through the evaluator is what makes "Process says I can, Create News says I cannot"
        // impossible rather than merely unlikely. The candidate set above is narrow, so the
        // in-memory pass is over a handful of rows, not a table scan.
        var candidates = await _dbContext.VisitRequestCampuses
            .AsNoTracking()
            .Include(vrc => vrc.VisitRequest)
            .Include(vrc => vrc.FormDetail)
            .Where(vrc => relatedInstanceIds.Contains(vrc.VisitInstanceId))
            .ToListAsync(cancellationToken);

        var eligibleInstances = candidates
            .Where(vrc => PEMS.Application.Delegations.News.VisitNewsAccess.Evaluate(
                vrc, vrc.VisitRequest, _currentUser,
                participantRoleByInstance.TryGetValue(vrc.VisitInstanceId, out var role) ? role : null)
                .CanCreate)
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
            .ToList();

        if (eligibleInstances.Count == 0)
            return new GetEligibleVisitInstancesResponse { Requested = requested };

        var instanceIds = eligibleInstances.Select(v => v.VisitInstanceId).ToList();
        var requestIds  = eligibleInstances.Select(v => v.VisitRequestId).Distinct().ToList();
        var campusIds   = eligibleInstances.Select(v => v.CampusId).Distinct().ToList();

        // Step 3: Batch fetch EFFECTIVE per-instance delegation names — each instance answers from its own
        // detail, since the request row carries no name to share between them.
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

        return new GetEligibleVisitInstancesResponse { Items = items, Requested = requested };
    }

    /// <summary>
    /// The canonical verdict for ONE campus, reason code included.
    ///
    /// <para>
    /// It re-uses <c>VisitNewsAccess.Evaluate</c> and adds no rule of its own — the point of this
    /// endpoint is that the Create-News page stops deriving a cause from the ABSENCE of an item in a
    /// list. The evaluator reports the FIRST thing standing in the way, so somebody outside the visit
    /// is told that, rather than being told about media consent for a visit they have no part in.
    /// </para>
    /// </summary>
    private async Task<RequestedVisitInstanceEligibilityDto> EvaluateRequestedAsync(
        ulong visitInstanceId,
        ulong currentUserId,
        string? acceptedParticipantRole,
        CancellationToken cancellationToken)
    {
        var instance = await _dbContext.VisitRequestCampuses
            .AsNoTracking()
            .Include(vrc => vrc.VisitRequest)
            .Include(vrc => vrc.FormDetail)
            .FirstOrDefaultAsync(vrc => vrc.VisitInstanceId == visitInstanceId, cancellationToken);

        // An id that names nothing answers exactly as an id the caller has no relation to. "Không tìm
        // thấy chuyến" and "chuyến này không phải của bạn" are different sentences, and being able to
        // tell them apart is how a caller enumerates campuses they were never part of.
        if (instance is null)
            return new RequestedVisitInstanceEligibilityDto
            {
                VisitInstanceId = visitInstanceId,
                CanCreate = false,
                ReasonCode = PEMS.Application.Delegations.News.VisitNewsAccess.ReasonCodes.NotInScope,
            };

        // Đã có bài tính THEO TÁC GIẢ — bài của người khác cho cùng chuyến không chặn quyền viết của
        // người đang đăng nhập (cùng quy tắc CreateNewsCommandHandler áp dụng khi thật sự tạo).
        var existingNews = await _dbContext.News
            .AsNoTracking()
            .Where(n => n.VisitInstanceId == visitInstanceId && n.AuthorUserId == currentUserId)
            .Select(n => new { n.NewsId, n.Status })
            .FirstOrDefaultAsync(cancellationToken);

        var verdict = PEMS.Application.Delegations.News.VisitNewsAccess.Evaluate(
            instance, instance.VisitRequest, _currentUser, acceptedParticipantRole,
            alreadyHasOwnNews: existingNews is not null);

        return new RequestedVisitInstanceEligibilityDto
        {
            VisitInstanceId = visitInstanceId,
            CanCreate = verdict.CanCreate,
            ReasonCode = verdict.ReasonCode,
            HasNews = existingNews is not null,
            ExistingNewsId = existingNews?.NewsId,
            CanEditExisting = existingNews is not null
                && (existingNews.Status == NewsConstants.Status.PendingReview
                    || existingNews.Status == NewsConstants.Status.Rejected),
        };
    }
}
