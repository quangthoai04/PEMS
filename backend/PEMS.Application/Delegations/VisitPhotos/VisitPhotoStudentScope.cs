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
    public static async Task<VisitPhotoStudentContext> ResolveAcceptedStudentAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ulong visitInstanceId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        if (currentUser.RoleCode != RoleCodes.Student)
            throw new ForbiddenException("Chỉ Sinh viên tham gia chuyến thăm mới dùng được chức năng ảnh đoàn khách.");

        // Re-check ACTIVE against the DB — a stale token must not authorize a disabled account.
        var isActiveStudent = await db.Users
            .AnyAsync(u => u.UserId == userId
                           && u.Status == "ACTIVE"
                           && u.Role.RoleCode == RoleCodes.Student,
                cancellationToken);
        if (!isActiveStudent)
            throw new ForbiddenException("Tài khoản không hợp lệ hoặc đã bị khóa.");

        var instance = await db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == visitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", visitInstanceId);

        var isAcceptedStudentParticipant = await db.VisitParticipants
            .AnyAsync(p => p.VisitInstanceId == visitInstanceId
                           && p.UserId == userId
                           && p.ParticipantRole == ParticipantRoles.Student
                           && p.Status == ParticipantStatuses.Accepted,
                cancellationToken);
        if (!isAcceptedStudentParticipant)
            throw new ForbiddenException("Bạn không phải Sinh viên đã nhận lời tham gia chuyến thăm này.");

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
