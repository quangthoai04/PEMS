namespace PEMS.Application.News.Queries.GetEligibleVisitInstancesForNews;

public sealed class GetEligibleVisitInstancesResponse
{
    public IReadOnlyList<EligibleVisitInstanceDto> Items { get; init; } = Array.Empty<EligibleVisitInstanceDto>();

    /// <summary>
    /// The verdict for the ONE campus the caller asked about, or null when they asked about none.
    /// Never inferred from <see cref="Items"/>: a campus can be missing from that list for six
    /// different reasons, and the user is entitled to the one that actually applies.
    /// </summary>
    public RequestedVisitInstanceEligibilityDto? Requested { get; init; }
}

/// <summary>
/// One campus, one verdict — from <c>VisitNewsAccess.Evaluate</c>, the same evaluator the process
/// screen, the create command and the list above use.
/// </summary>
public sealed class RequestedVisitInstanceEligibilityDto
{
    public ulong VisitInstanceId { get; init; }

    public bool CanCreate { get; init; }

    /// <summary>
    /// Null when <see cref="CanCreate"/>. Otherwise the stable code the client maps to ONE sentence:
    /// NEWS_VISIT_NOT_IN_SCOPE / NEWS_VISIT_PARTICIPANT_ROLE_NOT_ALLOWED /
    /// NEWS_VISIT_NOT_IN_WRITING_WINDOW / NEWS_VISIT_NOT_REQUIRED / NEWS_VISIT_MEDIA_CONSENT_DENIED /
    /// NEWS_ALREADY_EXISTS_FOR_VISIT_INSTANCE. Match on the code, never on the message.
    /// </summary>
    public string? ReasonCode { get; init; }

    /// <summary>Whether THIS author already has a post for this campus (one post per author per visit).</summary>
    public bool HasNews { get; init; }

    /// <summary>
    /// The author's existing post, so the page can offer "open the post you already wrote" instead of
    /// a dead end. Null unless <see cref="HasNews"/>.
    /// </summary>
    public ulong? ExistingNewsId { get; init; }

    /// <summary>Whether that existing post is still in a state its author may edit (mirrors CreateNews).</summary>
    public bool CanEditExisting { get; init; }
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
