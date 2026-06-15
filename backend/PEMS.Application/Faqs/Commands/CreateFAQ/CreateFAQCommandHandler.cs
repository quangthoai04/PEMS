using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Faqs.Commands.CreateFAQ;

public sealed class CreateFAQCommandHandler : IRequestHandler<CreateFAQCommand, CreateFAQResponse>
{
    public Task<CreateFAQResponse> Handle(CreateFAQCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Create FAQ has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}