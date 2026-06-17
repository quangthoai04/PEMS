using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Authentication.Common;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Authentication.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserProfileResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionChecker _permissionChecker;

    public GetCurrentUserQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPermissionChecker permissionChecker)
    {
        _db = db;
        _currentUser = currentUser;
        _permissionChecker = permissionChecker;
    }

    public async Task<UserProfileResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            throw new ForbiddenException();

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.PrimaryCampus)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
            throw new NotFoundException("User", userId);

        var subRole = user.Role?.RoleCode is "STAFF" or "DEPT" ? (user.SubRole ?? "NONE") : "NONE";
        var permissions = await _permissionChecker.GetPermissionsForRoleAsync(user.RoleId, subRole, cancellationToken);

        return new UserProfileResponse
        {
            User = AuthUserMapper.ToDto(user),
            Permissions = permissions
        };
    }
}
