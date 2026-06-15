using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Roles.Commands.CreateNewRole;

public sealed class CreateNewRoleCommandHandler : IRequestHandler<CreateNewRoleCommand, CreateNewRoleResponse>
{
    public Task<CreateNewRoleResponse> Handle(CreateNewRoleCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Create New Role has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}