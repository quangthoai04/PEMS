using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Accounts.Commands.ManageAccountStatus;

public sealed class ManageAccountStatusCommandHandler : IRequestHandler<ManageAccountStatusCommand, ManageAccountStatusResponse>
{
    public Task<ManageAccountStatusResponse> Handle(ManageAccountStatusCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Manage Account Status has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}