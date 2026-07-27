using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.DepartmentLeaderPersonnel.Queries.GetDepartmentPersonnelDetail;

/// <summary>
/// Spec §11. The scope service both authorizes the caller and proves the target's membership, so this
/// handler is a projection over an already-vetted row. Read-only.
/// </summary>
public sealed class GetDepartmentPersonnelDetailQueryHandler
    : IRequestHandler<GetDepartmentPersonnelDetailQuery, GetDepartmentPersonnelDetailResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IDepartmentLeaderPersonnelScopeService _scopeService;

    public GetDepartmentPersonnelDetailQueryHandler(
        IApplicationDbContext db, IDepartmentLeaderPersonnelScopeService scopeService)
    {
        _db = db;
        _scopeService = scopeService;
    }

    public async Task<GetDepartmentPersonnelDetailResponse> Handle(
        GetDepartmentPersonnelDetailQuery request, CancellationToken cancellationToken)
    {
        var scope = await _scopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync(cancellationToken);

        // Throws 404 PERSONNEL_NOT_FOUND for a missing id AND for a target in another department.
        await _scopeService.EnsureTargetBelongsToCurrentDepartmentAsync(scope, request.UserId, cancellationToken);

        var headUserId = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == scope.DepartmentId)
            .Select(d => d.HeadUserId)
            .FirstOrDefaultAsync(cancellationToken);

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == request.UserId)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Phone,
                u.Gender,
                u.Status,
                RoleCode = u.Role.RoleCode,
                u.SubRole,
                u.AvatarUrl,
                u.CreatedAt,
                u.UpdatedAt,
                u.LastLoginAt,
            })
            .FirstAsync(cancellationToken);

        return new GetDepartmentPersonnelDetailResponse
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Gender = DepartmentPersonnelGenders.ToWire(user.Gender),
            Status = user.Status,
            RoleCode = user.RoleCode,
            SubRole = user.SubRole,
            Position = DepartmentPersonnelActionFlags.ResolvePosition(user.SubRole),
            AvatarUrl = user.AvatarUrl,
            DepartmentId = scope.DepartmentId,
            DepartmentName = scope.DepartmentName,
            CampusId = scope.CampusId,
            CampusName = scope.CampusName,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLoginAt = user.LastLoginAt,
            CanEdit = DepartmentPersonnelActionFlags.CanEdit(),
            CanDisable = DepartmentPersonnelActionFlags.CanDisable(
                user.UserId, user.Status, scope.ActorUserId, headUserId),
            CanEnable = DepartmentPersonnelActionFlags.CanEnable(user.UserId, user.Status, scope.ActorUserId),
            CanTransferLeadershipTo = DepartmentPersonnelActionFlags.CanTransferLeadershipTo(
                user.UserId, user.Status, user.SubRole, scope.ActorUserId, headUserId),
            CanResendEmailConfirmation = DepartmentPersonnelActionFlags.CanResendEmailConfirmation(user.Status),
            IsCurrentDepartmentLeader = headUserId == user.UserId,
        };
    }
}
