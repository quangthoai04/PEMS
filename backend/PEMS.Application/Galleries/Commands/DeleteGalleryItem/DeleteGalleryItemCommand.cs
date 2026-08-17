using MediatR;

namespace PEMS.Application.Galleries.Commands.DeleteGalleryItem;

/// <summary>
/// "Xóa nội dung Gallery" (Staff Leader). Permanently removes the item from management and public
/// VisitFPTU via SOFT delete — <c>gallery_items.deleted_at/deleted_by</c> — never a physical DELETE and
/// never a status flip (HIDDEN stays a separate, reversible action). Campus scope is resolved from the
/// JWT, so no campus id is accepted from the client.
/// </summary>
public sealed record DeleteGalleryItemCommand(long GalleryItemId) : IRequest<DeleteGalleryItemResponse>;
