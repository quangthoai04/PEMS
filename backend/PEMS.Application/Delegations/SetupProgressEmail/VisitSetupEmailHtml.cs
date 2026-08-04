using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace PEMS.Application.Delegations.SetupProgressEmail;

/// <summary>
/// Renders the setup snapshot as the HTML tables that go in the setup-progress email body.
///
/// <para>
/// This is a TRUSTED block: its output is injected into the rendered body verbatim, without the
/// HTML-encoding that template variables get. That is the whole reason it exists — a table cannot be
/// passed as a variable — and it is also why every value that comes from the database goes through
/// <see cref="Esc"/> on the way in. A delegation named <c>&lt;script&gt;</c> is a string here, not markup.
/// </para>
/// <para>
/// Styling is inline and table-based on purpose. Mail clients strip &lt;style&gt; blocks and most of
/// them do not lay out flex or grid, so anything cleverer renders as an unformatted pile of text in
/// exactly the clients guests are most likely to use.
/// </para>
/// <para>
/// <b>Every table declares its column widths.</b> Without them the browser and the mail client fall back
/// to automatic layout, which sizes each column from its content: one long agenda description takes the
/// surplus and the remaining columns collapse toward their <i>minimum</i> content width — the longest
/// single word. In Vietnamese that turns a heading into a column one syllable wide, so "Thời gian" comes
/// out stacked as "Thời"/"gian" and the table reads as broken. The widths are declared three times over
/// on purpose: <c>&lt;colgroup&gt;</c> for browsers, a <c>width</c> attribute on every cell for Outlook's
/// Word engine (which honours neither <c>colgroup</c> nor <c>table-layout</c>), and
/// <c>table-layout:fixed</c> so a single long value can no longer widen its column at the others' expense.
/// Wrapping is set on the cells to match: with a fixed layout an unbreakable token would otherwise spill
/// out of the cell instead of shrinking it.
/// </para>
/// </summary>
public static class VisitSetupEmailHtml
{
    /// <summary>
    /// The one border colour every table uses. Darkened from the old <c>#d1d5db</c>: at that value the
    /// grid all but disappeared in clients that render on an off-white background, so a table read as a
    /// run of unaligned text rather than as a table.
    /// </summary>
    private const string Border = "#374151";

    private const string HeadBg = "#f3f4f6";
    private const string Muted = "#6b7280";

    /// <summary>
    /// Padding lives here rather than on the table's <c>cellpadding</c> attribute: CSS padding is the one
    /// both Gmail and Outlook apply consistently, and setting <c>cellpadding</c> as well would double it
    /// in the clients that honour both.
    /// </summary>
    private const string CellPad = "padding:6px 8px";

    /// <summary>
    /// Wrapping that is safe in a fixed layout, stated as three properties because each answers a
    /// different question.
    ///
    /// <para>
    /// <c>overflow-wrap:break-word</c> is the one that does the work: a long unbroken value (an address, a
    /// pasted URL) breaks rather than pushing the column wider, which under a fixed layout it cannot do
    /// and would instead overflow the border.
    /// </para>
    /// <para>
    /// <c>word-break:normal</c> is stated EXPLICITLY, and is a change from the previous
    /// <c>word-break:break-word</c>. The old value let a break fall anywhere, so ordinary Vietnamese wrapped
    /// mid-syllable — "Phòng Hợp tác Quốc tế" coming out as "Phòng Hợp tá"/"c Quốc tế" — for no benefit,
    /// since <c>overflow-wrap</c> already covers the only case that needs forcing. (<c>break-all</c> is the
    /// same fault, harder: it is never used here.)
    /// </para>
    /// <para>
    /// <c>white-space:normal</c> undoes a <c>nowrap</c> some clients inherit onto table cells, which would
    /// otherwise stop wrapping altogether and blow the column out.
    /// </para>
    /// </summary>
    private const string Wrap = "white-space:normal;overflow-wrap:break-word;word-break:normal";

    private static string DataCell(string width) =>
        $"border:1px solid {Border};{CellPad};text-align:left;vertical-align:top;{Wrap};width:{width}";

    private static string HeadCell(string width) =>
        $"border:1px solid {Border};background:{HeadBg};{CellPad};font-size:12px;font-weight:bold;"
        + $"text-align:left;vertical-align:top;{Wrap};width:{width}";

