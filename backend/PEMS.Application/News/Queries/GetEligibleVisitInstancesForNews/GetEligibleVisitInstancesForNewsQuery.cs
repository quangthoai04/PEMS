using MediatR;

namespace PEMS.Application.News.Queries.GetEligibleVisitInstancesForNews;

public sealed class GetEligibleVisitInstancesForNewsQuery : IRequest<GetEligibleVisitInstancesResponse>
{
    public bool IncludeAlreadyHasNews { get; init; } = false;
}
