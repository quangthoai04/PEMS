using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Emails.Queries.ViewEmail;

public class ViewEmailQueryHandler : IRequestHandler<ViewEmailQuery, ViewEmailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ViewEmailQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ViewEmailDto> Handle(ViewEmailQuery request, CancellationToken cancellationToken)
    {
        var email = await _context.SentEmails
            .Include(e => e.Recipients)
            .FirstOrDefaultAsync(e => e.SentEmailId == request.Id, cancellationToken);

        if (email == null)
            return null; // Handle properly or throw NotFoundException

        var currentUserId = _currentUserService.UserId;
        var currentUserEmail = _currentUserService.Email;

        if (string.IsNullOrEmpty(currentUserEmail))
        {
            var user = await _context.Users.FindAsync(currentUserId);
            currentUserEmail = user?.Email ?? "";
        }

        bool isSender = email.SentBy == currentUserId;
        bool isRecipient = email.Recipients.Any(r => r.RecipientEmail == currentUserEmail);

        if (!isSender && !isRecipient)
            return null; // Not authorized

        string senderName = "Hệ thống / Người dùng";
        string senderEmail = "sender@pems.local";
        if (email.SentBy.HasValue)
        {
            var sender = await _context.Users.FindAsync(email.SentBy.Value);
            if (sender != null)
            {
                senderName = sender.FullName;
                senderEmail = sender.Email;
            }
        }

        var dto = new ViewEmailDto
        {
            Id = email.SentEmailId,
            Subject = email.Subject,
            Body = email.BodySnapshot ?? "",
            SenderName = senderName,
            SenderEmail = senderEmail,
            SentAt = email.SentAt,
            Status = email.Status,
            ProcessStatus = email.DeliveredAt.HasValue ? "COMPLETED" : (email.Status == "FAILED" ? "FAILED" : "PROCESSING"),
            To = email.Recipients.Where(r => r.RecipientType == "TO").Select(r => new EmailRecipientDto { Name = r.RecipientName ?? r.RecipientEmail, Email = r.RecipientEmail, DeliveryStatus = r.DeliveryStatus }).ToList(),
            Cc = email.Recipients.Where(r => r.RecipientType == "CC").Select(r => new EmailRecipientDto { Name = r.RecipientName ?? r.RecipientEmail, Email = r.RecipientEmail, DeliveryStatus = r.DeliveryStatus }).ToList(),
            Bcc = email.Recipients.Where(r => r.RecipientType == "BCC").Select(r => new EmailRecipientDto { Name = r.RecipientName ?? r.RecipientEmail, Email = r.RecipientEmail, DeliveryStatus = r.DeliveryStatus }).ToList(),
            CanReply = isRecipient,
            CanConfirm = isRecipient && !email.DeliveredAt.HasValue && email.Status != "FAILED",
            CanMarkComplete = (isRecipient || isSender) && !email.DeliveredAt.HasValue && email.Status != "FAILED"
        };

        return dto;
    }
}