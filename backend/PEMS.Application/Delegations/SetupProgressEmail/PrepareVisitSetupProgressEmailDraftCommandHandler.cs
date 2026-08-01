using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>
/// Builds the Host's setup-progress draft server-side — template, recipients and the mandatory Schedule
/// Report — so the compose screen opens on something the backend already stands behind.
///
/// <para>
/// Nothing the client sends decides any of it. The recipient list is derived from the instance, the
/// subject and body are rendered from <c>email_templates</c>, and the PDF is produced by the same
/// service the download button uses. The only client input is the language and whether to re-open an
/// existing draft.
/// </para>
/// </summary>
public sealed class PrepareVisitSetupProgressEmailDraftCommandHandler
    : IRequestHandler<PrepareVisitSetupProgressEmailDraftCommand, PrepareVisitSetupProgressEmailDraftResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IVisitSetupProgressRecipientResolver _recipients;
    private readonly IScheduleReportArtifactService _reports;
    private readonly IVisitFormReadService _formRead;
    private readonly IFileStorageService _storage;
    private readonly IGoogleDriveStorageService _drive;
    private readonly ILogger<PrepareVisitSetupProgressEmailDraftCommandHandler> _logger;

    public PrepareVisitSetupProgressEmailDraftCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IEmailTemplateRenderer renderer,
        IVisitSetupProgressRecipientResolver recipients,
        IScheduleReportArtifactService reports,
        IVisitFormReadService formRead,
        IFileStorageService storage,
        IGoogleDriveStorageService drive,
        ILogger<PrepareVisitSetupProgressEmailDraftCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _renderer = renderer;
        _recipients = recipients;
        _reports = reports;
        _formRead = formRead;
        _storage = storage;
        _drive = drive;
        _logger = logger;
    }

    public async Task<PrepareVisitSetupProgressEmailDraftResponse> Handle(
        PrepareVisitSetupProgressEmailDraftCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        var instance = await VisitSetupProgressEmailGuard.ResolveHostInstanceAsync(
            _db, request.VisitRequestId, request.VisitInstanceId, userId, cancellationToken);

        // ── Re-open rather than pile up ───────────────────────────────────────
        if (request.ReuseExistingDraft)
        {
            var existing = await SetupProgressDrafts.FindReusableAsync(
                _db, instance.VisitInstanceId, userId, cancellationToken);
            if (existing is not null)
            {
                var report = await SetupProgressDrafts.FindReportAttachmentAsync(
                    _db, existing.EmailDraftId, instance.VisitRequestId, cancellationToken);

                // A draft whose mandatory report has gone (a purged file, a half-finished earlier
                // attempt) is not reusable: re-opening it would present a sendable-looking composer with
                // no report on it. Fall through and build a fresh one instead.
                //
                // "Gone" has to mean the FILE, not the row. Checking only that the documents row exists
                // was the same mistake made everywhere else in this flow: the row outlives its bytes, so
                // a report deleted on Drive still produced a composer that looked complete and a message
                // that would have arrived without the attachment it promises. The probe reads the bytes.
                var reportIsUsable = false;
                if (report is not null)
                {
                    var probe = await StoredFileProbe.ProbeAsync(
                        _db, _storage, _drive, report.Value.FileId, cancellationToken);
                    reportIsUsable = probe.IsAvailable;

                    if (!reportIsUsable)
                    {
                        // Regenerate rather than refuse. The Host asked for a progress update; the report
                        // is derived data this service can rebuild from the setup as it stands now, so the
                        // useful answer is a working draft — not an error about a file they never chose.
                        // Logged anyway: a vanished archived report is worth an operator's attention even
                        // when the Host never notices it happened.
                        _logger.LogWarning(
                            "Setup-progress draft {DraftId} for visit instance {VisitInstanceId} references report file "
                            + "{FileId}, which is unreadable ({Code}); building a fresh draft instead of reusing it.",
                            existing.EmailDraftId, instance.VisitInstanceId, report.Value.FileId, probe.ErrorCode);
                    }
                }

                if (reportIsUsable)
                {
                    return new PrepareVisitSetupProgressEmailDraftResponse
                    {
                        DraftId = existing.EmailDraftId,
                        ReusedExistingDraft = true,
                        // Read back from the artifact rather than echoing the request: the draft was
                        // rendered in whatever language it was created with, and saying otherwise would
                        // misdescribe content this call did not produce.
                        LanguageCode = SetupProgressDrafts.LanguageOf(report!.Value.FileName),
                        ReportFileId = report.Value.FileId,
                        ReportFileName = report.Value.FileName,
                        ReportGeneratedAt = report.Value.GeneratedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                        // Deliberately NOT the stored body. This draft may have been edited in an
                        // earlier session and nothing records whether it was, so claiming its content is
                        // the generated text would tell the composer there is nothing to lose and let a
                        // sync overwrite the Host's paragraphs without asking. Left empty, the composer
                        // treats the body as possibly edited and warns — the wrong guess in the safe
                        // direction, costing one confirmation click instead of somebody's writing.
                        BodyHtml = string.Empty,
                        Warnings = new List<string>
                        {
                            $"Đang mở bản nháp đã tạo lúc {existing.CreatedAt:HH:mm dd/MM/yyyy}. " +
                            "Chọn “Tạo bản cập nhật mới” nếu anh/chị muốn soạn lại từ đầu.",
                        },
                    };
                }
            }
        }

        var language = string.Equals(request.LanguageCode, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";

        // ── Content: rendered by the backend from email_templates, never by the client ─────────
        var content = await _formRead.ResolveCampusFormContentAsync(
            instance.VisitRequest, new[] { instance.VisitInstanceId }, cancellationToken);
        var delegationName = content.TryGetValue(instance.VisitInstanceId, out var detail)
            ? detail.DelegationName
            : string.Empty;

        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId).Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var hostName = await _db.Users
            .Where(u => u.UserId == userId).Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        // ── The report FIRST: its data is also what the body's HTML tables are built from, so the
        // message and its attachment describe one moment rather than two reads either side of a save.
        var artifact = await _reports.RenderAsync(instance, language, cancellationToken);
        var snapshot = await VisitSetupSnapshotBuilder.BuildAsync(_db, instance, artifact, cancellationToken);

        var rendered = await _renderer.RenderAsync(new EmailRenderRequest(
            VisitSetupProgressEmailGuard.TemplateCode,
            language,
            VisitSetupProgressEmailGuard.BuildVariables(instance, delegationName, campusName, hostName),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EmailTrustedBlocks.SetupSummaryBlock] = VisitSetupEmailHtml.Render(snapshot, language),
            }),
            cancellationToken);

        var envelope = await _recipients.ResolveAsync(instance, cancellationToken);

        // Archived only after the body rendered: a failure here must not leave a stored PDF behind
        // for a draft that was never created.
        var reportFileId = await _reports.StoreAsync(artifact, instance, userId, cancellationToken);

        var now = VietnamTime.Now();
        var draft = new EmailDraft
        {
            EmailTemplateId = rendered.EmailTemplateId,
            RelatedType = SetupProgressDrafts.RelatedType,
            RelatedId = instance.VisitInstanceId,
            Subject = rendered.Subject,
            BodyContent = rendered.Body,
            BodyFormat = EmailBodyFormat.HTML,
            Status = EmailDraftStatus.DRAFT,
            CreatedBy = userId,
            LastEditedBy = userId,
            CreatedAt = now,
        };

        try
        {
            _db.EmailDrafts.Add(draft);
            await _db.SaveChangesAsync(cancellationToken);

            var order = 0;
            foreach (var (recipient, type) in Flatten(envelope))
            {
                _db.EmailDraftRecipients.Add(new EmailDraftRecipient
                {
                    EmailDraftId = draft.EmailDraftId,
                    RecipientEmail = recipient.Email,
                    RecipientName = recipient.DisplayName,
                    RecipientType = type,
                    DisplayOrder = (uint)order++,
                    CreatedAt = now,
                });
            }

            _db.EmailDraftAttachments.Add(new EmailDraftAttachment
            {
                EmailDraftId = draft.EmailDraftId,
                FileId = reportFileId,
                AttachmentType = EmailAttachmentType.ATTACHMENT,
                DisplayName = artifact.FileName,
                DisplayOrder = 0,
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Deliberately logs the instance, not the file: a storage path or a signed URL in a log is a
            // way to reach the document without going through authorization.
            _logger.LogError(ex,
                "Failed to persist the setup-progress draft for visit instance {VisitInstanceId}; the generated report stays archived.",
                instance.VisitInstanceId);
            throw;
        }

        return new PrepareVisitSetupProgressEmailDraftResponse
        {
            DraftId = draft.EmailDraftId,
            ReusedExistingDraft = false,
            LanguageCode = language,
            ReportFileId = reportFileId,
            ReportFileName = artifact.FileName,
            ReportGeneratedAt = artifact.GeneratedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            BodyHtml = rendered.Body,
            Warnings = envelope.Warnings.ToList(),
        };
    }

    private static IEnumerable<(EmailRecipient Recipient, string Type)> Flatten(SetupProgressRecipients e)
    {
        foreach (var r in e.To) yield return (r, EmailRecipientTypes.To);
        foreach (var r in e.Cc) yield return (r, EmailRecipientTypes.Cc);
        foreach (var r in e.Bcc) yield return (r, EmailRecipientTypes.Bcc);
    }
}
