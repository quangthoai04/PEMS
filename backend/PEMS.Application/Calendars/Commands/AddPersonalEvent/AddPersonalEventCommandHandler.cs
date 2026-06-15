using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Calendars.Commands.AddPersonalEvent;

public sealed class AddPersonalEventCommandHandler : IRequestHandler<AddPersonalEventCommand, AddPersonalEventResponse>
{
    public Task<AddPersonalEventResponse> Handle(AddPersonalEventCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Add Personal Event has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}