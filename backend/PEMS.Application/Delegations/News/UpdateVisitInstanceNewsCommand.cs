using MediatR;

namespace PEMS.Application.Delegations.News;

/// <summary>
/// Edit a visit-instance news post (author or the instance Host) while it is not yet PUBLISHED.
/// Editing re-submits it for review (status → PENDING_REVIEW). RowVersion guards concurrent edits.
/// </summary>
public sealed record UpdateVisitInstanceNewsCommand(
    ulong NewsId,
    string Title,
    string? Summary,
    string? Body,
    int RowVersion) : IRequest<VisitNewsDto>;
