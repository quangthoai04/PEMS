using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.PublicContent.Queries.ViewNotifications;

public sealed class ViewNotificationsQueryHandler : IRequestHandler<ViewNotificationsQuery, ViewNotificationsDto>
{
    public Task<ViewNotificationsDto> Handle(ViewNotificationsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Notifications has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}