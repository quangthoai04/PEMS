using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Commands.ManageDepartmentStatus;

public sealed class ManageDepartmentStatusCommandHandler : IRequestHandler<ManageDepartmentStatusCommand, ManageDepartmentStatusResponse>
{
    public Task<ManageDepartmentStatusResponse> Handle(ManageDepartmentStatusCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Manage Department Status has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}