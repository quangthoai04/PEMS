using System;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// Access rules for the VisitProcess "Cảnh báo & Thông báo" reminder settings. Configuration
/// (write) is the host's job; reading is also allowed for the coordinating Staff Leader and HO.
/// </summary>
public static class VisitReminderAccess
{
    /// <summary>Only the official current host may create/update/cancel reminder settings.</summary>
    public static bool CanConfigure(ICurrentUserService user, VisitRequestCampus instance)
        => user.UserId is not null && instance.CurrentHostUserId == user.UserId;

    /// <summary>Host, the campus's Staff Leader, and HO may read the saved schedule.</summary>
    public static bool CanView(ICurrentUserService user, VisitRequestCampus instance)
    {
        if (user.UserId is null) return false;
        if (instance.CurrentHostUserId == user.UserId) return true;
        if (user.RoleCode == RoleCodes.Ho) return true;
        if (user.RoleCode == RoleCodes.Staff
            && string.Equals(user.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
            && user.PrimaryCampusId == instance.CampusId)
            return true;
        return false;
    }
}
