using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;

/// <summary>
/// Backend is the single source of authority for face-scan/tagging: the frontend button visibility
/// (Staff-only "Sau tiếp khách" section) must never be treated as the access control. Every
/// endpoint re-checks the caller here — no permission is expanded beyond what already governs the
/// visit_photos resource itself (<see cref="VisitInstanceMediaAccessScope"/>): Host or ACCEPTED
/// participant, and only regular Staff (not Student, not Admin/StaffLeader/HO) perform the scan/tag
/// action, matching the spec's "Staff chọn thủ công khách". Admin's role stays limited to managing
/// api_configurations — it is not implicitly granted tagging rights.
/// </summary>
public static class VisitPhotoFaceScanAccess
{
    public static async Task<VisitInstanceMediaAccessContext> ResolveStaffAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ulong visitInstanceId,
        CancellationToken cancellationToken)
    {
        var scope = await VisitInstanceMediaAccessScope.ResolveAsync(db, currentUser, visitInstanceId, cancellationToken);

        if (currentUser.RoleCode != RoleCodes.Staff)
            throw new ForbiddenException(
                "Chỉ Staff phụ trách chuyến thăm mới được quét và gán tên khuôn mặt.");

        return scope;
    }
}
