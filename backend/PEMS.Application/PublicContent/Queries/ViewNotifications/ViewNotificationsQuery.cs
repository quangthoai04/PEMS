using MediatR;

namespace PEMS.Application.PublicContent.Queries.ViewNotifications;

public class ViewNotificationsQuery : IRequest<ViewNotificationsDto>
{
    public int PageSize { get; init; } = 20;
}
