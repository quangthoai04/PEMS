using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Commands.ReviewAssignedTasks;

public sealed class ReviewAssignedTasksCommandHandler : IRequestHandler<ReviewAssignedTasksCommand, ReviewAssignedTasksResponse>
{
    public Task<ReviewAssignedTasksResponse> Handle(ReviewAssignedTasksCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Review Assigned Tasks has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}