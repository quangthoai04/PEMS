using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Roles.Commands.ConfigureRolePermissions;

public sealed class ConfigureRolePermissionsCommandHandler : IRequestHandler<ConfigureRolePermissionsCommand, ConfigureRolePermissionsResponse>
{
    public Task<ConfigureRolePermissionsResponse> Handle(ConfigureRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Configure Role Permissions has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}