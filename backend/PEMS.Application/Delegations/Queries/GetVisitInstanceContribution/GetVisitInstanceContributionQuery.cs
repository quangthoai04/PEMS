using MediatR;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceContribution;

/// <summary>
/// Returns the Contribution Page payload (permissions + read-only summary + workspace status) for
/// a campus instance. Access (spec §5.3 / §10.3) is granted only to the current Host, an
/// ACCEPTED/ASSIGNED participant, or a Department user with a real logistics/task relation to the
/// instance; everyone else (Admin, Visitor, unrelated, INVITED/DECLINED/REMOVED) gets 403/404.
/// </summary>
public sealed record GetVisitInstanceContributionQuery(ulong VisitInstanceId)
    : IRequest<ContributionPageDto>;
