using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Single source of truth for the Staff Leader VisitFPTU Gallery scope (UC-GAL-01..07). Every gallery
/// query/command is scoped to the authenticated Staff Leader's <c>primary_campus_id</c> — never a campus
/// chosen by the client. The actor must be STAFF + LEADER with a campus (account ACTIVE is implied by
/// holding a valid session). Any other actor → 403.
/// </summary>
internal static class StaffLeaderGalleryScope
{
    /// <summary>
    /// Validates the caller is a Staff Leader and returns their campus id, or throws 403 (wrong actor)
    /// / 422 (Staff Leader without a campus).
    /// </summary>
    public static ulong EnsureStaffLeaderCampus(ICurrentUserService currentUser)
    {
        var isStaffLeader = currentUser.IsAuthenticated
            && currentUser.RoleCode == RoleCodes.Staff
            && currentUser.SubRole == UserSubRoles.Leader;

        if (!isStaffLeader)
        {
            throw new AuthBusinessException(
                GalleryErrorCodes.GalleryManagementForbidden,
                "Bạn không có quyền quản lý gallery.", 403);
        }

        if (currentUser.PrimaryCampusId is null)
        {
            throw new AuthBusinessException(
                GalleryErrorCodes.NoCampusAssigned,
                "Tài khoản chưa được gán cơ sở nên không thể quản lý gallery.", 422);
        }

        return currentUser.PrimaryCampusId.Value;
    }
}
