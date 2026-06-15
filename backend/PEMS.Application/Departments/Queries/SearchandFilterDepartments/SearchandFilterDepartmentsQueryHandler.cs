using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Queries.SearchandFilterDepartments;

public sealed class SearchandFilterDepartmentsQueryHandler : IRequestHandler<SearchandFilterDepartmentsQuery, SearchandFilterDepartmentsDto>
{
    public Task<SearchandFilterDepartmentsDto> Handle(SearchandFilterDepartmentsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search and Filter Departments has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}