using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Partners.Queries.GetPublicPartnerCountries;

/// <summary>
/// GET /api/public/partners/countries — distinct countries among APPROVED + PUBLIC partners, for the
/// public directory's country filter dropdown (and to validate/normalize whatever string
/// GlobeComponent's pin click emits before it's used as a filter value). Anonymous.
/// </summary>
public sealed class GetPublicPartnerCountriesQuery : IRequest<List<PublicPartnerCountryDto>>
{
}

public sealed class PublicPartnerCountryDto
{
    /// <summary>The exact <c>partners.country</c> value — pass this back as the filter's <c>country</c>.</summary>
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}
