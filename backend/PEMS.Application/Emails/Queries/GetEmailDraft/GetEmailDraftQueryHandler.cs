using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Queries.GetEmailDraft;

/// <summary>
/// Loads one draft for the composer, and answers the three ways that can legitimately fail with three
/// different statuses.
///
/// <para>
/// They used to be two — 404 and 403 — with a sent or discarded draft opening as though it were still
/// editable. The composer would then autosave into it and the send would be refused later by the
/// dispatcher, with a conflict the author had no way to anticipate. A draft sent from another tab is
/// neither missing nor forbidden: it is finished, and saying so is the only answer that lets the screen
/// offer the right next step.
/// </para>
/// </summary>
public sealed class GetEmailDraftQueryHandler : IRequestHandler<GetEmailDraftQuery, EmailDraftDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetEmailDraftQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EmailDraftDto> Handle(GetEmailDraftQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        // One read for all three decisions. Asking twice — once for the owner, once for existence — was
        // not merely wasteful: CreatedBy is nullable, so "no row" and "row owned by nobody" collapsed
        // onto the same default, and only the ordering of the two checks kept them apart.
        var head = await _db.EmailDrafts
            .Where(d => d.EmailDraftId == request.EmailDraftId)
            .Select(d => new { d.CreatedBy, d.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (head is null)
            throw new NotFoundException(
                $"Email nháp (#{request.EmailDraftId}) không tồn tại hoặc đã bị xoá khỏi hệ thống.",
                EmailErrorCodes.DraftNotFound);

        if (head.CreatedBy != userId)
            throw new ForbiddenException("Bạn chỉ được xem email nháp do chính mình tạo.");

        if (head.Status != EmailDraftStatus.DRAFT)
            throw new ConflictException(
                head.Status == EmailDraftStatus.SENT
                    ? "Email nháp này đã được gửi. Nội dung đã gửi xem được trong lịch sử email."
                    : "Email nháp này đã bị huỷ.",
                EmailErrorCodes.DraftNotEditable);

        return (await EmailDraftMapper.LoadDtoAsync(_db, request.EmailDraftId, cancellationToken))!;
    }
}
