using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Queries.ViewCoordinationTasks;

public sealed class ViewCoordinationTasksQueryHandler : IRequestHandler<ViewCoordinationTasksQuery, ViewCoordinationTasksDto>
{
    public Task<ViewCoordinationTasksDto> Handle(ViewCoordinationTasksQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Coordination Tasks has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}