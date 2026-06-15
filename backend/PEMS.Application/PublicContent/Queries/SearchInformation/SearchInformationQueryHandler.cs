using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.PublicContent.Queries.SearchInformation;

public sealed class SearchInformationQueryHandler : IRequestHandler<SearchInformationQuery, SearchInformationDto>
{
    public Task<SearchInformationDto> Handle(SearchInformationQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search Information has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}