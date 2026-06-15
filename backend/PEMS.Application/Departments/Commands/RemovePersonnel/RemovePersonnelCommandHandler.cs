using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Commands.RemovePersonnel;

public sealed class RemovePersonnelCommandHandler : IRequestHandler<RemovePersonnelCommand, RemovePersonnelResponse>
{
    public Task<RemovePersonnelResponse> Handle(RemovePersonnelCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Remove Personnel has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}