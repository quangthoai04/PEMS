using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.News.Queries.ViewNewsList;

public sealed class ViewNewsListQueryHandler : IRequestHandler<ViewNewsListQuery, ViewNewsListDto>
{
    public Task<ViewNewsListDto> Handle(ViewNewsListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View News List has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}