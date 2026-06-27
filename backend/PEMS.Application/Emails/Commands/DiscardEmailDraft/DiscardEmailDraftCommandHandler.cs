using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Commands.DiscardEmailDraft;

public sealed class DiscardEmailDraftCommandHandler
    : IRequestHandler<DiscardEmailDraftCommand, DiscardEmailDraftResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DiscardEmailDraftCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DiscardEmailDraftResponse> Handle(
        DiscardEmailDraftCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        var draft = await _db.EmailDrafts
            .FirstOrDefaultAsync(d => d.EmailDraftId == request.EmailDraftId, cancellationToken)
            ?? throw new NotFoundException("EmailDraft", request.EmailDraftId);

        if (draft.CreatedBy != userId)
            throw new ForbiddenException("Bạn chỉ được huỷ email nháp do chính mình tạo.");
        if (draft.Status != EmailDraftStatus.DRAFT)
            throw new ConflictException("Chỉ có thể huỷ email nháp đang ở trạng thái DRAFT.");

        draft.Status = EmailDraftStatus.DISCARDED;
        draft.DiscardedAt = DateTime.Now;
        draft.LastEditedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);

        return new DiscardEmailDraftResponse { EmailDraftId = draft.EmailDraftId, Status = draft.Status.ToString() };
    }
}
