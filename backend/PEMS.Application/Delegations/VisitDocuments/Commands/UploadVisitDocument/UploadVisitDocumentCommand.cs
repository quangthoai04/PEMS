using MediatR;

namespace PEMS.Application.Delegations.VisitDocuments.Commands.UploadVisitDocument;

/// <summary>
/// Host (or Admin/Staff-Leader/ACCEPTED participant — same scope as visit photos/News cover image,
/// see <see cref="VisitPhotos.VisitInstanceMediaAccessScope"/>) uploads one document from the
/// "Tài liệu" section of a visit-in-progress. The backend resolves <c>visit_request_id</c> and
/// <c>campus_code</c> from the instance itself — relation IDs supplied by the frontend are never
/// trusted. <see cref="PartnerNames"/> may be empty: the document then saves as OwnerType=VISIT
/// ("Theo đoàn khách") instead of being tagged to any partner.
/// </summary>
public sealed class UploadVisitDocumentCommand : IRequest<UploadVisitDocumentResponse>
{
    public ulong VisitInstanceId { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 0..N partner display names. Each is resolved to an existing partner (exact-name/alias match)
    /// or created (source MANUAL) — same resolve-or-create behavior the frontend used to do itself
    /// before this endpoint existed. The one uploaded file is tagged to every resolved partner (one
    /// <c>documents</c> row per partner, same <c>file_id</c>).
    /// </summary>
    public List<string> PartnerNames { get; set; } = new();
}
