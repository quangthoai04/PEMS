using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.PublicContent.Queries.ViewNews;

public sealed class ViewNewsQueryHandler : IRequestHandler<ViewNewsQuery, ViewNewsDto>
{
    public Task<ViewNewsDto> Handle(ViewNewsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View News has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}