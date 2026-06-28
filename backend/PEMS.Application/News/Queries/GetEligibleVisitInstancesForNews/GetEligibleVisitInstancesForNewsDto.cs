namespace PEMS.Application.News.Queries.GetEligibleVisitInstancesForNews;

public sealed class GetEligibleVisitInstancesResponse
{
    public IReadOnlyList<EligibleVisitInstanceDto> Items { get; init; } = Array.Empty<EligibleVisitInstanceDto>();
}

public sealed class EligibleVisitInstanceDto
{
    public ulong VisitInstanceId { get; init; }
    public string VisitTitle { get; init; } = string.Empty;
    public string CampusName { get; init; } = string.Empty;
    public DateTime PlannedStartAt { get; init; }
    public DateTime PlannedEndAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool HasNews { get; init; }
    public bool CanSelect { get; init; }
}
