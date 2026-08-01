using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Commands.SendEmailDraft;

/// <summary>
/// Sends a saved draft: the author's own TO/CC/BCC, their attachments, one message.
///
/// <para>
/// What remains here is the authorisation this endpoint owns — a draft is sendable by the person who
/// wrote it, and only while it is still a draft. Everything after that decision (content and envelope
/// validation, the send-time re-check of attachment scope, the atomic claim that stops a double click
/// becoming two messages, the link back to the message produced) lives in
/// <see cref="IEmailDraftDispatcher"/>, shared with the setup-progress send whose guards are about a
/// visit rather than a mailbox.
/// </para>
/// </summary>
public sealed class SendEmailDraftCommandHandler
    : IRequestHandler<SendEmailDraftCommand, SendEmailDraftResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailDraftDispatcher _dispatcher;

    public SendEmailDraftCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IEmailDraftDispatcher dispatcher)
    {
        _db = db;
        _currentUser = currentUser;
        _dispatcher = dispatcher;
    }

    public async Task<SendEmailDraftResponse> Handle(
        SendEmailDraftCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        var draft = await _db.EmailDrafts
            .FirstOrDefaultAsync(d => d.EmailDraftId == request.EmailDraftId, cancellationToken)
            ?? throw new NotFoundException("EmailDraft", request.EmailDraftId);

        if (draft.CreatedBy != userId)
            throw new ForbiddenException("Bạn chỉ được gửi email nháp do chính mình tạo.");
        if (draft.Status != EmailDraftStatus.DRAFT)
            throw new ConflictException("Email nháp đã được gửi hoặc huỷ.");

        var result = await _dispatcher.DispatchAsync(draft, userId, cancellationToken);

        return new SendEmailDraftResponse
        {
            EmailDraftId = result.EmailDraftId,
            SentEmailId = result.SentEmailId,
            Status = result.Status,
            Success = result.Success,
            DraftStatus = result.DraftStatus,
            Message = result.Message,
        };
    }
}
