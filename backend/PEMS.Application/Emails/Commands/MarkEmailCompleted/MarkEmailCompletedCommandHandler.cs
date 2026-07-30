using System;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using PEMS.Application.Common;
using PEMS.Application.Emails.Common;
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

        // The same relation the detail query resolves, so the button the screen shows and the answer this
        // command gives are decided by one rule rather than by two hand-written ones. The previous check
        // compared recipient addresses with a case-sensitive == , which quietly refused anyone whose stored
        // address differed only in case from the one on their token.
        var relation = SentEmailAccess.Resolve(
            _currentUserService.UserId, currentUserEmail, email.SentBy, email.Recipients,
            r => r.RecipientEmail, r => r.RecipientType);

        if (!SentEmailAccess.CanMarkComplete(relation, email.DeliveredAt))
        {
            return email.DeliveredAt.HasValue
                ? new MarkEmailCompletedResponse { Success = false, Message = "Email đã được đánh dấu hoàn thành từ trước." }
                : new MarkEmailCompletedResponse { Success = false, Message = "Bạn không có quyền thao tác trên email này." };
        }

        email.DeliveredAt = VietnamTime.Now();

        await _context.SaveChangesAsync(cancellationToken);

        return new MarkEmailCompletedResponse { Success = true, Message = "Đã chuyển email sang trạng thái hoàn thành." };
    }
}
