using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.VisitPhotos;

/// <summary>
/// Resolved authorization context for the visit-photo upload feature: the caller is an ACTIVE,
/// non-Admin user who is the instance's Host or holds an ACCEPTED participation in the exact campus
/// instance.
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
    /// SEC-14 (chốt business rule): Photo Upload = Host OR an ACCEPTED-status participant of that
    /// EXACT visit instance — nothing else. ASSIGNED is excluded on purpose: that participant has not
    /// yet accepted, so they may not upload. The former blanket "role ADMIN or STAFF" bypass is
    /// removed entirely, and ADMIN is now explicitly, unconditionally denied FIRST — before any
    /// relationship check — consistent with <c>IRoleAccessPolicy.CanAccessVisitManagement</c>
    /// excluding ADMIN from the whole Visit/Delegation domain; an account that is ADMIN today but was
    /// once recorded as this instance's Host must not pass through that historical relationship.
    ///
    /// This must stay byte-for-byte equivalent to the database guard
    /// <c>trg_visit_photos_validate_bi</c> — a caller the application approves but the trigger
    /// rejects (or the reverse) surfaces as a raw 500 (SIGNAL 45000) instead of a clean 403, or as a
    /// silent over-permission the app-layer fix alone would not close. Keep both layers in sync.
    /// </summary>
    public static async Task<VisitPhotoStudentContext> ResolveAcceptedStudentAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ulong visitInstanceId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        if (currentUser.RoleCode == RoleCodes.Admin)
            throw new ForbiddenException(
                "Bạn chỉ có thể tải ảnh cho chuyến tiếp khách mà bạn là Host hoặc đã xác nhận tham gia.");

        var instance = await db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == visitInstanceId, cancellationToken)
            ?? throw new NotFoundException("Chuyến tiếp khách", visitInstanceId);

        var isActiveAccount = await db.Users
            .AnyAsync(u => u.UserId == userId && u.Status == "ACTIVE", cancellationToken);
        if (!isActiveAccount)
            throw new ForbiddenException("Tài khoản không hợp lệ hoặc đã bị khóa.");

        var isHost = instance.CurrentHostUserId == userId;

        if (!isHost)
        {
            var hasAcceptedParticipation = await db.VisitParticipants
                .AnyAsync(vp =>
                    vp.VisitInstanceId == visitInstanceId &&
                    vp.UserId == userId &&
                    vp.Status == ParticipantStatuses.Accepted,
                    cancellationToken);
            if (!hasAcceptedParticipation)
                throw new ForbiddenException(
                    "Bạn chỉ có thể tải ảnh cho chuyến tiếp khách mà bạn là Host hoặc đã xác nhận tham gia.");
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
