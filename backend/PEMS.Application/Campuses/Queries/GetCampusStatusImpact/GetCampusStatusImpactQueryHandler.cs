using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Application.Campuses.Queries.GetCampusStatusImpact;

/// <summary>
/// UC-86 §18 impact preview. Same HO/ADMIN + own-campus guards as ManageCampusStatus; computes
/// the shared <see cref="CampusStatusImpactCalculator"/> / <see cref="CampusActivationRequirements"/>
/// results without changing anything, so the modal and the transactional guard can never disagree.
/// </summary>
public sealed class GetCampusStatusImpactQueryHandler
    : IRequestHandler<GetCampusStatusImpactQuery, GetCampusStatusImpactResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoleAccessPolicy _accessPolicy;

    public GetCampusStatusImpactQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IRoleAccessPolicy accessPolicy)
    {
        _db = db;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
    }

    public async Task<GetCampusStatusImpactResponse> Handle(
        GetCampusStatusImpactQuery request, CancellationToken cancellationToken)
    {
        if (!_accessPolicy.CanAccessCampusManagement(_currentUser))
        {
            throw new AuthBusinessException(
                CampusErrorCodes.CampusManagementForbidden,
                "Bạn không có quyền xem ảnh hưởng trạng thái campus.", 403);
        }

        var targetStatus = request.TargetStatus.Trim().ToUpperInvariant();

        var campus = await _db.Campuses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CampusId == request.CampusId, cancellationToken)
            ?? throw new NotFoundException("Campus", request.CampusId);

        // Same own-campus gate as the command — the preview must not reveal a path the
        // command would reject anyway.
        if (_currentUser.PrimaryCampusId is { } ownCampusId && campus.CampusId == ownCampusId)
            throw new ForbiddenException("Bạn không thể thay đổi trạng thái campus của chính mình.");

        var isNoOp = campus.Status == targetStatus;
        var snapshot = await CampusAvailabilityEvaluator.EvaluateAsync(
            _db, campus.CampusId, cancellationToken);

        if (targetStatus == EntityStatuses.Inactive)
        {
            var impact = await CampusStatusImpactCalculator.ComputeDisableImpactAsync(
                _db, campus.CampusId, cancellationToken);

            return new GetCampusStatusImpactResponse
            {
                CampusId = campus.CampusId,
                Name = campus.Name,
                CurrentStatus = campus.Status,
                TargetStatus = targetStatus,
                CanChange = !isNoOp && !impact.HasBlockers,
                BlockerCount = impact.BlockerCount,
                BlockersByStatus = impact.BlockersByStatus,
                BlockerExamples = impact.BlockerExamples,
                Readiness = snapshot?.Readiness,
            };
        }

        // ── Enable preview: master data + ACTIVE IC department (BR-86-14); Staff Leader is
        // NOT required (BR-86-15) but the would-be readiness is returned for the warning. ──
        var enableIssues = new List<string>();
        foreach (var field in CampusActivationRequirements.GetMissingMasterData(campus))
            enableIssues.Add($"MASTER_DATA_INCOMPLETE:{field}");

        if (snapshot is null || snapshot.ActiveIcDepartmentCount == 0)
            enableIssues.Add(CampusReadinessIssues.ActiveIcDepartmentMissing);

        // Readiness the campus WOULD have once ACTIVE (rule re-run with ACTIVE status).
        var wouldBeReadiness = snapshot is null
            ? null
            : CampusReadinessRule.Evaluate(
                EntityStatuses.Active,
                snapshot.ActiveIcDepartmentCount,
                snapshot.ValidStaffLeaderCount);

        return new GetCampusStatusImpactResponse
        {
            CampusId = campus.CampusId,
            Name = campus.Name,
            CurrentStatus = campus.Status,
            TargetStatus = targetStatus,
            CanChange = !isNoOp && enableIssues.Count == 0,
            EnableIssues = enableIssues,
            Readiness = wouldBeReadiness,
        };
    }
}
