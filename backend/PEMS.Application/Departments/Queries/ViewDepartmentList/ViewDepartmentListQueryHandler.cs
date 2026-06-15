using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Queries.ViewDepartmentList;

public sealed class ViewDepartmentListQueryHandler : IRequestHandler<ViewDepartmentListQuery, ViewDepartmentListDto>
{
    public Task<ViewDepartmentListDto> Handle(ViewDepartmentListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Department List has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}