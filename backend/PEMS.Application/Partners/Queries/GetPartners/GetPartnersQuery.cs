using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPartners;

/// <summary>GET /api/partners — role-scoped, filtered, paginated partner list.</summary>
public sealed class GetPartnersQuery : IRequest<PartnerListResponse>
{
    /// <summary>Search by partner code / name / short name.</summary>
    public string? Search { get; set; }
    public string? Country { get; set; }
    public ulong? CampusId { get; set; }
    public string? ProfileStatus { get; set; }
    public string? Visibility { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
