using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Infrastructure.Identity;

/// <summary>
/// Data-scope checks for command/query handlers. HO and ADMIN are treated as
/// system-wide; STAFF/STUDENT are scoped to their primary campus; DEPT is scoped
/// to its department; everyone else only to their own records.
/// </summary>
public sealed class OwnershipChecker : IOwnershipChecker
{
    private readonly ICurrentUserService _currentUser;

    public OwnershipChecker(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    private bool IsSystemWide =>
        _currentUser.RoleCode is RoleCodes.Admin or RoleCodes.Ho;

    public bool IsOwner(ulong? resourceOwnerUserId)
    {
        if (resourceOwnerUserId is null || !_currentUser.IsAuthenticated)
            return false;

        return resourceOwnerUserId == _currentUser.UserId;
    }

    public bool CanAccessCampus(ulong? campusId)
    {
        if (!_currentUser.IsAuthenticated)
            return false;

        if (IsSystemWide)
            return true;

        if (campusId is null)
            return false;

        return campusId == _currentUser.PrimaryCampusId;
    }

    public bool CanAccessDepartment(ulong? departmentId)
    {
        if (!_currentUser.IsAuthenticated)
            return false;

        if (IsSystemWide)
            return true;

        if (departmentId is null)
            return false;

        return departmentId == _currentUser.DepartmentId;
    }
}
