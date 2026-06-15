using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.MeetingMinutes.Queries.ViewMinutesList;

public sealed class ViewMinutesListQueryHandler : IRequestHandler<ViewMinutesListQuery, ViewMinutesListDto>
{
    public Task<ViewMinutesListDto> Handle(ViewMinutesListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Minutes List has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}