using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Queries.ViewPersonnelDetails;

public sealed class ViewPersonnelDetailsQueryHandler : IRequestHandler<ViewPersonnelDetailsQuery, ViewPersonnelDetailsDto>
{
    public Task<ViewPersonnelDetailsDto> Handle(ViewPersonnelDetailsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Personnel Details has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}