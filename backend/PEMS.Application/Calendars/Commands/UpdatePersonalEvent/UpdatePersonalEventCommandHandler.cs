using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Calendars.Commands.UpdatePersonalEvent;

public sealed class UpdatePersonalEventCommandHandler : IRequestHandler<UpdatePersonalEventCommand, UpdatePersonalEventResponse>
{
    public Task<UpdatePersonalEventResponse> Handle(UpdatePersonalEventCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Update Personal Event has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}