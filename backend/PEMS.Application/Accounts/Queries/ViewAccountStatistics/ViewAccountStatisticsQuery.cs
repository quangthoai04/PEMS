using MediatR;

namespace PEMS.Application.Accounts.Queries.ViewAccountStatistics;

/// <summary>
/// UC-95-SL statistics. Returns account counts scoped exactly like the account list for
/// the calling role (ADMIN: all; HO: HO + Staff Leaders; Staff Leader: own-campus
/// STAFF / DEPARTMENT-LEADER / STUDENT).
/// </summary>
public sealed class ViewAccountStatisticsQuery : IRequest<ViewAccountStatisticsDto>
{
}
