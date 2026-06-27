using System;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Emails.Commands.MarkEmailCompleted;

public class MarkEmailCompletedCommandHandler : IRequestHandler<MarkEmailCompletedCommand, MarkEmailCompletedResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkEmailCompletedCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<MarkEmailCompletedResponse> Handle(MarkEmailCompletedCommand request, CancellationToken cancellationToken)
    {
        var email = await _context.SentEmails
            .Include(e => e.Recipients)
            .FirstOrDefaultAsync(e => e.SentEmailId == request.SentEmailId, cancellationToken);

        if (email == null)
        {
            return new MarkEmailCompletedResponse { Success = false, Message = "Email không tồn tại." };
        }

        var currentUserEmail = _currentUserService.Email;
        if (string.IsNullOrEmpty(currentUserEmail))
        {
            var user = await _context.Users.FindAsync(_currentUserService.UserId);
            currentUserEmail = user?.Email ?? "";
        }

        bool isSender = email.SentBy == _currentUserService.UserId;
        bool isRecipient = email.Recipients.Any(r => r.RecipientEmail == currentUserEmail);

        if (!isSender && !isRecipient)
        {
            return new MarkEmailCompletedResponse { Success = false, Message = "Bạn không có quyền thao tác trên email này." };
        }

        if (email.DeliveredAt.HasValue)
        {
            return new MarkEmailCompletedResponse { Success = false, Message = "Email đã được đánh dấu hoàn thành từ trước." };
        }

        email.DeliveredAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new MarkEmailCompletedResponse { Success = true, Message = "Đã chuyển email sang trạng thái hoàn thành." };
    }
}
