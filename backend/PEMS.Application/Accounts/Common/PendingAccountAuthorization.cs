using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Authorization for managing a still-pending account's email confirmation (resend / edit email). Only an
/// HO (any campus) or the Staff Leader of the account's OWN campus may act; anyone else — including a
/// campus-mismatched leader — is refused with 403.
/// </summary>
public static class PendingAccountAuthorization
{
    public static void EnsureCanManagePending(ICurrentUserService actor, User account)
    {
        if (actor.UserId is null || !actor.IsAuthenticated)
            throw new ForbiddenException("Bạn cần đăng nhập để thực hiện thao tác này.");

        var isHo = actor.RoleCode == RoleCodes.Ho;
        var isCampusLeader = actor.RoleCode == RoleCodes.Staff
            && actor.SubRole == UserSubRoles.Leader
            && actor.PrimaryCampusId == account.PrimaryCampusId;

        if (!isHo && !isCampusLeader)
            throw new ForbiddenException("Bạn không có quyền quản lý tài khoản này.");
    }
}
