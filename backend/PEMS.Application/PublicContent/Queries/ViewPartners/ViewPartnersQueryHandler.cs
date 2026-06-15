using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.PublicContent.Queries.ViewPartners;

public sealed class ViewPartnersQueryHandler : IRequestHandler<ViewPartnersQuery, ViewPartnersDto>
{
    public Task<ViewPartnersDto> Handle(ViewPartnersQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Partners has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}