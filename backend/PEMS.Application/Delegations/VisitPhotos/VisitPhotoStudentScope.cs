using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.VisitPhotos;

/// <summary>
/// Resolved authorization context for the visit-photo upload feature: the caller is an ACTIVE user
/// who is the instance's Host, an ADMIN/STAFF (regular Staff or Staff Leader), or holds an
/// ACCEPTED/ASSIGNED participation in the exact campus instance.
/// </summary>
public sealed class VisitPhotoStudentContext
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
/// The single scope gate every visit-photo upload goes through (anti-IDOR): all relation IDs
/// (visit_request_id, campus_code) are resolved server-side from <c>visit_instance_id</c>; nothing
/// the frontend claims about relations is trusted.
/// </summary>
public static class VisitPhotoStudentScope
{
    /// <summary>
    /// Resolves the instance scope and enforces the uploader rule.
    ///
    /// This must stay byte-for-byte equivalent to the database guard
    /// <c>trg_visit_photos_validate_bi</c>: an ACTIVE user may upload a visit photo when they are the
    /// instance's current Host, hold role ADMIN or STAFF (covers both regular Staff and Staff Leader —
    /// sub_role is not checked), or hold an ACCEPTED/ASSIGNED participation in that exact instance.
    /// Keep both layers in sync — a caller the application approves but the trigger rejects surfaces
    /// as a 500 (SIGNAL 45000) instead of a clean 403.
    /// </summary>
    public static async Task<VisitPhotoStudentContext> ResolveAcceptedStudentAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ulong visitInstanceId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        var instance = await db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == visitInstanceId, cancellationToken)
            ?? throw new NotFoundException("Chuyến tiếp khách", visitInstanceId);

        var isActiveAccount = await db.Users
            .AnyAsync(u => u.UserId == userId && u.Status == "ACTIVE", cancellationToken);
        if (!isActiveAccount)
            throw new ForbiddenException("Tài khoản không hợp lệ hoặc đã bị khóa.");

        var roleCode = currentUser.RoleCode ?? string.Empty;
        var isHost = instance.CurrentHostUserId == userId;
        var isStaffOrAdmin = roleCode == RoleCodes.Admin || roleCode == RoleCodes.Staff;

        if (!isHost && !isStaffOrAdmin)
        {
            var hasAcceptedOrAssignedParticipation = await db.VisitParticipants
                .AnyAsync(vp =>
                    vp.VisitInstanceId == visitInstanceId &&
                    vp.UserId == userId &&
                    (vp.Status == ParticipantStatuses.Accepted || vp.Status == ParticipantStatuses.Assigned),
                    cancellationToken);
            if (!hasAcceptedOrAssignedParticipation)
                throw new ForbiddenException(
                    "Bạn chỉ có thể tải ảnh cho chuyến tiếp khách mà bạn là Host, Staff/Admin, hoặc đã xác nhận/được phân công tham gia.");
        }

        var campusCode = await db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.CampusCode)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Campus", instance.CampusId);

        return new VisitPhotoStudentContext
        {
            UserId = userId,
            Instance = instance,
            CampusCode = campusCode,
        };
    }
}