    // Column allocations. They sum to 100% in every table so a fixed layout has nothing left to guess at.
    private static readonly string[] PeopleWidths = { "32%", "43%", "25%" };

    /// <summary>
    /// Thời gian / Nội dung / Địa điểm / Phụ trách — four columns, and only ever four.
    ///
    /// <para>
    /// "Nội dung" is the widest because it carries the activity title AND its description in one cell;
    /// "Địa điểm" was widened from 18% because a venue is a phrase ("Hội trường tầng 3, toà Beta") and at
    /// the old share it wrapped onto three lines beside a half-empty description column.
    /// </para>
    /// </summary>
    private static readonly string[] AgendaWidths = { "18%", "42%", "22%", "18%" };
    private static readonly string[] LogisticsWidths = { "34%", "12%", "30%", "24%" };
    private static readonly string[] KeyValueWidths = { "34%", "66%" };

    public static string Render(VisitSetupSnapshot s, string language)
    {
        bool en = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder();

        // ── 1. Overview ──────────────────────────────────────────────────────
        Section(sb, en ? "1. Visit overview" : "1. Thông tin chung");
        OpenTable(sb, KeyValueWidths);
        KeyValue(sb, en ? "Delegation" : "Tên đoàn", s.Report.DelegationName);
        KeyValue(sb, en ? "Campus" : "Cơ sở tiếp đón", s.CampusName);
        KeyValue(sb, en ? "Time" : "Thời gian", Window(s.Report.PlannedStartAt, s.Report.PlannedEndAt));
        KeyValue(sb, en ? "Location" : "Địa điểm", s.Report.Location);
        if (!string.IsNullOrWhiteSpace(s.Report.Purpose))
            KeyValue(sb, en ? "Purpose" : "Mục đích tham quan", s.Report.Purpose!);
        if (!string.IsNullOrWhiteSpace(s.WorkingContent))
            KeyValue(sb, en ? "Working content" : "Nội dung làm việc", s.WorkingContent!);
        CloseTable(sb);

        // ── 2. Guest side ────────────────────────────────────────────────────
        // "Valid data" means a named person: a row with no name is a half-finished form entry, and
        // listing it back as a blank line reads like the delegation lost somebody.
        var guests = s.Report.GuestSide.Where(g => !string.IsNullOrWhiteSpace(g.FullName)).ToList();
        Section(sb, en ? "2. Delegation members" : "2. Danh sách khách");
        if (guests.Count == 0) Empty(sb, en ? "No guest has been recorded yet." : "Chưa có thông tin khách.");
        else People(sb, guests, en);

        // ── 3. FPT side ──────────────────────────────────────────────────────
        Section(sb, en ? "3. FPT participants" : "3. Thành phần phía FPT");
        if (s.Report.FptSide.Count == 0)
            Empty(sb, en ? "No participant has confirmed yet." : "Chưa có thành phần tham gia xác nhận.");
        else People(sb, s.Report.FptSide, en);

        // ── 4. Agenda ────────────────────────────────────────────────────────
        Section(sb, en ? "4. Detailed schedule" : "4. Lịch trình chi tiết");
        if (s.Report.Agenda.Count == 0)
        {
            Empty(sb, en ? "The schedule has not been set up yet." : "Chưa có nội dung lịch trình được thiết lập.");
        }
        else
        {
            OpenTable(sb, AgendaWidths);
            Head(sb, en
                ? new[] { "Time", "Activity", "Venue", "Party in charge" }
                : new[] { "Thời gian", "Nội dung", "Địa điểm", "Phụ trách" },
                AgendaWidths);
            foreach (var a in s.Report.Agenda)
            {
                // Title and description are ONE cell, not two columns: they are one activity, and
                // splitting them produced a row whose cell count no longer matched the header.
                // <strong> + <div> rather than <br/> + <span> so the description is its own block —
                // Outlook collapses the margin on an inline run and the two ran together as one line.
                var activity = $"<strong>{Esc(a.Title)}</strong>";
                if (!string.IsNullOrWhiteSpace(a.Description))
                    activity += $"<div style=\"color:{Muted};font-size:12px;margin-top:2px\">{Esc(a.Description!)}</div>";

                Row(sb, new[]
                    {
                        Esc(Window(a.StartTime, a.EndTime)),
                        activity,
                        Esc(Fallback(a.Venue)),
                        Esc(Fallback(a.Responsible)),
                    },
                    AgendaWidths, preEscaped: true);
            }
            CloseTable(sb);
        }

        // ── 5. Preparation items ─────────────────────────────────────────────
        Section(sb, en ? "5. Preparation status" : "5. Trạng thái chuẩn bị");
        if (s.Logistics.Count == 0)
        {
            Empty(sb, en ? "No preparation item has been registered yet." : "Chưa có hạng mục chuẩn bị nào.");
        }
        else
        {
            OpenTable(sb, LogisticsWidths);
            Head(sb, en
                ? new[] { "Item", "Quantity", "Needed", "Status" }
                : new[] { "Hạng mục", "Số lượng", "Thời gian cần", "Trạng thái" },
                LogisticsWidths);
            foreach (var l in s.Logistics)
            {
                Row(sb, new[]
                {
                    l.Title,
                    l.Quantity?.ToString() ?? "—",
                    Window(l.UsageStartAt, l.UsageEndAt),
                    StatusLabel(l.Status, en),
                }, LogisticsWidths);
            }
            CloseTable(sb);
        }

        // ── 6. Additional requests ───────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(s.TransportationNote))
        {
            Section(sb, en ? "6. Additional requests" : "6. Yêu cầu bổ sung");
            OpenTable(sb, KeyValueWidths);
            KeyValue(sb, en ? "Transport" : "Di chuyển", s.TransportationNote!);
            CloseTable(sb);
        }

        sb.Append($"<p style=\"color:{Muted};font-size:12px;margin-top:16px\">")
          .Append(en ? "Data updated at: " : "Dữ liệu được cập nhật lúc: ")
          .Append("<strong>").Append(Esc(s.GeneratedAt.ToString("HH:mm dd/MM/yyyy"))).Append("</strong>. ")
          .Append(en
              ? "The attached Schedule Report is generated from this same snapshot."
              : "Báo cáo Lịch trình đính kèm được tạo từ đúng dữ liệu này.")
          .Append("</p>");

        return sb.ToString();
    }

