using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.VisitPhotos;

/// <summary>
/// Resolved authorization context for the Student visit-photo feature: the caller is an ACTIVE
/// STUDENT with an ACCEPTED STUDENT participation in the exact campus instance.
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
/// The single scope gate every visit-photo command/query goes through (anti-IDOR): all relation IDs
/// (visit_request_id, campus_code) are resolved server-side from <c>visit_instance_id</c>; nothing
/// the frontend claims about relations is trusted.
/// </summary>
public static class VisitPhotoStudentScope
{
    /// <summary>
    /// Resolves the instance scope and then enforces the STRICT uploader rule.
    ///
    /// This must stay byte-for-byte equivalent to the database guard
    /// <c>trg_visit_photos_validate_bi</c>, which rejects any <c>visit_photos</c> insert whose
    /// <c>uploaded_by</c> is not an ACTIVE user with role STUDENT holding an ACCEPTED STUDENT
    /// participation in that exact instance. Without this check the application layer is weaker than
    /// the database: a Host/Staff/Leader (or an INACTIVE or merely ASSIGNED student) passes
    /// authorization, reaches the INSERT, and the trigger turns it into SIGNAL 45000 — surfacing as a
    /// 500 instead of a clean 403.
    /// </summary>
    public static async Task<VisitPhotoStudentContext> ResolveAcceptedStudentAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ulong visitInstanceId,
        CancellationToken cancellationToken)
    {
        // Shared anti-IDOR resolution: relation ids come from visit_instance_id, never from the client.
        var mediaScope = await VisitInstanceMediaAccessScope.ResolveAsync(
            db, currentUser, visitInstanceId, cancellationToken);

        if ((currentUser.RoleCode ?? string.Empty) != RoleCodes.Student)
            throw new ForbiddenException("Chỉ sinh viên tham gia chuyến tiếp khách mới được tải ảnh lên.");

        var isActiveStudentAccount = await db.Users
            .AnyAsync(u => u.UserId == mediaScope.UserId && u.Status == "ACTIVE", cancellationToken);
        if (!isActiveStudentAccount)
            throw new ForbiddenException("Tài khoản không hợp lệ hoặc đã bị khóa.");

        var hasAcceptedStudentParticipation = await db.VisitParticipants
            .AnyAsync(vp =>
                vp.VisitInstanceId == visitInstanceId &&
                vp.UserId == mediaScope.UserId &&
                vp.ParticipantRole == ParticipantRoles.Student &&
                vp.Status == ParticipantStatuses.Accepted,
                cancellationToken);
        if (!hasAcceptedStudentParticipation)
            throw new ForbiddenException(
                "Bạn chỉ có thể tải ảnh cho chuyến tiếp khách mà bạn đã xác nhận tham gia.");

        return new VisitPhotoStudentContext
        {
            UserId = mediaScope.UserId,
            Instance = mediaScope.Instance,
            CampusCode = mediaScope.CampusCode,
        };
    }
}
