using MediatR;

namespace PEMS.Application.Delegations.News;

/// <summary>Re-submit a visit-instance news post for review (e.g. after it was REJECTED): status → PENDING_REVIEW.</summary>
public sealed record SubmitVisitInstanceNewsCommand(ulong NewsId) : IRequest<VisitNewsDto>;
