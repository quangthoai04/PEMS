using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Roles.Commands.UpdateRoleDetails;

public sealed class UpdateRoleDetailsCommandHandler : IRequestHandler<UpdateRoleDetailsCommand, UpdateRoleDetailsResponse>
{
    public Task<UpdateRoleDetailsResponse> Handle(UpdateRoleDetailsCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Update Role Details has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}