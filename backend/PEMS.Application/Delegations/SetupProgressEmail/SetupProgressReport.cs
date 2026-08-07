using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>The Schedule Report carried by a setup-progress email, when it carries one.</summary>
public readonly record struct SetupProgressReportAttachment(
    ulong FileId, string FileName, DateTime GeneratedAt);

/// <summary>
/// What makes a file THE Schedule Report of a visit.
///
/// <para>
/// It has to be a fact rather than a naming convention, because the send is told which files to attach by
/// the browser. The report is the file archived under THIS delegation with category
/// <c>SCHEDULE_REPORT</c> — read from <c>documents</c>, so a Host who uploads their own
/// <c>PEMS_Schedule_Report_….pdf</c> is not mistaken for the generated one, and the real report is still
/// recognised if its display name is edited.
/// </para>
/// <para>
/// The question this answers used to be "may this send proceed"; it is now "is this the file this screen
/// generated", which is what decides whether a send-time storage failure gets the message about rebuilding
/// it. Carrying the report is no longer a condition of sending — see
/// <see cref="SendVisitSetupProgressEmailCommandHandler"/>.
/// </para>
/// <para>
/// This used to look the answer up by joining <c>email_draft_attachments</c>: "which attachment of this
/// draft is the report". With no draft to join to, the same question is asked of the file id directly, and
/// the answer is the same one — the join to <c>documents</c> was always the part that decided it.
/// </para>
/// </summary>
public static class SetupProgressReport
{
    /// <summary>What a setup-progress email (and the sent_emails row) is related to.</summary>
    public const string RelatedType = "VISIT_INSTANCE";

    /// <summary>The <c>documents.document_category</c> the archived Schedule Report carries.</summary>
    public const string ReportDocumentCategory = "SCHEDULE_REPORT";

    /// <summary>
    /// The report metadata for <paramref name="fileId"/>, or null when that file is not a Schedule Report
    /// of this visit.
    ///
    /// <para>
    /// Null is the answer for BOTH "no such document row" and "a document belonging to another visit", and
    /// deliberately so: neither is this visit's generated report, and telling the two apart in a message
    /// would confirm the existence of a file the caller may not see.
    /// </para>
    /// </summary>
    public static async Task<SetupProgressReportAttachment?> FindReportAsync(
        IApplicationDbContext db, ulong fileId, ulong visitRequestId, CancellationToken ct)
    {
        var row = await db.Documents
            .Where(d => d.FileId == fileId
                        && d.OwnerType == "VISIT"
                        && d.OwnerId == visitRequestId
                        && d.DocumentCategory == ReportDocumentCategory)
            .OrderByDescending(d => d.DocumentId)
            .Select(d => new { d.FileId, d.Title, d.CreatedAt })
            .FirstOrDefaultAsync(ct);

        return row is null
            ? null
            : new SetupProgressReportAttachment(row.FileId, row.Title ?? string.Empty, row.CreatedAt);
    }

    /// <summary>
    /// The language a stored report was rendered in, read off the filename suffix the artifact service
    /// writes.
    /// </summary>
    public static string LanguageOf(string? reportFileName)
        => reportFileName is not null
           && reportFileName.EndsWith("_EN.pdf", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : "vi";

    /// <summary>vi | en, from whatever the client asked for. Anything else is vi.</summary>
    public static string NormalizeLanguage(string? requested)
        => string.Equals(requested, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";
}
