using MediatR;
using PEMS.Application.Files.Queries.GetFileContent;

namespace PEMS.Application.Partners.Queries.GetPublicPartnerMedia;

/// <summary>
/// Anonymous, partner-scoped file proxy — the public equivalent of the authenticated
/// <c>/api/files/{id}/content</c> (which requires a session and therefore cannot serve logos/covers
/// to anonymous visitors of the public partner pages). Streams a file's bytes ONLY when the file id
/// is the LogoFileId or CoverFileId of an APPROVED + PUBLIC partner; otherwise 404, so the proxy can
/// never be used to fetch documents, avatars, or any other internal file by guessing an id.
/// </summary>
public sealed record GetPublicPartnerMediaQuery(ulong FileId) : IRequest<FileContentDto>;
