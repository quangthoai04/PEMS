namespace PEMS.Application.Dashboard.Queries.GetHODashboardOverview;

public class HODashboardOverviewDto
{
    public HOKpisDto Kpis { get; set; } = new();
    public List<HOActionItemDto> ActionItems { get; set; } = new();
    public List<HOPendingRequestDto> PendingRequests { get; set; } = new();
    public List<HOUpcomingVisitDto> UpcomingVisits { get; set; } = new();
    public List<HOCampusStatusDto> CampusStatus { get; set; } = new();
    public List<HORecentActivityDto> RecentActivities { get; set; } = new();
}

public class HOKpisDto
{
    public int PendingRequests { get; set; }
    public int OverdueRequests { get; set; }
    public int UpcomingVisits { get; set; }
    public int LowFeedback { get; set; }
}

public class HOActionItemDto
{
    public string Title { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
}

public class HOPendingRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
}

public class HOUpcomingVisitDto
{
    public string Name { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
}

public class HOCampusStatusDto
{
    public string Name { get; set; } = string.Empty;
    public int Processing { get; set; }
    public int Upcoming { get; set; }
    public int Alerts { get; set; }
}

public class HORecentActivityDto
{
    public string Content { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}
