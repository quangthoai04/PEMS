using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Authentication.Queries.GetCurrentUserPermissions;

public sealed class GetCurrentUserPermissionsQueryHandler
    : IRequestHandler<GetCurrentUserPermissionsQuery, PermissionsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionChecker _permissionChecker;

    public GetCurrentUserPermissionsQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPermissionChecker permissionChecker)
    {
        _db = db;
        _currentUser = currentUser;
        _permissionChecker = permissionChecker;
    }

    public async Task<PermissionsResponse> Handle(GetCurrentUserPermissionsQuery request, CancellationToken cancellationToken)
    {
        var roleId = _currentUser.RoleId;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;

        if (roleId is null)
        {
            // Fall back to loading the user when claims are incomplete.
            var userId = _currentUser.UserId;
            if (userId is null)
                throw new ForbiddenException();

            var user = await _db.Users.AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
                ?? throw new ForbiddenException();

            roleId = user.RoleId;
            roleCode = user.Role?.RoleCode;
            subRole = user.SubRole;
        }

        if (roleCode == "STAFF" || roleCode == "DEPT")
        {
            if (string.IsNullOrEmpty(subRole))
                throw new ForbiddenException();
        }
        else
        {
            subRole = "NONE";
        }

        var permissions = await _permissionChecker.GetPermissionsForRoleAsync(roleId.Value, subRole, cancellationToken);

        return new PermissionsResponse
        {
            RoleCode = roleCode ?? string.Empty,
            Permissions = permissions
        };
    }
}
