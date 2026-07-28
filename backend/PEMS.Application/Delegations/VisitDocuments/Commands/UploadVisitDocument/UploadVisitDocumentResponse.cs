namespace PEMS.Application.Delegations.VisitDocuments.Commands.UploadVisitDocument;

public sealed class UploadedVisitDocumentPartnerDto
{
    public ulong DocumentId { get; init; }
    public ulong PartnerId { get; init; }
    public string PartnerName { get; init; } = string.Empty;
}

public sealed class UploadVisitDocumentResponse
{
    public ulong FileId { get; init; }
    public string FileName { get; init; } = string.Empty;

    /// <summary>Authorized proxy URL (<c>/api/files/{fileId}/content</c>) — no Drive id exposed.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>One entry per partner the document got tagged to; empty when saved as VISIT.</summary>
    public IReadOnlyList<UploadedVisitDocumentPartnerDto> Partners { get; init; } = Array.Empty<UploadedVisitDocumentPartnerDto>();

    /// <summary>Set only when <see cref="Partners"/> is empty (OwnerType=VISIT, "Theo đoàn khách").</summary>
    public ulong? VisitDocumentId { get; init; }
}
