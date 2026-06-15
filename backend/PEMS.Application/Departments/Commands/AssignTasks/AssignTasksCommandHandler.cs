using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Commands.AssignTasks;

public sealed class AssignTasksCommandHandler : IRequestHandler<AssignTasksCommand, AssignTasksResponse>
{
    public Task<AssignTasksResponse> Handle(AssignTasksCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Assign Tasks has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}