using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Partners.Commands.EditPartnerInformation;

public sealed class EditPartnerInformationCommandHandler : IRequestHandler<EditPartnerInformationCommand, EditPartnerInformationResponse>
{
    public Task<EditPartnerInformationResponse> Handle(EditPartnerInformationCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Edit Partner Information has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}