using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Emails;

namespace PEMS.Application.Emails.Commands.SendEmail;

public sealed class SendEmailCommandHandler : IRequestHandler<SendEmailCommand, SendEmailResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;

    public SendEmailCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEmailService emailService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
    }

    public async Task<SendEmailResponse> Handle(SendEmailCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sentEmail = new SentEmail
        {
            EmailTemplateId = request.TemplateId.HasValue ? (ulong?)request.TemplateId.Value : null,
            RelatedType = "GENERAL",
            Subject = request.Subject,
            BodySnapshot = request.Body,
            Status = "QUEUED",
            SentBy = _currentUserService.UserId,
            CreatedAt = now,
            LastAttemptAt = now,
        };

        foreach (var recipient in request.To)
        {
            var email = recipient.Email.Trim();
            if (string.IsNullOrWhiteSpace(email)) continue;

            sentEmail.Recipients.Add(new SentEmailRecipient
            {
                RecipientEmail = email,
                RecipientName = email,
                RecipientType = "TO",
                DeliveryStatus = "QUEUED",
                CreatedAt = now,
            });
        }

        _context.SentEmails.Add(sentEmail);
        await _context.SaveChangesAsync(cancellationToken);

        var hasFailure = false;
        foreach (var recipient in sentEmail.Recipients)
        {
            recipient.SentAt = DateTime.UtcNow;
            try
            {
                await _emailService.SendAsync(recipient.RecipientEmail, request.Subject, request.Body, cancellationToken);
                recipient.DeliveryStatus = "DELIVERED";
                recipient.DeliveredAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                hasFailure = true;
                recipient.DeliveryStatus = "FAILED";
                recipient.ErrorMessage = ex.Message;
            }
        }

        sentEmail.SentAt = DateTime.UtcNow;
        sentEmail.LastAttemptAt = sentEmail.SentAt;

        // Compute aggregated status: ALL ok → SENT; ALL failed → FAILED; mixed → PARTIAL_FAILED.
        var allFailed = sentEmail.Recipients.All(r => r.DeliveryStatus == "FAILED");
        if (!hasFailure)
        {
            sentEmail.Status = "SENT";
            sentEmail.DeliveredAt = sentEmail.SentAt;
            sentEmail.ErrorMessage = null;
        }
        else if (allFailed)
        {
            sentEmail.Status = "FAILED";
            sentEmail.DeliveredAt = null;
            sentEmail.ErrorMessage = "Tất cả người nhận gửi thất bại.";
        }
        else
        {
            sentEmail.Status = "PARTIAL_FAILED";
            sentEmail.DeliveredAt = null;
            sentEmail.ErrorMessage = "Một hoặc nhiều người nhận gửi thất bại.";
        }

        await _context.SaveChangesAsync(cancellationToken);

        var message = sentEmail.Status switch
        {
            "SENT" => "Gửi email thành công.",
            "FAILED" => "Gửi email thất bại với tất cả người nhận.",
            _ => "Gửi email thất bại với một hoặc nhiều người nhận.",
        };

        return new SendEmailResponse
        {
            SentEmailId = sentEmail.SentEmailId,
            Status = sentEmail.Status,
            Success = sentEmail.Status == "SENT",
            Message = message,
        };
    }
}
