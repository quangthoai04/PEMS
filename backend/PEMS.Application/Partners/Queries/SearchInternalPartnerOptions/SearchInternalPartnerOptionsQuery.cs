using System.Collections.Generic;
using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.SearchInternalPartnerOptions;

/// <summary>
/// Partner options for the form when an AUTHENTICATED internal user is filling it in.
///
/// <para>Separate from the public query on purpose. Staff filling the same form used to be served the
/// public list — ACTIVE + APPROVED + PUBLIC only — so an organization that exists internally but was
/// never published simply did not come up, and the only way forward was to retype it as free text.
/// That is how a stable partner id gets lost at the source, and why the minutes screen then asks to
/// "create" an organization the system already knows (PART-03).</para>
/// </summary>
public sealed class SearchInternalPartnerOptionsQuery : IRequest<IReadOnlyList<InternalPartnerOptionDto>>
{
    public string? Keyword { get; init; }
    public int Limit { get; init; } = 20;
}

/// <summary>
/// One selectable organization. Carries <see cref="ProfileStatus"/> and the owning campus so a
/// profile still awaiting approval can SAY so in the dropdown rather than passing itself off as an
/// approved partner.
/// </summary>
public sealed class InternalPartnerOptionDto
{
    public ulong PartnerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? Country { get; init; }
    public string? City { get; init; }
    public string PartnerType { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>APPROVED or PENDING_APPROVAL — never DRAFT/REJECTED (<see cref="PartnerProfileStatuses"/>).</summary>
    public string ProfileStatus { get; init; } = string.Empty;
    public ulong OwnerCampusId { get; init; }
    public string? OwnerCampusName { get; init; }
}
