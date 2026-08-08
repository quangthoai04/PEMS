using MediatR;

namespace PEMS.Application.Partners.Commands.UploadPartnerImage;

public enum PartnerImageKind
{
    Logo,
    Cover,
}

/// <summary>
/// POST /api/partners/{partnerId}/logo-upload or .../cover-upload — uploads a logo/cover image to
/// Google Drive under <c>DocumentPartnerFolderId / {partner_code} / Ảnh {1|2}</c> and returns the
/// fileId to include as <c>logoFileId</c>/<c>coverFileId</c> in the subsequent
/// PUT /api/partners/{partnerId} save (mirrors the News cover-upload flow: upload first, save links
/// the fileId to the entity — see UploadNewsCoverImageCommand).
/// </summary>
public sealed record UploadPartnerImageCommand(
    ulong PartnerId,
    PartnerImageKind Kind,
    byte[] Content,
    string FileName,
    string? ContentType) : IRequest<UploadPartnerImageResponse>;
