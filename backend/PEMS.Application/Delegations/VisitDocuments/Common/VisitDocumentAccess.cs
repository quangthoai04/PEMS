using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitPhotos;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.VisitDocuments.Common;

/// <summary>
/// SEC-17 remediation. Visit Document upload used to share the broad, view-only
/// <see cref="VisitInstanceMediaAccessScope"/> (Host, ANY Staff Leader — cross-campus, since that
/// scope had its own separate SEC-15 bug — or an ACCEPTED/ASSIGNED participant). Chốt business
/// rule: Visit Document Upload = Host of that EXACT visit instance only. No participant, no Staff
/// Leader, no Admin exception. This is deliberately its own narrow gate rather than a parameter on
/// the shared broad scope, so a future relaxation of viewing rules can never silently widen uploads.
/// </summary>
public static class VisitDocumentAccess
{
    public static async Task<VisitInstanceMediaAccessContext> ResolveUploadAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ulong visitInstanceId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            throw new ForbiddenException("Bạn chưa đăng nhập.");

        // Explicit, unconditional early-deny — consistent with every other Visit-domain helper this
        // remediation touches. ADMIN must never pass through a historical Host relationship either.
        if (currentUser.RoleCode == RoleCodes.Admin)
            throw new ForbiddenException(
                "Chỉ Host của chuyến tiếp khách này mới được tải tài liệu lên.");

        var instance = await db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == visitInstanceId, cancellationToken)
            ?? throw new NotFoundException("Chuyến tiếp khách", visitInstanceId);

        if (instance.CurrentHostUserId != userId)
            throw new ForbiddenException(
                "Chỉ Host của chuyến tiếp khách này mới được tải tài liệu lên.");

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
