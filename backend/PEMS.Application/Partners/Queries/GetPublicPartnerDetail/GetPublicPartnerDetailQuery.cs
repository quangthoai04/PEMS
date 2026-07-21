using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPublicPartnerDetail;

/// <summary>GET /api/public/partners/{partnerIdOrSlug} — APPROVED + PUBLIC only. Anonymous.</summary>
public sealed record GetPublicPartnerDetailQuery(
    string PartnerIdOrSlug, string? LanguageCode = null) : IRequest<PublicPartnerDto>;
