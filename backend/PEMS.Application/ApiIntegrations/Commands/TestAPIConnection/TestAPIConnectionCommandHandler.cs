using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.ApiIntegrations.Commands.TestAPIConnection;

public sealed class TestAPIConnectionCommandHandler : IRequestHandler<TestAPIConnectionCommand, TestAPIConnectionResponse>
{
    public Task<TestAPIConnectionResponse> Handle(TestAPIConnectionCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Test API Connection has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}