using System.Threading;
using System.Threading.Tasks;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Queries.ExportScheduleReport;

/// <summary>One rendered "Báo cáo Lịch trình" — the bytes plus the name they should be filed under.</summary>
/// <param name="Content">The PDF.</param>
/// <param name="FileName">
/// <c>PEMS_Schedule_Report_{requestCode}_{yyyyMMdd_HHmm}.pdf</c>, carrying the language suffix when the
/// report was rendered in English, so the two are distinguishable in a folder listing.
/// </param>
/// <param name="LanguageCode">The language actually rendered — <c>vi</c> or <c>en</c>, never null.</param>
/// <param name="GeneratedAt">Wall-clock Vietnam time the render happened (PEMS stores no UTC).</param>
public sealed record ScheduleReportArtifact(
    byte[] Content,
    string FileName,
    string LanguageCode,
    System.DateTime GeneratedAt);

/// <summary>
/// Builds and stores the schedule report for ONE campus instance.
///
/// <para>
/// It exists because two callers now need the same PDF from the same data: the download on the
/// VisitProcess page, and the setup-progress email that must attach it. Leaving the render inside the
/// download handler would have meant the email either duplicating the build/translate/render chain — two
/// implementations of one document, free to drift — or downloading its own report through the browser
/// and posting the bytes back up, which turns a server-side artifact into something the client can
/// substitute.
/// </para>
/// <para>
/// <see cref="RenderAsync"/> and <see cref="StoreAsync"/> are deliberately separate. Rendering has no
/// side effects, so a caller that only needs bytes (a preview, a retry) creates no file and no
/// <c>documents</c> row; storing is what archives the report and hands back the <c>file_id</c> an email
/// attachment can reference. The download handler archives best-effort; the email flow cannot, because a
/// draft with no attachment row is not a draft anyone should be able to send.
/// </para>
/// </summary>
public interface IScheduleReportArtifactService
{
    /// <summary>
    /// Renders the report for <paramref name="instance"/>. The caller must have already loaded the
    /// instance with its <c>VisitRequest</c> (and Partner) and its agendas, and must have checked the
    /// caller's scope — this service renders, it does not authorise.
    /// </summary>
    /// <exception cref="Common.Exceptions.ValidationException">The instance has no assigned host.</exception>
    Task<ScheduleReportArtifact> RenderAsync(
        VisitRequestCampus instance, string? languageCode, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads the artifact into the delegation's own "Tài liệu / Theo đoàn khách" folder and records the
    /// matching <c>documents</c> row, returning the new <c>file_id</c>.
    ///
    /// <para>
    /// Unlike the aggregate dashboard exports, this report belongs to one delegation, so it is filed
    /// under that delegation rather than in the flat "Report" folder.
    /// </para>
    /// </summary>
    Task<ulong> StoreAsync(
        ScheduleReportArtifact artifact,
        VisitRequestCampus instance,
        ulong actorUserId,
        CancellationToken cancellationToken);
}
