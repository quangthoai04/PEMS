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

    public bool IsOwner(string? resourceOwnerUserId)
    {
        if (string.IsNullOrEmpty(resourceOwnerUserId) || !_currentUser.IsAuthenticated)
            return false;

        return string.Equals(resourceOwnerUserId, _currentUser.UserId, StringComparison.Ordinal);
    }

    public bool CanAccessCampus(string? campusId)
    {
        if (!_currentUser.IsAuthenticated)
            return false;

        if (IsSystemWide)
            return true;

        if (string.IsNullOrEmpty(campusId))
            return false;

        return string.Equals(campusId, _currentUser.PrimaryCampusId, StringComparison.Ordinal);
    }

    public bool CanAccessDepartment(string? departmentId)
    {
        if (!_currentUser.IsAuthenticated)
            return false;

        if (IsSystemWide)
            return true;

        if (string.IsNullOrEmpty(departmentId))
            return false;

        return string.Equals(departmentId, _currentUser.DepartmentId, StringComparison.Ordinal);
    }
}
