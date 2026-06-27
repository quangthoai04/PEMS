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
            .Include(e => e.EmailTemplate)
            .Include(e => e.Recipients)
            .Include(e => e.Attachments).ThenInclude(a => a.File)
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

        // Bỏ logic filter isSender, isRecipient tạm thời hoặc giữ lại nhưng nới lỏng (ví dụ HO có quyền xem tất cả).
        // Yêu cầu bài toán là HO, StaffLeader... xem chi tiết quản lý email đã gửi, nên không thể chỉ sender/recipient mới được xem.

        SentEmailSenderDto? senderDto = null;
        if (email.SentBy.HasValue)
        {
            var sender = await _context.Users.FindAsync(email.SentBy.Value);
            if (sender != null)
            {
                senderDto = new SentEmailSenderDto
                {
                    UserId = sender.UserId,
                    FullName = sender.FullName,
                    Email = sender.Email
                };
            }
        }

        var dto = new ViewEmailDto
        {
            SentEmailId = email.SentEmailId,
            EmailTemplateId = email.EmailTemplateId,
            TemplateName = email.EmailTemplate?.Name,
            TemplateCode = email.EmailTemplate?.TemplateCode,
            RelatedType = email.RelatedType,
            RelatedId = email.RelatedId,
            Subject = email.Subject,
            BodySnapshot = email.BodySnapshot ?? "",
            Status = email.Status,
            ErrorMessage = email.ErrorMessage,
            RetryCount = email.RetryCount,
            LastAttemptAt = email.LastAttemptAt,
            DeliveredAt = email.DeliveredAt,
            SentAt = email.SentAt,
            CreatedAt = email.CreatedAt,
            Sender = senderDto,
            Recipients = email.Recipients.Select(r => new SentEmailRecipientDto
            {
                RecipientEmail = r.RecipientEmail,
                RecipientName = r.RecipientName,
                RecipientType = r.RecipientType,
                DeliveryStatus = r.DeliveryStatus,
                ProviderMessageId = r.ProviderMessageId,
                ErrorMessage = r.ErrorMessage,
                SentAt = r.SentAt,
                DeliveredAt = r.DeliveredAt
            }).ToList(),
            Attachments = email.Attachments.Select(a => new SentEmailAttachmentDto
            {
                FileId = a.FileId,
                FileName = a.File?.OriginalFilename ?? a.DisplayName ?? "unknown_file",
                MimeType = a.File?.MimeType,
                SizeBytes = a.File?.FileSize,
                IsInline = a.AttachmentType == PEMS.Domain.Enums.EmailAttachmentType.INLINE_IMAGE,
                ContentId = a.ContentId,
                PreviewUrl = a.File?.WebViewUrl,
                DownloadUrl = a.File?.DownloadUrl
            }).ToList()
        };

        return dto;
    }
}