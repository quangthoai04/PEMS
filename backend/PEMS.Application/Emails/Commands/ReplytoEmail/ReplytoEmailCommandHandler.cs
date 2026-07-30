using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Commands.ReplytoEmail;

/// <summary>
/// Replies to a message the current user is entitled to read.
///
/// <para>
/// Three defects made this the most dishonest path in the module. It wrote CC and BCC rows into the
/// history and then sent only to the TO address — so the record claimed delivery to people who received
/// nothing at all. It let any holder of an internal role reply to any message by id, without checking
/// whether they were party to it. And it set <c>delivered_at</c> on the ORIGINAL message as a side
/// effect, fabricating a delivery confirmation on a record the reply had no business touching.
/// </para>
/// <para>
/// <b>Reply and Reply All.</b> Reply is addressed to the original sender alone. Reply All adds the
/// original's VISIBLE recipients and drops the replier's own mailbox. Neither carries the original's blind
/// copies: BCC is a fact about that send, and forwarding it into a new message would tell the whole thread
/// who had been included quietly. The BCC rows are filtered out before the planner is even called, so
/// there is no ordering mistake that could leak them.
/// </para>
/// <para>
/// Re-authorisation happens on every reply, in both modes, against the message as it stands now — being
/// party to the conversation is checked at send time, not inherited from whenever the reader opened it.
/// </para>
/// </summary>
public class ReplytoEmailCommandHandler : IRequestHandler<ReplytoEmailCommand, ReplytoEmailResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly IManualEmailSender _sender;
    private readonly EmailRecipientOptions _recipientOptions;

    public ReplytoEmailCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IHtmlSanitizerService sanitizer,
        IManualEmailSender sender,
        IOptions<EmailRecipientOptions> recipientOptions)
    {
        _context = context;
        _currentUserService = currentUserService;
        _sanitizer = sanitizer;
        _sender = sender;
        _recipientOptions = recipientOptions?.Value ?? new EmailRecipientOptions();
    }

    public async Task<ReplytoEmailResponse> Handle(
        ReplytoEmailCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is not { } userId)
            throw new ForbiddenException();

        var original = await _context.SentEmails
            .FirstOrDefaultAsync(e => e.SentEmailId == request.OriginalEmailId, cancellationToken)
            ?? throw new NotFoundException("SentEmail", request.OriginalEmailId);

        var originalRecipients = await _context.SentEmailRecipients
            .Where(r => r.SentEmailId == original.SentEmailId)
            .ToListAsync(cancellationToken);

        // ── May this person reply at all? ─────────────────────────────────────
        //
        // Being party to the conversation is what grants the right — sender or addressee, blind copies
        // included. A role does not: an HO who was never on the message has nothing to reply to.
        var viewerEmail = await ResolveViewerEmailAsync(userId, cancellationToken);
        var relation = SentEmailAccess.Resolve(
            userId, viewerEmail, original.SentBy, originalRecipients,
            r => r.RecipientEmail, r => r.RecipientType);

        if (!SentEmailAccess.CanView(relation))
            throw new ForbiddenException("Bạn không có quyền phản hồi email này.");

        // A system-generated message has no person behind it to answer.
        if (original.SentBy is not { } originalSenderId)
            throw new ConflictException("Không thể phản hồi email hệ thống tự động.");

        var originalSender = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == originalSenderId, cancellationToken)
            ?? throw new ConflictException("Không tìm thấy người gửi của email gốc để phản hồi.");

        var subject = original.Subject.StartsWith("Re: ", StringComparison.OrdinalIgnoreCase)
            ? original.Subject
            : "Re: " + original.Subject;

        var content = ManualEmailContent.Validate(
            subject, request.Body, EmailBodyFormat.HTML, _sanitizer);

        // Who the reply goes to. In Reply mode: the original sender plus this author's own copies. In
        // Reply All: additionally the original's VISIBLE recipients, minus this author's own mailbox.
        // The planner is never given the original's BCC rows, so no path exists by which they could
        // reappear — and the caller cannot assert recipients either, because only `ReplyAll` crosses the
        // wire, not a list of addresses.
        var plan = ReplyRecipientPlanner.Plan(
            request.ReplyAll ? ReplyMode.All : ReplyMode.SenderOnly,
            new EmailRecipient(originalSender.Email, originalSender.FullName),
            originalRecipients
                .Where(r => !string.Equals(r.RecipientType, EmailRecipientTypes.Bcc, StringComparison.OrdinalIgnoreCase))
                .Select(r => new ReplySourceRecipient(r.RecipientEmail, r.RecipientName, r.RecipientType))
                .ToList(),
            Map(request.Cc),
            Map(request.Bcc),
            viewerEmail);

        var envelope = EmailRecipientValidator.Validate(
            plan.To, plan.Cc, plan.Bcc, _recipientOptions.MaxRecipients);

        var result = await _sender.SendAsync(new ManualEmailMessage(
            SenderUserId: userId,
            Subject: content.Subject,
            Body: content.Body,
            BodyFormat: EmailBodyFormat.HTML,
            Envelope: envelope,
            Attachments: new List<ManualEmailAttachment>(),
            // The existing contract records the parent here; the threading headers come from InReplyTo.
            RelatedType: "REPLY",
            RelatedId: original.SentEmailId,
            InReplyTo: original), cancellationToken);

        // The original is left exactly as it was. Marking a message "handled" is a separate, explicit
        // action (mark-completed) that the reader takes on purpose.
        return new ReplytoEmailResponse
        {
            Success = result.Success,
            Message = result.Message,
        };
    }

    private async Task<string?> ResolveViewerEmailAsync(ulong userId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_currentUserService.Email)) return _currentUserService.Email;

        return await _context.Users
            .Where(u => u.UserId == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static List<EmailRecipient> Map(List<EmailRecipientInput>? source)
        => source is null
            ? new List<EmailRecipient>()
            : source.Where(r => r is not null)
                    .Select(r => new EmailRecipient(r.Email ?? string.Empty, r.Name))
                    .ToList();
}
