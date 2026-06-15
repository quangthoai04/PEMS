using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.CreatePartnerProfile;

public sealed class CreatePartnerProfileCommandHandler : IRequestHandler<CreatePartnerProfileCommand, CreatePartnerProfileResponse>
{
    public Task<CreatePartnerProfileResponse> Handle(CreatePartnerProfileCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Create Partner Profile has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}