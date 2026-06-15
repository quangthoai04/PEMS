using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Queries.ViewMeetingMinutesDetails;

public sealed class ViewMeetingMinutesDetailsQueryHandler : IRequestHandler<ViewMeetingMinutesDetailsQuery, ViewMeetingMinutesDetailsDto>
{
    public Task<ViewMeetingMinutesDetailsDto> Handle(ViewMeetingMinutesDetailsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Meeting Minutes Details has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}