using MediatR;

namespace PEMS.Application.Delegations.News;

/// <summary>List the news posts attached to a campus instance (Visitor sees only published).</summary>
public sealed record GetVisitInstanceNewsQuery(ulong VisitInstanceId) : IRequest<VisitNewsListDto>;
