using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Commands.SendEmail;

/// <summary>
/// Manual compose. Validates what the sender wrote, then hands the whole envelope to the shared manual
/// pipeline, which records one <c>sent_emails</c> row and sends exactly one MIME message.
///
/// <para>
/// What this handler no longer does: loop the recipients and call SMTP once each (which turned every
/// addressee into a lone TO), hard-code <c>recipient_type = 'TO'</c> for all of them, mark them
/// <c>DELIVERED</c> on the strength of provider acceptance, or write the raw exception text into
/// <c>error_message</c>.
/// </para>
/// </summary>
public sealed class SendEmailCommandHandler : IRequestHandler<SendEmailCommand, SendEmailResponse>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IManualEmailSender _sender;
    private readonly PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer _normalizer;
    private readonly EmailRecipientOptions _recipientOptions;

    public SendEmailCommandHandler(
        ICurrentUserService currentUserService,
        IHtmlSanitizerService sanitizer,
        IManualEmailSender sender,
        PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer normalizer,
        IOptions<EmailRecipientOptions> recipientOptions)
    {
        _currentUserService = currentUserService;
        _sanitizer = sanitizer;
        _sender = sender;
        _normalizer = normalizer;
        _recipientOptions = recipientOptions?.Value ?? new EmailRecipientOptions();
    }

    public async Task<SendEmailResponse> Handle(SendEmailCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is not { } userId)
            throw new ForbiddenException();

        // Content and envelope are both checked BEFORE any row is written, so a rejected message leaves
        // nothing behind in the history.
        var format = EmailDraftWriter.ParseBodyFormat(request.BodyFormat);
        var content = ManualEmailContent.Validate(request.Subject, request.Body, format, _sanitizer);

        var envelope = EmailRecipientValidator.Validate(
            Map(request.To), Map(request.Cc), Map(request.Bcc), _recipientOptions.MaxRecipients);

        var body = content.IsHtml
            ? await _normalizer.NormalizeHtmlAsync(content.Body, cancellationToken)
            : content.Body;

        var result = await _sender.SendAsync(new ManualEmailMessage(
            SenderUserId: userId,
            Subject: content.Subject,
            Body: body,
            BodyFormat: format,
            Envelope: envelope,
            Attachments: new List<ManualEmailAttachment>(),
            RelatedType: "GENERAL"), cancellationToken);

        return new SendEmailResponse
        {
            SentEmailId = result.SentEmailId,
            Status = result.Status,
            Success = result.Success,
            Message = result.Message,
        };
    }

    private static List<EmailRecipient> Map(List<EmailRecipientDto>? source)
        => source is null
            ? new List<EmailRecipient>()
            : source.Where(r => r is not null)
                    .Select(r => new EmailRecipient(r.Email ?? string.Empty, r.Name))
                    .ToList();
}
