using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Documents.Commands.UploadPartnerDocument;

/// <summary>
/// POST /api/partners/{partnerId}/documents — registers an already-uploaded file
/// (files.file_id from /api/files/upload) as a PARTNER document.
/// </summary>
public sealed class UploadPartnerDocumentCommand : IRequest<PartnerDocumentDto>
{
    public ulong PartnerId { get; set; }
    public ulong FileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DocumentCategory { get; set; }
}
