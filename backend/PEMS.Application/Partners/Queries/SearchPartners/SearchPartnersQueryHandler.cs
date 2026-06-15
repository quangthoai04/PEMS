using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Partners.Queries.SearchPartners;

public sealed class SearchPartnersQueryHandler : IRequestHandler<SearchPartnersQuery, SearchPartnersDto>
{
    public Task<SearchPartnersDto> Handle(SearchPartnersQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search Partners has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}