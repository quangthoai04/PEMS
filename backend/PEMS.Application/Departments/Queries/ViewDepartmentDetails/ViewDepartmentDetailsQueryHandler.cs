using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Queries.ViewDepartmentDetails;

public sealed class ViewDepartmentDetailsQueryHandler : IRequestHandler<ViewDepartmentDetailsQuery, ViewDepartmentDetailsDto>
{
    public Task<ViewDepartmentDetailsDto> Handle(ViewDepartmentDetailsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Department Details has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}