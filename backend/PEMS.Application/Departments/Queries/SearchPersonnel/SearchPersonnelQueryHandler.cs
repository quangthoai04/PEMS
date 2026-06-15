using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Queries.SearchPersonnel;

public sealed class SearchPersonnelQueryHandler : IRequestHandler<SearchPersonnelQuery, SearchPersonnelDto>
{
    public Task<SearchPersonnelDto> Handle(SearchPersonnelQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search Personnel has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}