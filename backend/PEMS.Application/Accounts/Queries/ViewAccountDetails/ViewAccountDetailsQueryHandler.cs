using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Accounts.Queries.ViewAccountDetails;

public sealed class ViewAccountDetailsQueryHandler : IRequestHandler<ViewAccountDetailsQuery, ViewAccountDetailsDto>
{
    public Task<ViewAccountDetailsDto> Handle(ViewAccountDetailsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Account Details has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}