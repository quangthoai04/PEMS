using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Authentication.Commands.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, LogoutResponse>
{
    public Task<LogoutResponse> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Logout has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}