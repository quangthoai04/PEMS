using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Campuses.Queries.ViewCampusList;

public sealed class ViewCampusListQueryHandler : IRequestHandler<ViewCampusListQuery, ViewCampusListDto>
{
    public Task<ViewCampusListDto> Handle(ViewCampusListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Campus List has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}