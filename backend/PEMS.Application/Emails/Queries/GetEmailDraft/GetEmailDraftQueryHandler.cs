using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Queries.GetEmailDraft;

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

        var ownerId = await _db.EmailDrafts
            .Where(d => d.EmailDraftId == request.EmailDraftId)
            .Select(d => d.CreatedBy)
            .FirstOrDefaultAsync(cancellationToken);
        // CreatedBy is nullable; a missing draft yields default (null) → NotFound.
        var draftExists = await _db.EmailDrafts
            .AnyAsync(d => d.EmailDraftId == request.EmailDraftId, cancellationToken);
        if (!draftExists)
            throw new NotFoundException("EmailDraft", request.EmailDraftId);
        if (ownerId != userId)
            throw new ForbiddenException("Bạn chỉ được xem email nháp do chính mình tạo.");

        return (await EmailDraftMapper.LoadDtoAsync(_db, request.EmailDraftId, cancellationToken))!;
    }
}
