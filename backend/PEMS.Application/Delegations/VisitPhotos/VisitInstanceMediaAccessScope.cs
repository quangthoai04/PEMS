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
/// Authorizes access to a đoàn's (visit instance's) shared photo folder/gallery for both the
/// Student visit-photo feature AND composing a News post for that instance — i.e. "Host or ACCEPTED
/// participant" (Staff regular or Student), the same rule <c>CreateNewsCommandHandler</c> already
/// enforces when actually creating the News row. Deliberately broader than the Student-only
/// <see cref="VisitPhotoStudentScope"/>, since News authors also include Staff and both features
/// need to see/reuse the same underlying <c>VR-{visit_request_id}/{campus_code}</c> Drive folder.
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

        var roleCode = currentUser.RoleCode ?? string.Empty;
        var subRole  = currentUser.SubRole  ?? string.Empty;
        var isAllowedRole = (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff)
                          || roleCode == RoleCodes.Student;
        if (!isAllowedRole)
            throw new ForbiddenException("Chỉ Staff thường và Student mới được truy cập ảnh đoàn.");

        var instance = await db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == visitInstanceId, cancellationToken)
            ?? throw new NotFoundException("Chuyến tiếp khách", visitInstanceId);

        var isHost = instance.CurrentHostUserId == userId;
        var isParticipant = isHost || await db.VisitParticipants
            .AnyAsync(vp =>
                vp.VisitInstanceId == visitInstanceId &&
                vp.UserId == userId &&
                vp.Status == ParticipantStatuses.Accepted,
                cancellationToken);

        if (!isParticipant)
            throw new ForbiddenException(
                "Bạn chỉ có thể truy cập ảnh của chuyến tiếp khách mà bạn là Host hoặc đã xác nhận tham gia.");

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
