using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Calendars.Commands.DeletePersonalEvent;

public sealed class DeletePersonalEventCommandHandler : IRequestHandler<DeletePersonalEventCommand, DeletePersonalEventResponse>
{
    public Task<DeletePersonalEventResponse> Handle(DeletePersonalEventCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Delete Personal Event has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}