    // ── Building blocks ─────────────────────────────────────────────────────

    private static void Section(StringBuilder sb, string title) =>
        sb.Append("<h3 style=\"font-size:14px;margin:18px 0 6px;color:#004c91\">").Append(Esc(title)).Append("</h3>");

    private static void Empty(StringBuilder sb, string text) =>
        sb.Append($"<p style=\"color:{Muted};font-size:13px;margin:0 0 8px\"><em>").Append(Esc(text)).Append("</em></p>");

    /// <summary>
    /// Opens a table and declares its columns. <paramref name="widths"/> drives the
    /// <c>&lt;colgroup&gt;</c>; the same values are repeated on the cells by <see cref="Head"/> and
    /// <see cref="Row"/> for the clients that ignore it.
    /// </summary>
    private static void OpenTable(StringBuilder sb, IReadOnlyList<string> widths)
    {
        sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" ")
          .Append("style=\"border-collapse:collapse;width:100%;table-layout:fixed;")
          .Append($"font-size:13px;border:1px solid {Border}\">");

        sb.Append("<colgroup>");
        foreach (var w in widths) sb.Append($"<col style=\"width:{w}\" width=\"{w}\"/>");
        sb.Append("</colgroup>");

        // The single row group every table uses. Opened here rather than by each caller so a table can
        // never end up with a row outside one — see Head() for why that stopped being survivable.
        sb.Append("<tbody>");
    }

    private static void CloseTable(StringBuilder sb) => sb.Append("</tbody></table>");

    /// <summary>
    /// The header row — a normal <c>&lt;tr&gt;</c> of <c>&lt;td&gt;</c>s carrying the header styling,
    /// NOT <c>&lt;thead&gt;</c>/<c>&lt;th&gt;</c>.
    ///
    /// <para>
    /// It used to be the semantic markup, and that broke the composer. This block is written into an
    /// email draft that the Host then opens in the rich-text editor, and the editor's document model has
    /// no notion of a table header: it drops <c>&lt;thead&gt;</c> and <c>&lt;th&gt;</c> while keeping the
    /// text inside them, so the header cells collapsed into one run — "Họ tênĐơn vịVai trò" — sitting in
    /// a single cell above a body that still had its columns. The data rows were unaffected because
    /// <c>&lt;td&gt;</c> is understood, which is exactly what made the fault look like a styling glitch
    /// rather than a lost tag.
    /// </para>
    /// <para>
    /// Styled cells are also the safer choice for the email itself: Outlook's Word renderer treats
    /// <c>&lt;th&gt;</c> inconsistently and ignores much of what is set on it, so header formatting
    /// carried by the cell's own style survives more clients than the semantic tag would. Nothing is
    /// lost in meaning here — the table is already <c>role="presentation"</c>, i.e. declared to assistive
    /// technology as layout rather than data.
    /// </para>
    /// </summary>
    private static void Head(StringBuilder sb, IReadOnlyList<string> cells, IReadOnlyList<string> widths)
    {
        sb.Append("<tr>");
        for (var i = 0; i < cells.Count; i++)
            sb.Append($"<td align=\"left\" width=\"{widths[i]}\" style=\"{HeadCell(widths[i])}\">")
              .Append(Esc(cells[i])).Append("</td>");
        sb.Append("</tr>");
    }

    private static void Row(
        StringBuilder sb, IReadOnlyList<string> cells, IReadOnlyList<string> widths, bool preEscaped = false)
    {
        sb.Append("<tr>");
        for (var i = 0; i < cells.Count; i++)
            sb.Append($"<td width=\"{widths[i]}\" style=\"{DataCell(widths[i])}\">")
              .Append(preEscaped ? cells[i] : Esc(cells[i])).Append("</td>");
        sb.Append("</tr>");
    }

    private static void KeyValue(StringBuilder sb, string key, string value) =>
        sb.Append("<tr>")
          .Append($"<td width=\"{KeyValueWidths[0]}\" style=\"{HeadCell(KeyValueWidths[0])};font-size:13px\">")
          .Append(Esc(key)).Append("</td>")
          .Append($"<td width=\"{KeyValueWidths[1]}\" style=\"{DataCell(KeyValueWidths[1])}\">")
          .Append(Esc(value)).Append("</td>")
          .Append("</tr>");

    private static void People(StringBuilder sb, IReadOnlyList<Queries.ExportScheduleReport.ScheduleReportPersonDto> people, bool en)
    {
        OpenTable(sb, PeopleWidths);
        Head(sb, en ? new[] { "Name", "Organisation", "Role" } : new[] { "Họ tên", "Đơn vị", "Vai trò" },
            PeopleWidths);
        foreach (var p in people)
            Row(sb, new[] { p.FullName, p.Organization ?? "—", p.RoleLabel ?? "—" }, PeopleWidths);
        CloseTable(sb);
    }

    private static string Window(DateTime? from, DateTime? to)
    {
        if (from is null && to is null) return "—";
        if (from is null) return $"→ {to:HH:mm dd/MM/yyyy}";
        if (to is null) return $"{from:HH:mm dd/MM/yyyy}";
        return from.Value.Date == to.Value.Date
            ? $"{from:HH:mm}–{to:HH:mm} {from:dd/MM/yyyy}"
            : $"{from:HH:mm dd/MM/yyyy} – {to:HH:mm dd/MM/yyyy}";
    }

    /// <summary>
    /// Business-readable status text. The raw enum member is not shown: "REQUESTED" is an internal
    /// workflow token, and a guest reading it learns less than they would from a sentence.
    /// </summary>
    private static string StatusLabel(string status, bool en) => status?.ToUpperInvariant() switch
    {
        "REQUESTED" => en ? "Requested" : "Đã gửi yêu cầu",
        "ACCEPTED" or "ASSIGNED" or "IN_PROGRESS" => en ? "In progress" : "Đang chuẩn bị",
        "READY" or "COMPLETED" or "RETURNED" => en ? "Ready" : "Đã sẵn sàng",
        "REJECTED" or "CANCELLED" => en ? "Not arranged" : "Không bố trí",
        _ => en ? "Being coordinated" : "Đang phối hợp",
    };

    /// <summary>
    /// The one placeholder for "nobody has filled this in yet", matching what every other table in this
    /// block already renders for an absent value.
    ///
    /// <para>
    /// It replaces a literal <c>"FPT University"</c> that <see cref="Queries.ExportScheduleReport"/> used
    /// to substitute for a blank "Phụ trách". That was not a fallback but an assertion: it told the guest
    /// that a named party was running an item nobody had been assigned to, on every unassigned row of the
    /// schedule, so the column carried no information and looked like a stray cell in a table that was
    /// otherwise per-activity.
    /// </para>
    /// </summary>
    private static string Fallback(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value!;

    private static string Esc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
