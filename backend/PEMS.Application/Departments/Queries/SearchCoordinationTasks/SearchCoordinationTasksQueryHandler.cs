using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Queries.SearchCoordinationTasks;

public sealed class SearchCoordinationTasksQueryHandler : IRequestHandler<SearchCoordinationTasksQuery, SearchCoordinationTasksDto>
{
    public Task<SearchCoordinationTasksDto> Handle(SearchCoordinationTasksQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search Coordination Tasks has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}