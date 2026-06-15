using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Accounts.Queries.ViewAccountList;

public sealed class ViewAccountListQueryHandler : IRequestHandler<ViewAccountListQuery, ViewAccountListDto>
{
    public Task<ViewAccountListDto> Handle(ViewAccountListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Account List has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}