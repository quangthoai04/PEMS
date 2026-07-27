using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>Department type values used by <c>departments.department_type</c>.</summary>
public static class DepartmentTypes
{
    /// <summary>An ordinary campus department headed by a DEPARTMENT/LEADER.</summary>
    public const string General = "GENERAL";

    /// <summary>The per-campus International Cooperation department, headed by a STAFF/LEADER.</summary>
    public const string Ic = "IC";
}

/// <inheritdoc cref="IDepartmentLeaderPersonnelScopeService"/>
public sealed class DepartmentLeaderPersonnelScopeService : IDepartmentLeaderPersonnelScopeService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DepartmentLeaderPersonnelScopeService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DepartmentLeaderScope> EnsureCurrentUserIsActualDepartmentLeaderAsync(
        CancellationToken cancellationToken)
    {
        // 1. Authenticated. A missing/short JWT never reaches the database.
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } actorId)
            throw new AuthBusinessException(
                DepartmentLeaderErrorCodes.DepartmentLeaderRequired,
                "Bạn cần đăng nhập để quản lý nhân sự phòng ban.", 401);

        // 2. The JWT claims a Department Leader. Cheap pre-filter only — the database is the authority
        //    below, because a token minted before a leadership transfer still carries LEADER.
        var claimsLookRight = _currentUser.RoleCode == RoleCodes.Department
            && _currentUser.SubRole == UserSubRoles.Leader;
        if (!claimsLookRight)
            throw new AuthBusinessException(
                DepartmentLeaderErrorCodes.DepartmentLeaderRequired,
                "Chỉ Trưởng phòng ban mới được quản lý nhân sự phòng ban.", 403);

        // 3. Re-read the actor. Role/sub-role/status/department all come from the row, not the token.
        var actor = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == actorId)
            .Select(u => new
            {
                u.UserId,
                RoleCode = u.Role.RoleCode,
                u.SubRole,
                u.Status,
                u.DepartmentId,
                u.PrimaryCampusId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null
            || actor.RoleCode != RoleCodes.Department
            || actor.SubRole != UserSubRoles.Leader)
            throw new AuthBusinessException(
                DepartmentLeaderErrorCodes.DepartmentLeaderRequired,
                "Chỉ Trưởng phòng ban mới được quản lý nhân sự phòng ban.", 403);

        if (actor.Status != UserStatuses.Active)
            throw new AuthBusinessException(
                DepartmentLeaderErrorCodes.DepartmentLeaderRequired,
                "Tài khoản của bạn không ở trạng thái hoạt động.", 403);

        // 4. Department context must exist on the account itself.
        if (actor.DepartmentId is not { } departmentId)
            throw new AuthBusinessException(
                DepartmentLeaderErrorCodes.DepartmentContextMissing,
                "Tài khoản của bạn chưa được gán phòng ban.", 422);

        var department = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == departmentId)
            .Select(d => new
            {
                d.DepartmentId,
                d.Name,
                d.DepartmentType,
                d.Status,
                d.HeadUserId,
                d.CampusId,
                CampusName = d.Campus.Name,
                CampusStatus = d.Campus.Status,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (department is null)
            throw new AuthBusinessException(
                DepartmentLeaderErrorCodes.DepartmentContextMissing,
                "Không tìm thấy phòng ban của bạn.", 422);

        // 5. Only a GENERAL department is managed here — the IC department belongs to the Staff Leader
        //    flow and has different membership rules.
        if (!string.Equals(department.DepartmentType, DepartmentTypes.General, System.StringComparison.OrdinalIgnoreCase))
            throw new AuthBusinessException(
                DepartmentLeaderErrorCodes.DepartmentScopeForbidden,
                "Chức năng này chỉ áp dụng cho phòng ban thường.", 403);

        // 6. THE check the JWT cannot make: the caller must still be the seated head. A demoted leader
        //    holding a valid token is refused here (spec §4).
        if (department.HeadUserId != actorId)
            throw new AuthBusinessException(
                DepartmentLeaderErrorCodes.DepartmentScopeForbidden,
                "Bạn không còn là Trưởng phòng của phòng ban này.", 403);

        if (department.Status != EntityStatuses.Active)
            throw new AuthBusinessException(
                DepartmentLeaderErrorCodes.DepartmentNotActive,
                "Phòng ban của bạn đã ngừng hoạt động.", 422);

        if (department.CampusStatus != EntityStatuses.Active)
            throw new AuthBusinessException(
                DepartmentLeaderErrorCodes.DepartmentNotActive,
                "Cơ sở của phòng ban đã ngừng hoạt động.", 422);

        return new DepartmentLeaderScope
        {
            ActorUserId = actorId,
            DepartmentId = department.DepartmentId,
            CampusId = department.CampusId,
            DepartmentName = department.Name,
            CampusName = department.CampusName,
        };
    }

    public async Task<Department> ResolveCurrentDepartmentAsync(CancellationToken cancellationToken)
    {
        var scope = await EnsureCurrentUserIsActualDepartmentLeaderAsync(cancellationToken);

        return await _db.Departments
            .Include(d => d.Campus)
            .FirstOrDefaultAsync(d => d.DepartmentId == scope.DepartmentId, cancellationToken)
            ?? throw new AuthBusinessException(
                DepartmentLeaderErrorCodes.DepartmentContextMissing,
                "Không tìm thấy phòng ban của bạn.", 422);
    }

    public async Task<User> GetScopedPersonnelAsync(
        DepartmentLeaderScope scope, ulong userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null || !IsInScope(scope, user))
            throw NotFoundInScope();

        return user;
    }

    public async Task EnsureTargetBelongsToCurrentDepartmentAsync(
        DepartmentLeaderScope scope, ulong userId, CancellationToken cancellationToken)
    {
        var belongs = await _db.Users.AsNoTracking()
            .AnyAsync(
                u => u.UserId == userId
                     && u.DepartmentId == scope.DepartmentId
                     && u.Role.RoleCode == RoleCodes.Department,
                cancellationToken);

        if (!belongs) throw NotFoundInScope();
    }

    /// <summary>
    /// Membership test applied to every target: a DEPARTMENT account sitting in the caller's own
    /// department. Campus is implied (the department owns exactly one campus) but is checked anyway so
    /// a mis-provisioned row cannot slip through a campus-scoped operation.
    /// </summary>
    private static bool IsInScope(DepartmentLeaderScope scope, User user)
        => user.Role?.RoleCode == RoleCodes.Department
           && user.DepartmentId == scope.DepartmentId
           && (user.PrimaryCampusId is null || user.PrimaryCampusId == scope.CampusId);

    /// <summary>
    /// One response for "no such user" and "user in another department" alike — the caller learns
    /// nothing about accounts outside their scope.
    /// </summary>
    private static AuthBusinessException NotFoundInScope() => new(
        DepartmentLeaderErrorCodes.PersonnelNotFound,
        "Không tìm thấy nhân sự trong phòng ban của bạn.", 404);
}
