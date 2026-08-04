using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Commands.SendEmail;

/// <summary>
/// Manual compose, sent directly.
///
/// <para>
/// There is no draft in this path any more, and the compose screen never needed one: the message is held
/// in the browser while it is written and posted whole. What that removes is a class of failure rather
/// than a feature — a composer opened on a draft id that no longer existed used to present a form full of
/// generated text and then, on send, quietly create a NEW draft to send from, so a failed load produced a
/// real email backed by nothing the author had asked for.
/// </para>
/// <para>
/// Every rule the draft path enforced still runs, in <see cref="IDirectEmailSender"/>: content validation
/// and sanitising, the envelope rules, attachment scope re-checked against the database, and a
/// fail-closed refusal if an attachment's bytes cannot be read. Protection against a double click moved
/// from the DRAFT → SENT claim to <c>Idempotency-Key</c>, which this command declares through
/// <c>IIdempotentEmailSend</c>.
/// </para>
/// </summary>
public sealed class SendEmailCommandHandler : IRequestHandler<SendEmailCommand, SendEmailResponse>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDirectEmailSender _sender;

    public SendEmailCommandHandler(ICurrentUserService currentUserService, IDirectEmailSender sender)
    {
        _currentUserService = currentUserService;
        _sender = sender;
    }

    public async Task<SendEmailResponse> Handle(SendEmailCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is not { } userId)
            throw new ForbiddenException();

        var result = await _sender.SendAsync(
            SendEmailRequestMapper.ToDirectRequest(request), userId, cancellationToken);

        return new SendEmailResponse
        {
            SentEmailId = result.SentEmailId,
            Status = result.Status,
            Success = result.Success,
            Message = result.Message,
        };
    }
}

/// <summary>
/// Turns the compose command into the shared send request.
///
/// <para>
/// Shared with the preview handler on purpose: a preview that mapped the payload its own way could show
/// the author a message the send then assembled differently, which is the one thing a preview must not do.
/// </para>
/// </summary>
internal static class SendEmailRequestMapper
{
    public static DirectEmailRequest ToDirectRequest(SendEmailCommand request)
    {
        var recipients = new List<EmailComposeRecipientInput>();
        var order = 0;

        Add(request.To, EmailRecipientTypes.To);
        Add(request.Cc, EmailRecipientTypes.Cc);
        Add(request.Bcc, EmailRecipientTypes.Bcc);

        return new DirectEmailRequest(
            Subject: request.Subject,
            BodyContent: request.Body,
            BodyFormat: request.BodyFormat,
            Recipients: recipients,
            Attachments: request.Attachments,
            // GENERAL is what a message composed from the mailbox screen is about: nothing in particular.
            RelatedType: string.IsNullOrWhiteSpace(request.RelatedType) ? "GENERAL" : request.RelatedType,
            RelatedId: request.RelatedId);

        void Add(List<EmailRecipientDto>? group, string type)
        {
            foreach (var r in group ?? new List<EmailRecipientDto>())
            {
                if (r is null) continue;
                recipients.Add(new EmailComposeRecipientInput
                {
                    Email = r.Email ?? string.Empty,
                    Name = r.Name,
                    RecipientType = type,
                    DisplayOrder = order++,
                });
            }
        }
    }
}
