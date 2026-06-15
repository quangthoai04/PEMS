using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Faqs.Queries.SearchFAQ;

public sealed class SearchFAQQueryHandler : IRequestHandler<SearchFAQQuery, SearchFAQDto>
{
    public Task<SearchFAQDto> Handle(SearchFAQQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search FAQ has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}