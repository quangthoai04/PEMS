using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Emails.Commands.SendEmail;

public sealed class SendEmailCommandHandler : IRequestHandler<SendEmailCommand, SendEmailResponse>
{
    public Task<SendEmailResponse> Handle(SendEmailCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Send Email has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}