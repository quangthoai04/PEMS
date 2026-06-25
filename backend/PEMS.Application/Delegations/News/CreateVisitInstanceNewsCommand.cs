using MediatR;

namespace PEMS.Application.Delegations.News;

/// <summary>
/// Create a news post for a campus instance (UC tin tức). Allowed for the Host or an accepted
/// IC-Staff / Student participant. The post starts in PENDING_REVIEW (submitted for the Host to
/// publish via the existing News review UC). Many posts per instance are allowed.
/// </summary>
public sealed record CreateVisitInstanceNewsCommand(
    ulong VisitInstanceId,
    string Title,
    string? Summary,
    string? Body) : IRequest<VisitNewsDto>;
