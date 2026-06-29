using MediatR;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicCampusNavigation;

/// <summary>
/// UC §7.2 — area/location navigation tree of public-visible content for one campus (by code).
/// Returns null when the campus does not exist or is not ACTIVE (controller → 404). An ACTIVE campus
/// with no public content returns an empty <c>Areas</c> list so the frontend can show the empty state.
/// </summary>
public sealed record GetPublicCampusNavigationQuery(string CampusCode)
    : IRequest<PublicGalleryNavigationDto?>;
