using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Calendars.Queries.ViewEventDetails;

public sealed class ViewEventDetailsQueryHandler : IRequestHandler<ViewEventDetailsQuery, ViewEventDetailsDto>
{
    public Task<ViewEventDetailsDto> Handle(ViewEventDetailsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Event Details has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}