using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Commands.AddDepartmentPersonnel;

public sealed class AddDepartmentPersonnelCommandHandler : IRequestHandler<AddDepartmentPersonnelCommand, AddDepartmentPersonnelResponse>
{
    public Task<AddDepartmentPersonnelResponse> Handle(AddDepartmentPersonnelCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Add Department Personnel has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}