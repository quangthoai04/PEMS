using MediatR;
using PEMS.Application.Files.Queries.GetFileContent;

namespace PEMS.Application.PublicContent.Queries.GetPublicNewsFile;

/// <summary>
/// Anonymous streaming of an image/file that belongs to a PUBLISHED news article
/// (cover image or a section file). Any other file id is rejected with 404 so this
/// endpoint can never be used to enumerate private uploads.
/// </summary>
public sealed record GetPublicNewsFileQuery(ulong FileId) : IRequest<FileContentDto>;
