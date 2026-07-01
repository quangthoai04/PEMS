using MediatR;

namespace PEMS.Application.PublicContent.Queries.ViewPublicNewsDetail;

public sealed class ViewPublicNewsDetailQuery : IRequest<PublicNewsDetailDto>
{
    public ulong NewsId { get; init; }

    public ViewPublicNewsDetailQuery(ulong newsId)
    {
        NewsId = newsId;
    }
}
