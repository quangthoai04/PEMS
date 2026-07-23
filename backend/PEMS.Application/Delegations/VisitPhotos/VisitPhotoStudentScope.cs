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
        var mediaScope = await VisitInstanceMediaAccessScope.ResolveAsync(
            db, currentUser, visitInstanceId, cancellationToken);

        return new VisitPhotoStudentContext
        {
            UserId = mediaScope.UserId,
            Instance = mediaScope.Instance,
            CampusCode = mediaScope.CampusCode,
        };
    }
}
