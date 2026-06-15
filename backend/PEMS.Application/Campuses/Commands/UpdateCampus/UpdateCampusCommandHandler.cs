using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Campuses.Commands.UpdateCampus;

public sealed class UpdateCampusCommandHandler : IRequestHandler<UpdateCampusCommand, UpdateCampusResponse>
{
    public Task<UpdateCampusResponse> Handle(UpdateCampusCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Update Campus has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}