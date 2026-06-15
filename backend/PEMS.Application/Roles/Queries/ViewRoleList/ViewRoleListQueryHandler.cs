using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Roles.Queries.ViewRoleList;

public sealed class ViewRoleListQueryHandler : IRequestHandler<ViewRoleListQuery, ViewRoleListDto>
{
    public Task<ViewRoleListDto> Handle(ViewRoleListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Role List has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}