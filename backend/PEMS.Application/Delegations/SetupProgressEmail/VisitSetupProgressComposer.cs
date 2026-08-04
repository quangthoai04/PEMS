using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Contact;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>One rendered setup-progress message, and the report that describes the same moment.</summary>
public sealed record ComposedSetupProgressEmail(
    string Subject,
    string BodyHtml,
    ulong ReportFileId,
    string ReportFileName,
    DateTime GeneratedAt,
    string LanguageCode);

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
    private readonly IEmailContactResolver _contacts;

    public VisitSetupProgressComposer(
        IApplicationDbContext db,
        IEmailTemplateRenderer renderer,
        IScheduleReportArtifactService reports,
        IVisitFormReadService formRead,
        IEmailContactResolver contacts)
    {
        _db = db;
        _renderer = renderer;
        _reports = reports;
        _formRead = formRead;
        _contacts = contacts;
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

        // The reply contact is resolved here and travels into the body the Host reviews. Scoped to this
        // instance, so a multi-campus request cannot borrow another campus's Host.
        var contact = await _contacts.ResolveAsync(
            new EmailContactRequest(
                VisitSetupProgressEmailGuard.TemplateCode,
                language,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                SenderUserId: hostUserId),
            cancellationToken);

        var rendered = await _renderer.RenderAsync(new EmailRenderRequest(
            VisitSetupProgressEmailGuard.TemplateCode,
            language,
            VisitSetupProgressEmailGuard.BuildVariables(
                instance, delegationName, snapshot.CampusName, hostName),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EmailTrustedBlocks.SetupSummaryBlock] = VisitSetupEmailHtml.Render(snapshot, language),
                [EmailTrustedBlocks.ContactInformationBlock] = contact.BlockHtml,
            }),
            cancellationToken);

        // Archived only after the body rendered: a failure while rendering must not leave a stored PDF
        // behind for a message that was never composed. The reverse order is not available to us — storage
        // has no rollback — so the ordering is what limits a failure to an unreferenced file, never a
        // message promising a report that is not there.
        var reportFileId = await _reports.StoreAsync(artifact, instance, hostUserId, cancellationToken);

        return new ComposedSetupProgressEmail(
            rendered.Subject, rendered.Body, reportFileId, artifact.FileName, artifact.GeneratedAt, language);
    }
}
