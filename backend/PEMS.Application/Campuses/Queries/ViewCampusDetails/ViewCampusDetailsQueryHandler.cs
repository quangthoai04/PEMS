using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Campuses.Queries.ViewCampusDetails;

public sealed class ViewCampusDetailsQueryHandler : IRequestHandler<ViewCampusDetailsQuery, ViewCampusDetailsDto>
{
    public Task<ViewCampusDetailsDto> Handle(ViewCampusDetailsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Campus Details has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}