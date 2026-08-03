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
/// "Đồng bộ từ dữ liệu setup mới nhất": rebuilds BOTH halves of the message from one fresh read — the
/// HTML tables in the body and the attached Schedule Report — and moves the snapshot timestamp.
///
/// <para>
/// A draft can sit open while the agenda, the participant list or the timings change underneath it,
/// and the Host needs to send what is true now. Rebuilding only the PDF was worse than useless: the
/// body's tables would still describe the old state while the attachment described the new one, and
/// the message would contradict itself with nothing to say which half was right.
/// </para>
/// <para>
/// Because the body IS rebuilt, this overwrites anything the Host typed into it. That is why the
/// response reports <see cref="BodyRewritten"/> and why the composer must warn before calling it —
/// silently discarding someone's edit is the one outcome this must not produce. The subject and the
/// recipient list are NOT touched: those are addressing decisions, not a picture of the setup.
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

    /// <summary>The moment BOTH the body and the PDF were built from — one read, one timestamp.</summary>
    public string ReportGeneratedAt { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = "vi";

    /// <summary>The freshly rendered body, so the composer can show what it now holds.</summary>
    public string BodyHtml { get; init; } = string.Empty;

    /// <summary>Always true — kept explicit so the client cannot treat a rebuild as additive.</summary>
    public bool BodyRewritten { get; init; } = true;
}

public sealed class RefreshVisitSetupProgressEmailReportCommandHandler
    : IRequestHandler<RefreshVisitSetupProgressEmailReportCommand, RefreshVisitSetupProgressEmailReportResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IScheduleReportArtifactService _reports;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly PEMS.Application.Delegations.Services.VisitFormRead.IVisitFormReadService _formRead;

    public RefreshVisitSetupProgressEmailReportCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IScheduleReportArtifactService reports,
        IEmailTemplateRenderer renderer,
        PEMS.Application.Delegations.Services.VisitFormRead.IVisitFormReadService formRead)
    {
        _db = db;
        _currentUser = currentUser;
        _reports = reports;
        _renderer = renderer;
        _formRead = formRead;
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

        // ── One read, both halves ─────────────────────────────────────────────
        var artifact = await _reports.RenderAsync(instance, language, cancellationToken);
        var snapshot = await VisitSetupSnapshotBuilder.BuildAsync(_db, instance, artifact, cancellationToken);

        var content = await _formRead.ResolveCampusFormContentAsync(
            instance.VisitRequest, new[] { instance.VisitInstanceId }, cancellationToken);
        var delegationName = content.TryGetValue(instance.VisitInstanceId, out var detail)
            ? detail.DelegationName
            : string.Empty;
        var host = await _db.Users
            .Where(u => u.UserId == userId).Select(u => new { u.FullName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);
        var hostName = host?.FullName ?? string.Empty;
        var hostEmail = host?.Email ?? string.Empty;

        var rendered = await _renderer.RenderAsync(new EmailRenderRequest(
            VisitSetupProgressEmailGuard.TemplateCode,
            language,
            VisitSetupProgressEmailGuard.BuildVariables(
                instance, delegationName, snapshot.CampusName, hostName, hostEmail),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EmailTrustedBlocks.SetupSummaryBlock] = VisitSetupEmailHtml.Render(snapshot, language),
            }),
            cancellationToken);

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

        // The body is replaced, the subject and the recipients are not. Those are the Host's
        // addressing decisions; the body is the picture of the setup that just changed.
        draft.BodyContent = rendered.Body;
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
            BodyHtml = rendered.Body,
            BodyRewritten = true,
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
