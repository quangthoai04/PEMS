using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Authentication.Commands.LoginviaCredentials;

public sealed class LoginviaCredentialsCommandHandler : IRequestHandler<LoginviaCredentialsCommand, LoginviaCredentialsResponse>
{
    public Task<LoginviaCredentialsResponse> Handle(LoginviaCredentialsCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Login via Credentials has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}