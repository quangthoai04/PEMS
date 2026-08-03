using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
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
    private readonly IFileStorageService _storage;
    private readonly IGoogleDriveStorageService _drive;

    public SendVisitSetupProgressEmailDraftCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IEmailDraftDispatcher dispatcher,
        IFileStorageService storage,
        IGoogleDriveStorageService drive)
    {
        _db = db;
        _currentUser = currentUser;
        _dispatcher = dispatcher;
        _storage = storage;
        _drive = drive;
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

        // Having the ROW is not having the FILE. The dispatcher's attachment loader skips a file whose
        // bytes it cannot read — reasonable for an optional attachment, silently wrong for this one:
        // the body tells the guest a schedule report is attached, so a send that drops it delivers a
        // message that contradicts itself, and the Host is told it went fine.
        //
        // Refused rather than regenerated. Regenerating here would attach a report built from setup data
        // newer than the tables already written into this body, so the message would describe one state
        // and carry another — the exact contradiction "Đồng bộ dữ liệu mới nhất" exists to prevent by
        // rebuilding BOTH halves together. So: name the cause, and point at the button that fixes it.
        var probe = await StoredFileProbe.ProbeAsync(
            _db, _storage, _drive, report.Value.FileId, cancellationToken);
        if (!probe.IsAvailable)
            throw new ValidationException(
                $"Không gửi được: {StoredFileProbe.Describe(probe.Availability)}. "
                + "Bấm “Đồng bộ dữ liệu mới nhất” để tạo lại Báo cáo Lịch trình, rồi gửi lại. "
                + $"(Mã lỗi: {probe.ErrorCode})");

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
