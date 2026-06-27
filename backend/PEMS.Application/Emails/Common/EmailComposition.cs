using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Composes transactional emails so the user-editable content stays separate from the
/// system-controlled action block (accept/decline buttons with real one-time tokens, or a
/// login-required "view detail" link). The editable content is what the host may rewrite; the action
/// block is always injected by the backend at send time so the real tokens can never be broken.
/// </summary>
public static class EmailComposition
{
    public static string HE(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>Wrap inner content HTML in the branded PEMS card (the final sent body).</summary>
    public static string BrandedShell(string innerHtml) => $@"<!DOCTYPE html>
<html lang=""vi""><head><meta charset=""UTF-8""></head>
<body style=""font-family:Arial,sans-serif;background:#f4f6f9;margin:0;padding:20px"">
  <div style=""max-width:560px;margin:auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08)"">
    <div style=""background:linear-gradient(135deg,#004c91,#013565);padding:28px 32px"">
      <h1 style=""color:#fff;margin:0;font-size:22px"">PEMS — Campus Visit</h1>
      <p style=""color:#b3c8e8;margin:6px 0 0;font-size:13px"">FPT University</p>
    </div>
    <div style=""padding:32px;color:#374151;font-size:14px"">{innerHtml}</div>
    <div style=""background:#f9fafb;padding:16px 32px;text-align:center"">
      <p style=""color:#9ca3af;font-size:11px;margin:0"">© 2026 PEMS — FPT University. Không trả lời email này.</p>
    </div>
  </div>
</body></html>";

    // ── Real action blocks (with live URLs) ──

    public static string AcceptDeclineBlock(string acceptUrl, string declineUrl, string? assignUrl = null)
    {
        var assign = string.IsNullOrEmpty(assignUrl) ? string.Empty
            : $@"<a href=""{HE(assignUrl)}"" style=""display:inline-block;background:#004c91;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Gán nhân sự</a>";
        var assignNote = string.IsNullOrEmpty(assignUrl) ? string.Empty
            : @"<p style=""color:#6b7280;font-size:12px;margin-top:8px"">Lưu ý: thao tác <strong>Gán nhân sự</strong> yêu cầu đăng nhập hệ thống.</p>";
        return $@"<div style=""text-align:center;margin:24px 0"">
            <a href=""{HE(acceptUrl)}"" style=""display:inline-block;background:#10b981;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Chấp nhận</a>
            <a href=""{HE(declineUrl)}"" style=""display:inline-block;background:#ef4444;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Từ chối</a>
            {assign}
        </div>{assignNote}
        <p style=""color:#9ca3af;font-size:12px;margin-top:12px"">Liên kết phản hồi sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p>";
    }

    public static string DetailLinkBlock(string detailUrl, string label = "Mở yêu cầu để xử lý")
        => $@"<div style=""text-align:center;margin:24px 0"">
            <a href=""{HE(detailUrl)}"" style=""display:inline-block;background:#004c91;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">{HE(label)}</a>
        </div>
        <p style=""color:#6b7280;font-size:12px;margin-top:8px"">Sau khi đăng nhập, Trưởng phòng có thể chấp nhận xử lý, từ chối yêu cầu, gán nhân sự hoặc đề xuất thay đổi. Thao tác xử lý yêu cầu yêu cầu đăng nhập hệ thống.</p>";

    // ── Disabled action blocks (preview only — no live URLs/tokens) ──

    public static string DisabledAcceptDeclineBlock(bool withAssign = false)
    {
        var assign = withAssign
            ? @"<span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Gán nhân sự</span>"
            : string.Empty;
        return $@"<div style=""text-align:center;margin:24px 0"">
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Chấp nhận</span>
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Từ chối</span>
            {assign}
        </div>";
    }

    public static string DisabledDetailLinkBlock(string label = "Mở yêu cầu để xử lý")
        => $@"<div style=""text-align:center;margin:24px 0"">
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">{HE(label)}</span>
        </div>";

    /// <summary>
    /// Removes any &lt;a&gt; that points at an action placeholder ({{acceptUrl}}/{{declineUrl}}/
    /// {{assignUrl}}/{{detailUrl}}) plus the bare placeholders, so a template body can be reduced to
    /// its editable message content. Idempotent and safe on already-clean content.
    /// </summary>
    public static string StripActionArtifacts(string html)
    {
        if (string.IsNullOrEmpty(html)) return html ?? string.Empty;
        // Drop anchors whose href references an action placeholder.
        var noAnchors = Regex.Replace(html,
            @"<a\b[^>]*href\s*=\s*[""']?\{\{\s*(acceptUrl|declineUrl|assignUrl|detailUrl)\s*\}\}[""']?[^>]*>.*?</a>",
            string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        // Drop separators / leftover bare placeholders.
        noAnchors = Regex.Replace(noAnchors, @"\{\{\s*(acceptUrl|declineUrl|assignUrl|detailUrl)\s*\}\}", string.Empty, RegexOptions.IgnoreCase);
        // Collapse a paragraph that is now empty/only separators.
        noAnchors = Regex.Replace(noAnchors, @"<p>(\s|\||&nbsp;|&amp;)*</p>", string.Empty, RegexOptions.IgnoreCase);
        return noAnchors.Trim();
    }

    /// <summary>
    /// Converts the editable message HTML to readable plain text for the "Xem trước email" editor, so
    /// the host never sees raw &lt;p&gt;/&lt;br&gt;/&lt;strong&gt; tags. Block tags become line breaks,
    /// list items get a bullet, remaining tags are stripped and HTML entities decoded.
    /// </summary>
    public static string HtmlToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var s = html!;
        s = Regex.Replace(s, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*li[^>]*>", "• ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</\s*(p|div|li|tr|ul|ol|h[1-6])\s*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", string.Empty);
        s = WebUtility.HtmlDecode(s);
        s = Regex.Replace(s, @"[ \t]+\n", "\n");
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        return s.Trim();
    }

    /// <summary>
    /// Converts host-edited plain text back to safe HTML: each blank-line-separated block becomes a
    /// &lt;p&gt; and single newlines become &lt;br&gt;. The text is HTML-encoded first so it carries no
    /// markup of its own (the result is still run through the HTML sanitizer before sending).
    /// </summary>
    public static string PlainTextToHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text!.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        var blocks = Regex.Split(normalized, @"\n{2,}");
        var sb = new StringBuilder();
        foreach (var block in blocks)
        {
            if (string.IsNullOrWhiteSpace(block)) continue;
            sb.Append("<p>").Append(HE(block).Replace("\n", "<br>")).Append("</p>");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Resolves the raw HTML body from an override, preferring the plain text the host actually edited
    /// (converted via <see cref="PlainTextToHtml"/>); falls back to a legacy bodyHtml if only that was sent.
    /// </summary>
    public static string ResolveEditableHtml(EmailOverride ov)
        => !string.IsNullOrWhiteSpace(ov.BodyText) ? PlainTextToHtml(ov.BodyText) : (ov.BodyHtml ?? string.Empty);
}
