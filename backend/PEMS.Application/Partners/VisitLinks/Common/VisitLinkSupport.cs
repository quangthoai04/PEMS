using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Partners.VisitLinks.Common;

/// <summary>Access rule for partner-link operations on a visit instance.</summary>
public static class VisitLinkSupport
{
    /// <summary>
    /// Host of the instance, any ACCEPTED/ASSIGNED participant, the campus Staff Leader,
    /// HO and Admin may see/manage partner links of the visit.
    /// </summary>
    public static async Task<VisitRequestCampus> LoadInstanceWithAccessAsync(
        IApplicationDbContext db, ICurrentUserService user, ulong visitInstanceId, CancellationToken ct)
    {
        var instance = await db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == visitInstanceId, ct)
            ?? throw new NotFoundException("VisitRequestCampus", visitInstanceId);

        if (user.UserId is not { } userId)
            throw new ForbiddenException();

        var effective = EffectiveRole.Resolve(user.RoleCode ?? string.Empty, user.SubRole);
        var allowed =
            effective == EffectiveRole.Admin
            || effective == EffectiveRole.Ho
            || instance.CurrentHostUserId == userId
            || (effective == EffectiveRole.StaffLeader && user.PrimaryCampusId == instance.CampusId)
            || await db.VisitParticipants.AnyAsync(
                p => p.VisitInstanceId == visitInstanceId && p.UserId == userId
                     && (p.Status == "ACCEPTED" || p.Status == "ASSIGNED"), ct);

        if (!allowed)
            throw new ForbiddenException("Bạn không có quyền thao tác liên kết đối tác của chuyến thăm này.");

        return instance;
    }
}
