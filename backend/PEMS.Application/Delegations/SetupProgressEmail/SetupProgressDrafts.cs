using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>The mandatory Schedule Report attached to a setup-progress draft.</summary>
public readonly record struct SetupProgressReportAttachment(
    ulong EmailDraftAttachmentId, ulong FileId, string FileName, DateTime GeneratedAt);

/// <summary>
/// Shared lookups over setup-progress drafts.
///
/// <para>
/// The interesting one is <see cref="FindReportAttachmentAsync"/>. A draft may carry any number of files
/// the Host added, and exactly one of them may not be removed — so "which attachment is the report" has
/// to be a fact, not a naming convention. It is decided by joining to <c>documents</c>: the mandatory
/// attachment is the one whose file is archived under THIS delegation with category
/// <c>SCHEDULE_REPORT</c>. A Host who uploads a file called <c>PEMS_Schedule_Report_….pdf</c> therefore
/// does not acquire an undeletable attachment, and the real report keeps its protection even if its
/// display name is edited.
/// </para>
/// </summary>
public static class SetupProgressDrafts
{
    /// <summary>What a setup-progress draft (and the sent_emails row it becomes) is related to.</summary>
    public const string RelatedType = "VISIT_INSTANCE";

    /// <summary>The <c>documents.document_category</c> the archived Schedule Report carries.</summary>
    public const string ReportDocumentCategory = "SCHEDULE_REPORT";

    /// <summary>
    /// This Host's unsent setup-progress draft for this instance, if there is one. Ordered newest-first
    /// so a database that somehow holds two converges on the most recent rather than the oldest.
    /// </summary>
    public static Task<EmailDraft?> FindReusableAsync(
        IApplicationDbContext db, ulong visitInstanceId, ulong userId, CancellationToken ct)
        => db.EmailDrafts
            .Where(d => d.CreatedBy == userId
                        && d.RelatedType == RelatedType
                        && d.RelatedId == visitInstanceId
                        && d.Status == EmailDraftStatus.DRAFT
                        && d.EmailTemplate != null
                        && d.EmailTemplate.TemplateCode == VisitSetupProgressEmailGuard.TemplateCode)
            .OrderByDescending(d => d.EmailDraftId)
            .FirstOrDefaultAsync(ct);

    /// <summary>The draft's mandatory report attachment, or null when it no longer has one.</summary>
    public static async Task<SetupProgressReportAttachment?> FindReportAttachmentAsync(
        IApplicationDbContext db, ulong emailDraftId, ulong visitRequestId, CancellationToken ct)
    {
        var row = await (
            from a in db.EmailDraftAttachments
            join d in db.Documents on a.FileId equals d.FileId
            where a.EmailDraftId == emailDraftId
                  && d.OwnerType == "VISIT"
                  && d.OwnerId == visitRequestId
                  && d.DocumentCategory == ReportDocumentCategory
            orderby a.EmailDraftAttachmentId descending
            select new { a.EmailDraftAttachmentId, a.FileId, d.Title, d.CreatedAt })
            .FirstOrDefaultAsync(ct);

        return row is null
            ? null
            : new SetupProgressReportAttachment(
                row.EmailDraftAttachmentId, row.FileId, row.Title ?? string.Empty, row.CreatedAt);
    }

    /// <summary>
    /// The language a stored report was rendered in, read off the filename suffix the artifact service
    /// writes. Used when re-opening a draft this call did not render, so the response describes the
    /// content that actually exists rather than the language that was asked for.
    /// </summary>
    public static string LanguageOf(string? reportFileName)
        => reportFileName is not null
           && reportFileName.EndsWith("_EN.pdf", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : "vi";
}
