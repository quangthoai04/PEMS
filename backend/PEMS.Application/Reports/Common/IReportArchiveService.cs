namespace PEMS.Application.Reports.Common;

/// <summary>
/// Archives an already-generated report export (PDF/Excel/CSV) to Google Drive and creates a
/// matching <c>documents</c> row (OwnerType=REPORT) so it shows up on "Quản lý tài liệu" — without
/// changing the export handler's own download response. These reports are date-range/campus
/// aggregates across MANY delegations, so — unlike VISIT/PARTNER/LOGISTICS documents — they are
/// never nested under a delegation folder: they land in one flat "Report" folder directly under the
/// shared Drive root, optionally scoped to a campus via the <c>documents.campus_id</c> column only
/// (not a Drive subfolder).
/// </summary>
public interface IReportArchiveService
{
    /// <summary>
    /// Best-effort: a Drive/DB hiccup here must never block the user from getting their report file —
    /// callers should swallow failures (already logged) and still return the export bytes.
    /// </summary>
    Task ArchiveAsync(
        byte[] content,
        string fileName,
        string contentType,
        string documentCategory,
        ulong? campusId,
        ulong userId,
        CancellationToken cancellationToken);
}
