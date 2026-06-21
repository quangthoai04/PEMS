using MediatR;
using System.Collections.Generic;

namespace PEMS.Application.Campuses.Queries.GetActiveCampuses;

public sealed class GetActiveCampusesQuery : IRequest<List<ActiveCampusDto>>
{
}
