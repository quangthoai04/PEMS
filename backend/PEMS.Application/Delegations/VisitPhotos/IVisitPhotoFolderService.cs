using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.VisitPhotos;

/// <summary>The Drive target a photo upload lands in: the delegation folder row + the "Ảnh" subfolder id.</summary>
public sealed class VisitPhotoUploadTarget
{
    public VisitPhotoFolder Folder { get; init; } = null!;

    /// <summary>Drive id of the <c>Ảnh</c> subfolder below <c>{campus_code}</c> below the delegation root.</summary>
    public string PhotoFolderExternalId { get; init; } = null!;
}

/// <summary>The Drive target a Tài liệu (document) upload lands in.</summary>
public sealed class VisitDocumentUploadTarget
{
    public VisitPhotoFolder Folder { get; init; } = null!;

    /// <summary>
    /// Drive id of the resolved document-subtype subfolder (see <see cref="VisitDocumentSubtypes"/>)
    /// below <c>Tài liệu</c> below <c>{campus_code}</c> below the delegation root.
    /// </summary>
    public string DocumentFolderExternalId { get; init; } = null!;
}

/// <summary>
/// The delegation-scoped document subtype subfolders under "Tài liệu". "Báo cáo" (aggregate,
/// multi-delegation reports) intentionally has NO delegation-scoped folder — those archive into the
/// existing flat report folder instead (see <c>Reports/Common/ReportArchiveService</c>).
/// </summary>
public static class VisitDocumentSubtypes
{
    public const string TheoDoanKhach = "Theo đoàn khách";
    public const string DoiTac = "Đối tác";
    public const string HauCan = "Hậu cần";
}

/// <summary>
/// Provisions the mandated Drive structure for a delegation (đoàn khách) —
/// <c>VisitRequestPhotoFolderId / {RequestCode} / {campus_code} / Ảnh</c> for photos and
/// <c>.../{campus_code} / Tài liệu / {subtype}</c> for documents — and keeps
/// <c>visit_photo_folders</c> at exactly one row per request (the shared delegation root), even
/// under concurrent first uploads.
/// </summary>
public interface IVisitPhotoFolderService
{
    Task<VisitPhotoUploadTarget> EnsureUploadTargetAsync(
        VisitRequestCampus instance, string campusCode, ulong userId, CancellationToken cancellationToken);

    Task<VisitDocumentUploadTarget> EnsureDocumentUploadTargetAsync(
        VisitRequestCampus instance, string campusCode, string documentSubtype, ulong userId,
        CancellationToken cancellationToken);
}
