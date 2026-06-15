using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Campuses.Queries.SearchandFilterCampus;

public sealed class SearchandFilterCampusQueryHandler : IRequestHandler<SearchandFilterCampusQuery, SearchandFilterCampusDto>
{
    public Task<SearchandFilterCampusDto> Handle(SearchandFilterCampusQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search and Filter Campus has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}