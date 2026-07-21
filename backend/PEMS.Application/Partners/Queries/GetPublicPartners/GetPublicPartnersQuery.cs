using System.Collections.Generic;
using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPublicPartners;

/// <summary>GET /api/public/partners — APPROVED + PUBLIC only. Anonymous.</summary>
public sealed class GetPublicPartnersQuery : IRequest<GetPublicPartnersResponse>
{
    public string? Search { get; set; }
    public string? Country { get; set; }

    /// <summary>One of <see cref="PartnerTypes.All"/>. Ignored if not a recognized value.</summary>
    public string? PartnerType { get; set; }

    /// <summary>name_asc (default) | newest | country.</summary>
    public string? Sort { get; set; }

    /// <summary>Public-site locale ("vi" default, "en"/"ja"/"ko"/"zh-CN"). Descriptions are
    /// Vietnamese-only in the DB; a non-"vi" value triggers on-demand translation (cached).</summary>
    public string? LanguageCode { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public sealed class GetPublicPartnersResponse
{
    public List<PublicPartnerDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
