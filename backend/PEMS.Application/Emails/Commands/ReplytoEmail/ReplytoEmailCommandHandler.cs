using System;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Emails;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

using PEMS.Application.Common;
namespace PEMS.Application.Emails.Commands.ReplytoEmail;

public class ReplytoEmailCommandHandler : IRequestHandler<ReplytoEmailCommand, ReplytoEmailResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;

    public ReplytoEmailCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, IEmailService emailService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
    }

    public async Task<ReplytoEmailResponse> Handle(ReplytoEmailCommand request, CancellationToken cancellationToken)
    {
        var originalEmail = await _context.SentEmails
            .Include(e => e.Recipients)
            .FirstOrDefaultAsync(e => e.SentEmailId == request.OriginalEmailId, cancellationToken);

        if (originalEmail == null)
            return new ReplytoEmailResponse { Success = false, Message = "Email gốc không tồn tại." };

        // Determine who to send the reply to. Usually, reply to the sender of the original email.
        string toEmail = "sender@pems.local";
        string toName = "Sender";
        if (originalEmail.SentBy.HasValue)
        {
            var originalSender = await _context.Users.FindAsync(originalEmail.SentBy.Value);
            if (originalSender != null)
            {
                toEmail = originalSender.Email;
                toName = originalSender.FullName;
            }
        }
        else
        {
             // If original sender is not a user, maybe it's system email
             return new ReplytoEmailResponse { Success = false, Message = "Không thể phản hồi email hệ thống tự động." };
        }

        var replySubject = originalEmail.Subject.StartsWith("Re: ") ? originalEmail.Subject : "Re: " + originalEmail.Subject;

        var newEmail = new SentEmail
        {
            Subject = replySubject,
            BodySnapshot = request.Body,
            Status = "QUEUED",
            SentBy = _currentUserService.UserId,
            CreatedAt = VietnamTime.Now(),
            RelatedType = "REPLY",
            RelatedId = request.OriginalEmailId
        };

        newEmail.Recipients.Add(new SentEmailRecipient
        {
            RecipientEmail = toEmail,
            RecipientName = toName,
            RecipientType = "TO",
            DeliveryStatus = "PENDING"
        });

        if (request.Cc != null)
        {
            foreach(var cc in request.Cc)
                newEmail.Recipients.Add(new SentEmailRecipient { RecipientEmail = cc.Email, RecipientName = cc.Name, RecipientType = "CC", DeliveryStatus = "PENDING" });
        }
        if (request.Bcc != null)
        {
            foreach(var bcc in request.Bcc)
                newEmail.Recipients.Add(new SentEmailRecipient { RecipientEmail = bcc.Email, RecipientName = bcc.Name, RecipientType = "BCC", DeliveryStatus = "PENDING" });
        }

        _context.SentEmails.Add(newEmail);
        
        // Mark original email as completed
        originalEmail.DeliveredAt = VietnamTime.Now();

        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendAsync(toEmail, replySubject, request.Body, cancellationToken);
            newEmail.Status = "SENT";
            newEmail.SentAt = VietnamTime.Now();
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            newEmail.Status = "FAILED";
            newEmail.ErrorMessage = ex.Message;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new ReplytoEmailResponse { Success = true, Message = "Đã phản hồi email thành công." };
    }
}