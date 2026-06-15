using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Commands.AddNewDepartment;

public sealed class AddNewDepartmentCommandHandler : IRequestHandler<AddNewDepartmentCommand, AddNewDepartmentResponse>
{
    public Task<AddNewDepartmentResponse> Handle(AddNewDepartmentCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Add New Department has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}