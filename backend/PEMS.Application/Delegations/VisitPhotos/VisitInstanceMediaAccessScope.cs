using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.VisitPhotos;

public sealed class VisitInstanceMediaAccessContext
{
    public ulong UserId { get; init; }
    public VisitRequestCampus Instance { get; init; } = null!;
    public string CampusCode { get; init; } = null!;

    /// <summary>Uploads/edits are blocked once the request or the instance is cancelled.</summary>
    public bool CanUpload =>
        Instance.Status != PEMS.Shared.VisitInstanceStatus.Cancelled
        && Instance.VisitRequest.Status != VisitRequestStatuses.Cancelled;
}

/// <summary>
/// Authorizes access to a đoàn's (visit instance's) shared photo folder/gallery for VIEWING (folder
/// listing, face-scan, News cover image) — i.e. "Host, Admin/Staff-Leader, or ACCEPTED/ASSIGNED
/// participant", the same rule <c>CreateNewsCommandHandler</c> already enforces when actually
/// creating the News row. <see cref="VisitPhotoStudentScope"/> enforces the separate (now broader)
/// rule for UPLOADING — see that type for details.
/// All relation ids (visit_request_id, campus_code) are resolved server-side from
/// visit_instance_id; nothing the frontend claims is trusted.
/// </summary>
public static class VisitInstanceMediaAccessScope
{
    public static async Task<VisitInstanceMediaAccessContext> ResolveAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ulong visitInstanceId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            throw new ForbiddenException("Bạn chưa đăng nhập.");

        var instance = await db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == visitInstanceId, cancellationToken)
            ?? throw new NotFoundException("Chuyến tiếp khách", visitInstanceId);

        var roleCode = currentUser.RoleCode ?? string.Empty;
        var isHost = instance.CurrentHostUserId == userId;
        var isAdminOrLeader = roleCode == RoleCodes.Admin || (roleCode == RoleCodes.Staff && currentUser.SubRole == UserSubRoles.Leader);
        var isParticipant = isHost || isAdminOrLeader || await db.VisitParticipants
            .AnyAsync(vp =>
                vp.VisitInstanceId == visitInstanceId &&
                vp.UserId == userId &&
                (vp.Status == ParticipantStatuses.Accepted || vp.Status == ParticipantStatuses.Assigned),
                cancellationToken);

        if (!isParticipant)
            throw new ForbiddenException(
                "Bạn chỉ có thể truy cập ảnh của chuyến tiếp khách mà bạn có quyền quản lý hoặc tham gia.");

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
