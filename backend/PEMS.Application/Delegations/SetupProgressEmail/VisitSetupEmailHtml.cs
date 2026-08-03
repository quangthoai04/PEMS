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
/// </summary>
public static class VisitSetupEmailHtml
{
    private const string Border = "#d1d5db";
    private const string HeadBg = "#f3f4f6";
    private const string Muted = "#6b7280";

    public static string Render(VisitSetupSnapshot s, string language)
    {
        bool en = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder();

        // ── 1. Overview ──────────────────────────────────────────────────────
        Section(sb, en ? "1. Visit overview" : "1. Thông tin chung");
        OpenTable(sb);
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
            OpenTable(sb);
            Head(sb, en
                ? new[] { "Time", "Activity", "Venue", "Party in charge" }
                : new[] { "Thời gian", "Nội dung", "Địa điểm", "Phụ trách" });
            foreach (var a in s.Report.Agenda)
            {
                var activity = Esc(a.Title);
                if (!string.IsNullOrWhiteSpace(a.Description))
                    activity += $"<br/><span style=\"color:{Muted};font-size:12px\">{Esc(a.Description!)}</span>";
                Row(sb, new[] { Esc(Window(a.StartTime, a.EndTime)), activity, Esc(a.Venue), Esc(a.Responsible) },
                    preEscaped: true);
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
            OpenTable(sb);
            Head(sb, en
                ? new[] { "Item", "Quantity", "Needed", "Status" }
                : new[] { "Hạng mục", "Số lượng", "Thời gian cần", "Trạng thái" });
            foreach (var l in s.Logistics)
            {
                Row(sb, new[]
                {
                    l.Title,
                    l.Quantity?.ToString() ?? "—",
                    Window(l.UsageStartAt, l.UsageEndAt),
                    StatusLabel(l.Status, en),
                });
            }
            CloseTable(sb);
        }

        // ── 6. Additional requests ───────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(s.TransportationNote))
        {
            Section(sb, en ? "6. Additional requests" : "6. Yêu cầu bổ sung");
            OpenTable(sb);
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

    private static void OpenTable(StringBuilder sb) =>
        sb.Append("<table role=\"presentation\" cellpadding=\"6\" cellspacing=\"0\" ")
          .Append($"style=\"border-collapse:collapse;width:100%;font-size:13px;border:1px solid {Border}\">");

    private static void CloseTable(StringBuilder sb) => sb.Append("</table>");

    private static void Head(StringBuilder sb, IReadOnlyList<string> cells)
    {
        sb.Append("<tr>");
        foreach (var c in cells)
            sb.Append($"<th align=\"left\" style=\"border:1px solid {Border};background:{HeadBg};font-size:12px\">")
              .Append(Esc(c)).Append("</th>");
        sb.Append("</tr>");
    }

    private static void Row(StringBuilder sb, IReadOnlyList<string> cells, bool preEscaped = false)
    {
        sb.Append("<tr>");
        foreach (var c in cells)
            sb.Append($"<td style=\"border:1px solid {Border};vertical-align:top\">")
              .Append(preEscaped ? c : Esc(c)).Append("</td>");
        sb.Append("</tr>");
    }

    private static void KeyValue(StringBuilder sb, string key, string value) =>
        sb.Append("<tr>")
          .Append($"<td style=\"border:1px solid {Border};background:{HeadBg};width:34%;font-weight:bold\">")
          .Append(Esc(key)).Append("</td>")
          .Append($"<td style=\"border:1px solid {Border}\">").Append(Esc(value)).Append("</td>")
          .Append("</tr>");

    private static void People(StringBuilder sb, IReadOnlyList<Queries.ExportScheduleReport.ScheduleReportPersonDto> people, bool en)
    {
        OpenTable(sb);
        Head(sb, en ? new[] { "Name", "Organisation", "Role" } : new[] { "Họ tên", "Đơn vị", "Vai trò" });
        foreach (var p in people)
            Row(sb, new[] { p.FullName, p.Organization ?? "—", p.RoleLabel ?? "—" });
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

    private static string Esc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
