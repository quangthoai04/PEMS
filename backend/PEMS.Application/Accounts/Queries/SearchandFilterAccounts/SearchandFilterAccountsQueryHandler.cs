using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Accounts.Queries.SearchandFilterAccounts;

public sealed class SearchandFilterAccountsQueryHandler : IRequestHandler<SearchandFilterAccountsQuery, SearchandFilterAccountsDto>
{
    public Task<SearchandFilterAccountsDto> Handle(SearchandFilterAccountsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search and Filter Accounts has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}