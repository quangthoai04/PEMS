using MediatR;

namespace PEMS.Application.Files.Queries.GetFileContent;

/// <summary>
/// Proxies a stored file's binary back to the caller. Used by <c>GET /api/files/{fileId}/content</c>
/// so the frontend never talks to the storage provider directly and <c>users.avatar_url</c> can stay
/// a stable backend path.
/// </summary>
public sealed record GetFileContentQuery(long FileId) : IRequest<FileContentResult>;
