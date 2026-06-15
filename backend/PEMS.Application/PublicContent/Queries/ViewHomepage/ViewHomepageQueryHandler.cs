using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.PublicContent.Queries.ViewHomepage;

public sealed class ViewHomepageQueryHandler : IRequestHandler<ViewHomepageQuery, ViewHomepageDto>
{
    public Task<ViewHomepageDto> Handle(ViewHomepageQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Homepage has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}