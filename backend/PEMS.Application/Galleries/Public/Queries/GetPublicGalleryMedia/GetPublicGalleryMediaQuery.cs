using MediatR;
using PEMS.Application.Files.Queries.GetFileContent;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicGalleryMedia;

/// <summary>
/// Anonymous, gallery-scoped file proxy (BR-PGAL-13/14). Streams a file's bytes ONLY when the file
/// backs a public-visible gallery media (main file or its thumbnail); otherwise 404. This is what keeps
/// the public page from needing a token while still not exposing arbitrary files like the authenticated
/// <c>/api/files/{id}/content</c> would. Reuses the shared <see cref="FileContentDto"/>.
/// </summary>
public sealed record GetPublicGalleryMediaQuery(ulong FileId) : IRequest<FileContentDto>;
