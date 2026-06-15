using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Partners.Queries.ViewPartnerDetails;

public sealed class ViewPartnerDetailsQueryHandler : IRequestHandler<ViewPartnerDetailsQuery, ViewPartnerDetailsDto>
{
    public Task<ViewPartnerDetailsDto> Handle(ViewPartnerDetailsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Partner Details has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}