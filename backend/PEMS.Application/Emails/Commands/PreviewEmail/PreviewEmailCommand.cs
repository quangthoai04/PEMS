using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Commands.PreviewEmail;

/// <summary>
/// "Xem trước": what would go out, checked by the same code that would send it — and sent nowhere.
///
/// <para>
/// It takes the same payload as the send. That is the point: the composer previews by POSTing the message
/// it is holding, so the author is shown the body AFTER sanitising and the envelope AFTER the address
/// rules, rather than a browser-side approximation of both. The preview the composer used to show was
/// rendered from local state with a frontend sanitiser whose allow-list is not the backend's, so a body
/// could preview cleanly and be delivered with parts of it removed.
/// </para>
/// <para>
/// A refusal here is the same refusal the send would give, which is the useful thing to learn before
/// pressing send rather than after. Nothing is written and nothing reaches a provider.
/// </para>
/// </summary>
public sealed class PreviewEmailCommand : IRequest<PreviewEmailResponse>
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>PLAIN_TEXT | HTML. Defaults to HTML, which is what the composer produces.</summary>
    public string? BodyFormat { get; set; }

    public List<Commands.SendEmail.EmailRecipientDto> To { get; set; } = new();
    public List<Commands.SendEmail.EmailRecipientDto> Cc { get; set; } = new();
    public List<Commands.SendEmail.EmailRecipientDto> Bcc { get; set; } = new();

    public List<EmailComposeAttachmentInput> Attachments { get; set; } = new();

    public string? RelatedType { get; set; }
    public ulong? RelatedId { get; set; }
}

public sealed class PreviewEmailResponse
{
    public string Subject { get; set; } = string.Empty;

    /// <summary>The body as it would be SENT — sanitised, and with images normalised.</summary>
    public string Body { get; set; } = string.Empty;

    public bool IsHtml { get; set; }

    /// <summary>Each group as addresses, so the composer shows what the envelope resolved to.</summary>
    public List<string> To { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public List<string> Bcc { get; set; } = new();

    /// <summary>
    /// The attachments, named. Reaching this list at all means every one of them was readable: an
    /// unreadable file refuses the preview exactly as it refuses the send.
    /// </summary>
    public List<string> Attachments { get; set; } = new();
}

public sealed class PreviewEmailCommandHandler : IRequestHandler<PreviewEmailCommand, PreviewEmailResponse>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDirectEmailSender _sender;

    public PreviewEmailCommandHandler(ICurrentUserService currentUserService, IDirectEmailSender sender)
    {
        _currentUserService = currentUserService;
        _sender = sender;
    }

    public async Task<PreviewEmailResponse> Handle(
        PreviewEmailCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is not { } userId)
            throw new ForbiddenException();

        // Mapped through the SEND command's own mapper, so preview and send cannot disagree about what the
        // payload means.
        var preview = await _sender.PreviewAsync(
            Commands.SendEmail.SendEmailRequestMapper.ToDirectRequest(new Commands.SendEmail.SendEmailCommand
            {
                Subject = request.Subject,
                Body = request.Body,
                BodyFormat = request.BodyFormat,
                To = request.To,
                Cc = request.Cc,
                Bcc = request.Bcc,
                Attachments = request.Attachments,
                RelatedType = request.RelatedType,
                RelatedId = request.RelatedId,
            }),
            userId,
            cancellationToken);

        return new PreviewEmailResponse
        {
            Subject = preview.Subject,
            Body = preview.Body,
            IsHtml = preview.IsHtml,
            To = new List<string>(preview.To),
            Cc = new List<string>(preview.Cc),
            Bcc = new List<string>(preview.Bcc),
            Attachments = new List<string>(preview.Attachments),
        };
    }
}
