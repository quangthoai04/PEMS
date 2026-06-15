using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Roles.Commands.DisableAndDeleteRole;

public sealed class DisableAndDeleteRoleCommandHandler : IRequestHandler<DisableAndDeleteRoleCommand, DisableAndDeleteRoleResponse>
{
    public Task<DisableAndDeleteRoleResponse> Handle(DisableAndDeleteRoleCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Disable/Delete Role has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}