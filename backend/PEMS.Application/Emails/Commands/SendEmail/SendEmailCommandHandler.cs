using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Emails.Commands.SendEmail;

public sealed class SendEmailCommandHandler : IRequestHandler<SendEmailCommand, SendEmailResponse>
{
    private readonly PEMS.Application.Common.Interfaces.IEmailService _emailService;

    public SendEmailCommandHandler(PEMS.Application.Common.Interfaces.IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task<SendEmailResponse> Handle(SendEmailCommand request, CancellationToken cancellationToken)
    {
        foreach (var recipient in request.To)
        {
            if (!string.IsNullOrWhiteSpace(recipient.Email))
            {
                await _emailService.SendAsync(recipient.Email, request.Subject, request.Body, cancellationToken);
            }
        }
        return new SendEmailResponse();
    }
}