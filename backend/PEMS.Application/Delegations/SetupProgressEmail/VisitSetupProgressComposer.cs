using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Sender;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>
/// One rendered setup-progress message, and — when it could be archived — the report that describes the
/// same moment.
///
/// <para>
/// <see cref="ReportFileId"/> is nullable because the report is a DEFAULT attachment, not a required one.
/// The message is what this operation produces; the PDF is something it also produces when storage is
/// working. Null and <see cref="ReportWarning"/> travel together: exactly one of "here is the report" and
/// "here is why there isn't one" is ever populated, and neither an id of 0 nor an empty file name is used
/// to mean absence.
/// </para>
/// </summary>
public sealed record ComposedSetupProgressEmail(
    string Subject,
    string BodyHtml,
    ulong? ReportFileId,
    string? ReportFileName,
    DateTime GeneratedAt,
    string LanguageCode,
    string? ReportWarning = null);

/// <summary>
/// Renders the Host's setup-progress message from the template and the live setup, and archives the
/// Schedule Report the body's tables were built from.
///
/// <para>
/// It exists because <c>prepare</c> and <c>đồng bộ</c> are the same operation with a different amount of
/// context around it: both render the body from <c>email_templates</c>, both build the report, and the two
/// must agree exactly — a refresh that produced a body the prepare would not have produced would replace
/// the Host's message with a different one.
/// </para>
/// <para>
/// <b>One read, both halves.</b> The report is rendered FIRST and the body's HTML tables are built from
/// that same snapshot, so the message and its attachment describe one moment rather than two reads either
/// side of a save. Rebuilding only the PDF was worse than useless: the tables would still describe the old
/// state while the attachment described the new one, with nothing to say which half was right.
/// </para>
/// <para>
/// <b>Archiving the report is best-effort; composing the message is not.</b> The two used to fail as one,
/// and the consequence was out of all proportion to the cause: a Google Drive connection that had expired
/// made the whole "Gửi cập nhật chuẩn bị" action unusable — no composer, no message, nothing the Host
/// could send — because the LAST step of building an optional attachment threw. Storage is the only part
/// of this method that depends on a third party, so it is the only part allowed to fail quietly: the
/// message is returned with no report and a sentence saying why.
/// </para>
/// <para>
/// Rendering the report is still blocking, and that is not an oversight. The DTO it produces is what the
/// body's own tables are built from — <see cref="VisitSetupSnapshotBuilder"/> refuses an artifact without
/// it rather than issuing a second read — so a failure there is a failure to obtain the email's content,
/// which is the one thing this operation cannot do without. It touches no external service: the partner
/// logo is the only file it reads and that read is already best-effort.
/// </para>
/// </summary>
public interface IVisitSetupProgressComposer
{
    Task<ComposedSetupProgressEmail> ComposeAsync(
        VisitRequestCampus instance, ulong hostUserId, string? languageCode, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IVisitSetupProgressComposer"/>
public sealed class VisitSetupProgressComposer : IVisitSetupProgressComposer
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IScheduleReportArtifactService _reports;
    private readonly IVisitFormReadService _formRead;
    private readonly IEmailSenderVariableResolver _senders;
    private readonly ILogger<VisitSetupProgressComposer> _logger;

    public VisitSetupProgressComposer(
        IApplicationDbContext db,
        IEmailTemplateRenderer renderer,
        IScheduleReportArtifactService reports,
        IVisitFormReadService formRead,
        IEmailSenderVariableResolver senders,
        ILogger<VisitSetupProgressComposer> logger)
    {
        _db = db;
        _renderer = renderer;
        _reports = reports;
        _formRead = formRead;
        _senders = senders;
        _logger = logger;
    }

    public async Task<ComposedSetupProgressEmail> ComposeAsync(
        VisitRequestCampus instance, ulong hostUserId, string? languageCode, CancellationToken cancellationToken)
    {
        var language = SetupProgressReport.NormalizeLanguage(languageCode);

        var content = await _formRead.ResolveCampusFormContentAsync(
            instance.VisitRequest, new[] { instance.VisitInstanceId }, cancellationToken);
        var delegationName = content.TryGetValue(instance.VisitInstanceId, out var detail)
            ? detail.DelegationName
            : string.Empty;

        var host = await _db.Users
            .Where(u => u.UserId == hostUserId).Select(u => new { u.FullName })
            .FirstOrDefaultAsync(cancellationToken);
        var hostName = host?.FullName ?? string.Empty;

        // The report FIRST: its data is also what the body's HTML tables are built from.
        var artifact = await _reports.RenderAsync(instance, language, cancellationToken);
        var snapshot = await VisitSetupSnapshotBuilder.BuildAsync(_db, instance, artifact, cancellationToken);

        // The sender is the Host — the person who prepares this message and presses send — so their name,
        // role and address are substituted into the body they are about to review. Resolved here rather
        // than left to the dispatcher because this composer renders directly: the Host reviews and edits
        // THIS body, so it has to already read the way the sent one will.
        var variables = VisitSetupProgressEmailGuard.BuildVariables(
            instance, delegationName, snapshot.CampusName, hostName);

        var sender = await _senders.ResolveAsync(
            hostUserId, VisitSetupProgressEmailGuard.TemplateCode, cancellationToken);

        foreach (var pair in sender.ToVariableValues()) variables[pair.Key] = pair.Value;

        var rendered = await _renderer.RenderAsync(new EmailRenderRequest(
            VisitSetupProgressEmailGuard.TemplateCode,
            language,
            variables,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EmailTrustedBlocks.SetupSummaryBlock] = VisitSetupEmailHtml.Render(snapshot, language),
            }),
            cancellationToken);

