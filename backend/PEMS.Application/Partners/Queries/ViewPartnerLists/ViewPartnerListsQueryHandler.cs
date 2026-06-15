using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Partners.Queries.ViewPartnerLists;

public sealed class ViewPartnerListsQueryHandler : IRequestHandler<ViewPartnerListsQuery, ViewPartnerListsDto>
{
    public Task<ViewPartnerListsDto> Handle(ViewPartnerListsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Partner Lists has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}