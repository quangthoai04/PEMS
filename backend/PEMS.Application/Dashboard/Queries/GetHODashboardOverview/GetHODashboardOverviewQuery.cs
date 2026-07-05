using MediatR;

namespace PEMS.Application.Dashboard.Queries.GetHODashboardOverview;

public record GetHODashboardOverviewQuery : IRequest<HODashboardOverviewDto>;
