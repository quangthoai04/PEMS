using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Calendars.Queries.ViewDepartmentCalendar;

public sealed class ViewDepartmentCalendarQueryHandler : IRequestHandler<ViewDepartmentCalendarQuery, ViewDepartmentCalendarDto>
{
    public Task<ViewDepartmentCalendarDto> Handle(ViewDepartmentCalendarQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Department Calendar has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}