using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Partners.Queries.GetPublicPartnerTypes;

/// <summary>
/// GET /api/public/partners/types — distinct partner_type values (with counts) among APPROVED +
/// PUBLIC partners, for the public directory's partner type filter. Anonymous.
/// </summary>
public sealed record GetPublicPartnerTypesQuery(string? LanguageCode = null)
    : IRequest<List<PublicPartnerTypeDto>>;

public sealed class PublicPartnerTypeDto
{
    /// <summary>The exact <c>partners.partner_type</c> enum value — pass this back as the filter's <c>partnerType</c>.</summary>
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}
