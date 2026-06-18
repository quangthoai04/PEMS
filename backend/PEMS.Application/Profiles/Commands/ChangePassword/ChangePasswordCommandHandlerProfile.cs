using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Profiles.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, ChangePasswordResponse>
{
    public Task<ChangePasswordResponse> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Change Password has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}