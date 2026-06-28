using MediatR;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicCampuses;

/// <summary>
/// UC §7.1 — lists ACTIVE campuses for the public VisitFPTU campus picker (BR-PGAL-03). Anonymous.
/// </summary>
public sealed class GetPublicCampusesQuery : IRequest<PublicCampusListDto>
{
}
