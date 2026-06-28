using MediatR;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Queries.ViewGalleryItemDetails;

/// <summary>UC-GAL-03 View Detail Gallery (Staff Leader). Campus scope enforced in the handler.</summary>
public sealed class ViewGalleryItemDetailsQuery : IRequest<GalleryItemDetailDto>
{
    public long GalleryItemId { get; set; }
}