        // Archived only after the body rendered: a failure while rendering must not leave a stored PDF
        // behind for a message that was never composed. The reverse order is not available to us — storage
        // has no rollback — so the ordering is what limits a failure to an unreferenced file, never a
        // message promising a report that is not there.
        ulong? reportFileId = null;
        string? reportWarning = null;
        try
        {
            reportFileId = await _reports.StoreAsync(artifact, instance, hostUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A cancelled request is not a storage failure, and answering it with "the report could not
            // be created" would report an outage that did not happen.
            throw;
        }
        catch (BusinessRuleException ex)
        {
            // Everything Drive refuses arrives here: an expired or revoked grant, a folder the account
            // cannot see, an outage, a missing configuration. StoreAsync uploads BEFORE it records, and
            // FileUploadService deletes its own upload if the files insert fails, so a failure on this
            // path leaves no documents row and no files row pointing at nothing.
            reportWarning = DescribeReportFailure(ex.ErrorCode);
            _logger.LogWarning(ex,
                "Schedule report could not be archived for visit instance {VisitInstanceId} ({Code}); "
                + "the setup-progress message is returned without it.",
                instance.VisitInstanceId, ex.ErrorCode ?? "(none)");
        }
        catch (ValidationException ex)
        {
            // StoreAsync's own refusals — an instance whose campus code cannot be resolved, an upload
            // that answered with no usable file id. Same treatment: the Host still gets their message.
            reportWarning = DescribeReportFailure(errorCode: null);
            _logger.LogWarning(ex,
                "Schedule report could not be archived for visit instance {VisitInstanceId}; "
                + "the setup-progress message is returned without it.",
                instance.VisitInstanceId);
        }

        return new ComposedSetupProgressEmail(
            rendered.Subject, rendered.Body, reportFileId, reportFileId is null ? null : artifact.FileName,
            artifact.GeneratedAt, language, reportWarning);
    }

    /// <summary>
    /// What to tell the Host when the report could not be archived.
    ///
    /// <para>
    /// Two sentences rather than one, because the two situations need different things from different
    /// people. A grant that has expired or been revoked is repaired by an administrator reconnecting the
    /// account and will not fix itself, so the message must not invite the Host to keep pressing a button;
    /// everything else is a bad moment worth trying again. Neither of them says "chọn lại ngôn ngữ", which
    /// was the advice on screen for a cause that has nothing to do with the language.
    /// </para>
    /// </summary>
    private static string DescribeReportFailure(string? errorCode) => errorCode switch
    {
        GoogleDriveErrorCodes.TokenExpired
            or GoogleDriveErrorCodes.AuthFailed
            or GoogleDriveErrorCodes.ConfigMissing
            or GoogleDriveErrorCodes.NotConnected =>
            "Kết nối Google Drive cần được xác thực lại. Báo cáo Lịch trình chưa được đính kèm. "
            + "Anh/chị vẫn có thể gửi email, hoặc thử tạo lại báo cáo sau khi quản trị viên kết nối lại.",

        _ =>
            "Không thể tạo Báo cáo Lịch trình do kho tệp (Google Drive) hiện không khả dụng. "
            + "Anh/chị vẫn có thể gửi email mà không đính kèm báo cáo, hoặc bấm “Đồng bộ dữ liệu mới nhất” "
            + "để thử tạo lại.",
    };
}
