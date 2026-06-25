using MediatR;

namespace PEMS.Application.Accounts.Queries.RelatedVisitors;

/// <summary>
/// Staff Leader "Related Visitor Accounts" tab — detail of a single Visitor. The handler
/// re-checks (BR-03) that the Visitor is related to the caller's campus via at least one visible
/// request; an out-of-scope id is reported as Not Found so the account's existence is not leaked.
/// </summary>
public sealed class GetRelatedVisitorDetailsQuery : IRequest<RelatedVisitorAccountDetailDto>
{
    /// <summary>Target Visitor account id.</summary>
    public ulong VisitorUserId { get; set; }
}
