using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Authentication.Commands.LoginviaSSO;

public sealed class LoginviaSSOCommandHandler : IRequestHandler<LoginviaSSOCommand, LoginviaSSOResponse>
{
    public Task<LoginviaSSOResponse> Handle(LoginviaSSOCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Login via SSO has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}