using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>
/// Regenerates the Schedule Report on an existing setup-progress draft from the data as it stands now.
///
/// <para>
/// A draft can sit open while the agenda, the participant list or the timings change underneath it. The
/// Host needs to send the current report, not the one that happened to exist when they opened the
/// composer — but re-rendering the TEMPLATE at the same time would silently discard whatever they had
/// written, so this replaces the attachment and touches nothing else: recipients, subject and body are
/// left exactly as the Host left them.
/// </para>
/// </summary>
public sealed record RefreshVisitSetupProgressEmailReportCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    ulong EmailDraftId,
    string? LanguageCode) : IRequest<RefreshVisitSetupProgressEmailReportResponse>;

public sealed class RefreshVisitSetupProgressEmailReportResponse
{
    public ulong DraftId { get; init; }
    public ulong ReportFileId { get; init; }
    public string ReportFileName { get; init; } = string.Empty;
    public string ReportGeneratedAt { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = "vi";
}

public sealed class RefreshVisitSetupProgressEmailReportCommandHandler
    : IRequestHandler<RefreshVisitSetupProgressEmailReportCommand, RefreshVisitSetupProgressEmailReportResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IScheduleReportArtifactService _reports;

    public RefreshVisitSetupProgressEmailReportCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IScheduleReportArtifactService reports)
    {
        _db = db;
        _currentUser = currentUser;
        _reports = reports;
    }

    public async Task<RefreshVisitSetupProgressEmailReportResponse> Handle(
        RefreshVisitSetupProgressEmailReportCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        var instance = await VisitSetupProgressEmailGuard.ResolveHostInstanceAsync(
            _db, request.VisitRequestId, request.VisitInstanceId, userId, cancellationToken);

        var draft = await _db.EmailDrafts
            .Include(d => d.EmailTemplate)
            .FirstOrDefaultAsync(d => d.EmailDraftId == request.EmailDraftId, cancellationToken)
            ?? throw new NotFoundException("EmailDraft", request.EmailDraftId);

        AssertBelongsToThisFlow(draft, instance.VisitInstanceId, userId);

        var previous = await SetupProgressDrafts.FindReportAttachmentAsync(
            _db, draft.EmailDraftId, instance.VisitRequestId, cancellationToken);

        // Default to the language the draft already carries, so "làm mới báo cáo" cannot quietly swap
        // an English attachment onto a Vietnamese message.
        var language = request.LanguageCode is { Length: > 0 } requested
            ? (string.Equals(requested, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi")
            : SetupProgressDrafts.LanguageOf(previous?.FileName);

        var artifact = await _reports.RenderAsync(instance, language, cancellationToken);
        var fileId = await _reports.StoreAsync(artifact, instance, userId, cancellationToken);

        var now = VietnamTime.Now();

        // Replace, not append: the draft must end up with exactly one mandatory report on it. The old
        // FILE is left archived — it is a genuine report of this delegation, identical to one the
        // download button would have filed, and deleting it would erase a record rather than tidy up.
        if (previous is { } old)
        {
            var stale = await _db.EmailDraftAttachments
                .FirstOrDefaultAsync(a => a.EmailDraftAttachmentId == old.EmailDraftAttachmentId, cancellationToken);
            if (stale is not null) _db.EmailDraftAttachments.Remove(stale);
        }

        _db.EmailDraftAttachments.Add(new EmailDraftAttachment
        {
            EmailDraftId = draft.EmailDraftId,
            FileId = fileId,
            AttachmentType = EmailAttachmentType.ATTACHMENT,
            DisplayName = artifact.FileName,
            DisplayOrder = 0,
            CreatedAt = now,
        });

        draft.LastEditedBy = userId;
        draft.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        return new RefreshVisitSetupProgressEmailReportResponse
        {
            DraftId = draft.EmailDraftId,
            ReportFileId = fileId,
            ReportFileName = artifact.FileName,
            ReportGeneratedAt = artifact.GeneratedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            LanguageCode = language,
        };
    }

    /// <summary>
    /// Refuses a draft that is not this Host's setup-progress draft for this instance. Each clause
    /// closes a different way of pointing a valid-looking request at somebody else's work: a draft id
    /// guessed from another user, one belonging to a different campus, or an ordinary composed email
    /// steered through an endpoint whose guards were written for a template with no token in it.
    /// </summary>
    internal static void AssertBelongsToThisFlow(EmailDraft draft, ulong visitInstanceId, ulong userId)
    {
        if (draft.CreatedBy != userId)
            throw new ForbiddenException("Bạn chỉ được thao tác trên email nháp do chính mình tạo.");
        if (draft.Status != EmailDraftStatus.DRAFT)
            throw new ConflictException("Email nháp đã được gửi hoặc huỷ.");
        if (draft.RelatedType != SetupProgressDrafts.RelatedType || draft.RelatedId != visitInstanceId)
            throw new NotFoundException("EmailDraft", draft.EmailDraftId);
        if (draft.EmailTemplate?.TemplateCode != VisitSetupProgressEmailGuard.TemplateCode)
            throw new NotFoundException("EmailDraft", draft.EmailDraftId);
    }
}
