using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.VisitPhotos;

/// <summary>The Drive target an upload lands in: the request's folder row + the campus subfolder id.</summary>
public sealed class VisitPhotoUploadTarget
{
    public VisitPhotoFolder Folder { get; init; } = null!;

    /// <summary>Drive id of the <c>{campus_code}</c> subfolder below <c>VR-{visit_request_id}</c>.</summary>
    public string CampusFolderExternalId { get; init; } = null!;
}

/// <summary>
/// Provisions the mandated Drive structure for visit photos —
/// <c>VisitRequestPhotoFolderId / VR-{visit_request_id} / {campus_code}</c> — and keeps
/// <c>visit_photo_folders</c> at exactly one row per request, even under concurrent first uploads.
/// </summary>
public interface IVisitPhotoFolderService
{
    Task<VisitPhotoUploadTarget> EnsureUploadTargetAsync(
        VisitRequestCampus instance, string campusCode, ulong userId, CancellationToken cancellationToken);
}
