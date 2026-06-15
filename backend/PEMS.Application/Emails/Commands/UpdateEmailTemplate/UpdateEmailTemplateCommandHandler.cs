using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Emails.Commands.UpdateEmailTemplate;

public sealed class UpdateEmailTemplateCommandHandler : IRequestHandler<UpdateEmailTemplateCommand, UpdateEmailTemplateResponse>
{
    public Task<UpdateEmailTemplateResponse> Handle(UpdateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Update Email Template has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}