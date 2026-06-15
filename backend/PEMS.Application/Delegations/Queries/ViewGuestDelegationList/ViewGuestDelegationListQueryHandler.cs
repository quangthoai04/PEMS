using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

public sealed class ViewGuestDelegationListQueryHandler : IRequestHandler<ViewGuestDelegationListQuery, ViewGuestDelegationListDto>
{
    public Task<ViewGuestDelegationListDto> Handle(ViewGuestDelegationListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Guest Delegation List has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}