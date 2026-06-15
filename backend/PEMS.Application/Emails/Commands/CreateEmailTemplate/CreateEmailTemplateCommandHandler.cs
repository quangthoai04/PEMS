using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Emails.Commands.CreateEmailTemplate;

public sealed class CreateEmailTemplateCommandHandler : IRequestHandler<CreateEmailTemplateCommand, CreateEmailTemplateResponse>
{
    public Task<CreateEmailTemplateResponse> Handle(CreateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Create Email Template has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}