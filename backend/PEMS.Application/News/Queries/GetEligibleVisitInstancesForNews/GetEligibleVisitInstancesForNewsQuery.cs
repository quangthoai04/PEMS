using MediatR;

namespace PEMS.Application.News.Queries.GetEligibleVisitInstancesForNews;

public sealed class GetEligibleVisitInstancesForNewsQuery : IRequest<GetEligibleVisitInstancesResponse>
{
    public bool IncludeAlreadyHasNews { get; init; } = false;

    /// <summary>
    /// The ONE campus the caller arrived with (<c>?visitInstanceId=</c> on the Create-News page). When
    /// set, the response additionally carries <see cref="GetEligibleVisitInstancesResponse.Requested"/>
    /// — the canonical verdict for that campus, WITH the reason it is refused.
    ///
    /// <para>
    /// It exists because "absent from the list" is not a reason. The Create-News page used to search
    /// the list for the preset id and, on a miss, print a sentence naming three possible causes at
    /// once — a guess the user cannot act on, and one that disagreed with the process screen whenever
    /// the real cause was a fourth thing. The list answers "which campuses may I write for"; this
    /// answers "why not this one", and both answers come from the same evaluator, so they cannot
    /// disagree.
    /// </para>
    /// </summary>
    public ulong? VisitInstanceId { get; init; }
}
