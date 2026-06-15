using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Queries.SearchDelegations;

public sealed class SearchDelegationsQueryHandler : IRequestHandler<SearchDelegationsQuery, SearchDelegationsDto>
{
    public Task<SearchDelegationsDto> Handle(SearchDelegationsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search Delegations has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}