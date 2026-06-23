namespace PEMS.Application.Accounts.Queries.ViewAccountStatistics;

public sealed class ViewAccountStatisticsDto
{
    public int TotalAccounts { get; init; }
    public int ActiveAccounts { get; init; }
    public int LockedAccounts { get; init; }
    public int InactiveAccounts { get; init; }
}
