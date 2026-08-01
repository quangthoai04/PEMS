using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Commands.SendEmailDraft;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>
/// Sends a setup-progress draft, re-checking at send time everything that could have changed since it
/// was composed.
/// </summary>
public sealed record SendVisitSetupProgressEmailDraftCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    ulong EmailDraftId) : IRequest<SendEmailDraftResponse>;

/// <summary>
/// The reason this is not simply <c>POST /api/Emails/drafts/{id}/send</c>.
///
/// <para>
/// The generic endpoint asks one question — is this your draft, and is it still a draft — and both can
/// be true of a message that must no longer go out. Between opening the composer and pressing send, the
/// campus can be cancelled, the visit can start, or the delegation can be handed to a different host. A
/// draft written by yesterday's host is still owned by yesterday's host, so ownership alone would let
/// them mail the guest as if nothing had changed.
/// </para>
/// <para>
/// So the visit guards run again here, against the database, at the moment of sending; then the draft
/// goes through the SAME dispatcher the generic path uses, so the envelope rules, the attachment
/// re-check and the atomic claim against a double click are one implementation rather than two.
/// </para>
/// </summary>
public sealed class SendVisitSetupProgressEmailDraftCommandHandler
    : IRequestHandler<SendVisitSetupProgressEmailDraftCommand, SendEmailDraftResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailDraftDispatcher _dispatcher;

    public SendVisitSetupProgressEmailDraftCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IEmailDraftDispatcher dispatcher)
    {
        _db = db;
        _currentUser = currentUser;
        _dispatcher = dispatcher;
    }

    public async Task<SendEmailDraftResponse> Handle(
        SendVisitSetupProgressEmailDraftCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        // Re-authorisation, at send time, from the database — not from whatever the browser was told
        // when the composer opened.
        var instance = await VisitSetupProgressEmailGuard.ResolveHostInstanceAsync(
            _db, request.VisitRequestId, request.VisitInstanceId, userId, cancellationToken);

        var draft = await _db.EmailDrafts
            .Include(d => d.EmailTemplate)
            .FirstOrDefaultAsync(d => d.EmailDraftId == request.EmailDraftId, cancellationToken)
            ?? throw new NotFoundException("EmailDraft", request.EmailDraftId);

        RefreshVisitSetupProgressEmailReportCommandHandler.AssertBelongsToThisFlow(
            draft, instance.VisitInstanceId, userId);

        // The report is what makes this mail worth sending; a draft that lost it must not go out as a
        // bare message the guest cannot act on.
        var report = await SetupProgressDrafts.FindReportAttachmentAsync(
            _db, draft.EmailDraftId, instance.VisitRequestId, cancellationToken);
        if (report is null)
            throw new ValidationException(
                "Email này bắt buộc phải đính kèm Báo cáo Lịch trình. Vui lòng tạo lại báo cáo trước khi gửi.");

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
