using MediatR;

namespace PEMS.Application.Accounts.Queries.RelatedVisitors;

/// <summary>
/// Staff Leader "Related Visitor Accounts" tab — the nationality dropdown options.
///
/// Deliberately parameterless: the campus scope is resolved from the authenticated Staff Leader
/// (never from the client), and the options must cover EVERY related Visitor, so there is no
/// paging, keyword or status input to narrow the set with.
/// </summary>
public sealed class GetRelatedVisitorNationalitiesQuery : IRequest<RelatedVisitorNationalitiesDto>
{
}
