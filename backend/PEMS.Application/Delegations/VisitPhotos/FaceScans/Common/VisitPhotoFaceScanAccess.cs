using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;

/// <summary>
/// SEC-16 remediation. Backend is the single source of authority for face-scan/tagging: the
/// frontend button visibility must never be treated as the access control. <c>ResolveStaffAsync</c>
/// used to be a pure pass-through to the broad, view-only <see cref="VisitInstanceMediaAccessScope"/>
/// (Host, ANY Staff Leader, or an ACCEPTED/ASSIGNED participant) — nowhere near the chốt business
/// rule this method's own name promises.
///
/// Chốt business rule: Face Scan = Staff or Staff Leader role AND must be the Host of that EXACT
/// visit instance. Both conditions are required — role alone or Host status alone is insufficient.
/// The role check uses <see cref="EffectiveRole.Resolve"/> in a fail-closed try/catch: an
/// unresolvable (role_code, sub_role) combination denies, never crashes or defaults to allow.
/// </summary>
public static class VisitPhotoFaceScanAccess
{
    public static async Task<VisitInstanceMediaAccessContext> ResolveStaffAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ulong visitInstanceId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            throw new ForbiddenException("Bạn chưa đăng nhập.");

        string effectiveRole;
        try
        {
            effectiveRole = EffectiveRole.Resolve(currentUser.RoleCode ?? string.Empty, currentUser.SubRole);
        }
        catch (InvalidOperationException)
        {
            throw new ForbiddenException(
                "Chỉ Staff hoặc Staff Leader là Host của chuyến tiếp khách này mới được quét/gắn thẻ khuôn mặt.");
        }

        if (effectiveRole != EffectiveRole.Staff && effectiveRole != EffectiveRole.StaffLeader)
            throw new ForbiddenException(
                "Chỉ Staff hoặc Staff Leader là Host của chuyến tiếp khách này mới được quét/gắn thẻ khuôn mặt.");

        var instance = await db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == visitInstanceId, cancellationToken)
            ?? throw new NotFoundException("Chuyến tiếp khách", visitInstanceId);

        if (instance.CurrentHostUserId != userId)
            throw new ForbiddenException(
                "Chỉ Staff hoặc Staff Leader là Host của chuyến tiếp khách này mới được quét/gắn thẻ khuôn mặt.");

        var campusCode = await db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.CampusCode)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Campus", instance.CampusId);

        return new VisitInstanceMediaAccessContext
        {
            UserId = userId,
            Instance = instance,
            CampusCode = campusCode,
        };
    }
}
