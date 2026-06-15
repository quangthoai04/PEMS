using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Faqs.Commands.UpdateFAQ;

public sealed class UpdateFAQCommandHandler : IRequestHandler<UpdateFAQCommand, UpdateFAQResponse>
{
    public Task<UpdateFAQResponse> Handle(UpdateFAQCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Update FAQ has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}