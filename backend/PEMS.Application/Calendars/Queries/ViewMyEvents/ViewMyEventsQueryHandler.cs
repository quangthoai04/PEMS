using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Calendars.Queries.ViewMyEvents;

public sealed class ViewMyEventsQueryHandler : IRequestHandler<ViewMyEventsQuery, ViewMyEventsDto>
{
    public Task<ViewMyEventsDto> Handle(ViewMyEventsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View My Events has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}