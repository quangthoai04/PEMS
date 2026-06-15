using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationDetails;

public sealed class ViewGuestDelegationDetailsQueryHandler : IRequestHandler<ViewGuestDelegationDetailsQuery, ViewGuestDelegationDetailsDto>
{
    public Task<ViewGuestDelegationDetailsDto> Handle(ViewGuestDelegationDetailsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Guest Delegation Details has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}