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

    // ── Canonical action block markers ──
    // Every backend-injected action block is wrapped in these so the cleaner can find + remove an
    // already-injected block before re-injecting (idempotent on re-send/re-edit). The block is the
    // ONE source of truth for accept/decline/etc buttons — templates must not carry their own.
    public const string ActionBlockStart = "<!-- PEMS_ACTION_BLOCK_START -->";
    public const string ActionBlockEnd = "<!-- PEMS_ACTION_BLOCK_END -->";
    private static string WrapActionBlock(string inner) => ActionBlockStart + inner + ActionBlockEnd;

    /// <summary>System action/detail-URL variables the backend ALWAYS injects itself (via the canonical
    /// action block) — they are NEVER rendered into the editable body. Includes detailUrl: in the action
    /// email flows (logistics request / assignee) the detail button lives in the action block, so the
    /// body's {{detailUrl}} must be stripped. Reminders use their OWN renderer + provide a real detailUrl,
    /// so they are unaffected by this set.</summary>
    private static readonly System.Collections.Generic.HashSet<string> ActionUrlVarNames =
        new(System.StringComparer.OrdinalIgnoreCase)
        {
            "acceptUrl", "declineUrl", "negotiateUrl", "approveProposalUrl",
            "rejectProposalUrl", "confirmBorrowUrl", "confirmReturnUrl", "assignUrl",
            "detailUrl", "DETAIL_URL",
        };

    /// <summary>Regex alternation of every action/detail-URL var name used by the cleaner.</summary>
    private const string ActionVarAlternation =
        "acceptUrl|declineUrl|negotiateUrl|approveProposalUrl|rejectProposalUrl|confirmBorrowUrl|confirmReturnUrl|assignUrl|detailUrl|detail_url";

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
        return WrapActionBlock($@"<div style=""text-align:center;margin:24px 0"">
            <a href=""{HE(acceptUrl)}"" style=""display:inline-block;background:#10b981;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Chấp nhận</a>
            <a href=""{HE(declineUrl)}"" style=""display:inline-block;background:#ef4444;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Từ chối</a>
            {assign}
        </div>{assignNote}
        <p style=""color:#9ca3af;font-size:12px;margin-top:12px"">Liên kết phản hồi sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p>");
    }

    public static string DetailLinkBlock(string detailUrl, string label = "Mở yêu cầu để xử lý")
        => WrapActionBlock($@"<div style=""text-align:center;margin:24px 0"">
            <a href=""{HE(detailUrl)}"" style=""display:inline-block;background:#004c91;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">{HE(label)}</a>
        </div>
        <p style=""color:#6b7280;font-size:12px;margin-top:8px"">Sau khi đăng nhập, Trưởng phòng có thể chấp nhận xử lý, từ chối yêu cầu, gán nhân sự hoặc đề xuất thay đổi. Thao tác xử lý yêu cầu yêu cầu đăng nhập hệ thống.</p>");

    public static string LogisticsActionBlock(string acceptUrl, string declineUrl, string detailUrl, string detailLabel = "Hành động khác")
    {
        return WrapActionBlock($@"<div style=""text-align:center;margin:24px 0"">
            <a href=""{HE(acceptUrl)}"" style=""display:inline-block;background:#10b981;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Đồng ý</a>
            <a href=""{HE(declineUrl)}"" style=""display:inline-block;background:#ef4444;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Từ chối</a>
            <a href=""{HE(detailUrl)}"" style=""display:inline-block;background:#004c91;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">{HE(detailLabel)}</a>
        </div>
        <p style=""color:#6b7280;font-size:12px;margin-top:8px"">Lưu ý: <strong>Đồng ý / Từ chối</strong> là thao tác trực tiếp (không yêu cầu đăng nhập). <strong>Hành động khác</strong> (như gán nhân sự, thảo luận thêm) yêu cầu đăng nhập hệ thống.</p>
        <p style=""color:#9ca3af;font-size:12px;margin-top:12px"">Liên kết phản hồi trực tiếp sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p>");
    }

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

    public static string DisabledLogisticsActionBlock(string detailLabel = "Hành động khác")
    {
        return $@"<div style=""text-align:center;margin:24px 0"">
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Đồng ý</span>
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Từ chối</span>
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">{HE(detailLabel)}</span>
        </div>";
    }

    /// <summary>
    /// Reduces a template/edited body to its editable message content by removing every system action
    /// artifact, so the backend can inject exactly ONE canonical action block. Idempotent and safe on
    /// already-clean content. Removes, in order:
    ///   0. any already-injected canonical block (PEMS_ACTION_BLOCK_START..END);
    ///   1. &lt;a&gt; whose href targets a system action — a {{xxxUrl}} placeholder (raw OR URL-encoded
    ///      %7B%7B..%7D%7D) OR the real public email-action endpoint (/public/email-actions/) a user
    ///      may have pasted;
    ///   2. leftover bare placeholders (raw + URL-encoded);
    ///   3. legacy plain-text button pairs joined by a pipe (e.g. "Chấp nhận tham gia | Từ chối");
    ///   4. a lone pipe separator stranded between tags after its anchors were removed;
    ///   5. a &lt;p&gt;/&lt;div&gt; left holding only separators/whitespace.
    /// Normal user links (school website, Google Drive, docs) are untouched — only action hrefs match.
    /// </summary>
    public static string StripActionArtifacts(string html)
    {
        if (string.IsNullOrEmpty(html)) return html ?? string.Empty;
        var s = html;

        // 0) Drop any previously-injected canonical action block (idempotent re-send/re-edit).
        s = Regex.Replace(s,
            @"<!--\s*PEMS_ACTION_BLOCK_START\s*-->.*?<!--\s*PEMS_ACTION_BLOCK_END\s*-->",
            string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 1) Drop anchors whose href targets a system action (placeholder raw/encoded, or token URL).
        const string hrefNeedle =
            @"(?:\{\{\s*(?:" + ActionVarAlternation + @")\s*\}\}"
            + @"|%7[Bb]%7[Bb]\s*(?:" + ActionVarAlternation + @")\s*%7[Dd]%7[Dd]"
            + @"|/public/email-actions/|api/public/email-actions)";
        s = Regex.Replace(s,
            @"<a\b[^>]*\bhref\s*=\s*(?:""[^""]*" + hrefNeedle + @"[^""]*""|'[^']*" + hrefNeedle + @"[^']*'|[^\s>]*" + hrefNeedle + @"[^\s>]*)[^>]*>.*?</a>",
            string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 1b) Drop anchors whose VISIBLE TEXT is a known system "view detail" label AND whose href is
        //     NOT a real external link — catches a legacy detail anchor whose {{detailUrl}} href was
        //     blanked to a fallback string. Real user links (href=http/https/mailto/tel) are kept.
        s = Regex.Replace(s,
            @"<a\b(?![^>]*\bhref\s*=\s*[""']?(?:https?:|mailto:|tel:))[^>]*>\s*(?:Xem yêu cầu hậu cần|Xem chi tiết yêu cầu|Xem chi tiết trong PEMS|Xem chi tiết|View logistics request|View request details|View request|View detail|View details|Details|Detail)\s*</a>",
            string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 2) Drop leftover bare placeholders (raw + URL-encoded).
        s = Regex.Replace(s, @"\{\{\s*(?:" + ActionVarAlternation + @")\s*\}\}", string.Empty, RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"%7[Bb]%7[Bb]\s*(?:" + ActionVarAlternation + @")\s*%7[Dd]%7[Dd]", string.Empty, RegexOptions.IgnoreCase);

        // 3) Drop legacy plain-text button pairs joined by a pipe (the old text buttons).
        s = Regex.Replace(s,
            @"(?:&nbsp;|\s)*(?:Chấp nhận tham gia|Chấp nhận phối hợp|Chấp nhận|Đồng ý|Nhận nhiệm vụ|Accept invitation|Accept coordination|Accept assignment|Accept)(?:&nbsp;|\s)*\|(?:&nbsp;|\s)*(?:Từ chối|Decline)(?:(?:&nbsp;|\s)*\|(?:&nbsp;|\s)*(?:Gán nhân sự|Assign staff))?",
            string.Empty, RegexOptions.IgnoreCase);

        // 4) Clean a lone pipe stranded between tags after its anchors were removed.
        s = Regex.Replace(s, @">(?:\s|&nbsp;)*\|(?:\s|&nbsp;|\|)*<", "><", RegexOptions.IgnoreCase);

        // 5) Collapse a block (<p>/<div>) now holding only separators/whitespace.
        s = Regex.Replace(s, @"<(p|div)(?:\s[^>]*)?>(?:\s|\||&nbsp;|&amp;|<br\s*/?>)*</\1>", string.Empty, RegexOptions.IgnoreCase);

        return s.Trim();
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

    /// <summary>
    /// Replaces {{variable}} placeholders with context values, applying fallbacks for missing values.
    /// </summary>
    public static string RenderTemplate(string template, System.Collections.Generic.Dictionary<string, string> context, string contextType = "GENERAL")
    {
        if (string.IsNullOrEmpty(template)) return template;

        // Rich editors (Quill) URL-encode braces inside href attributes, so a template link like
        // href="{{AcceptUrl}}" comes back as href="%7B%7BAcceptUrl%7D%7D". Normalize the encoded
        // double-brace form back to {{ }} (both upper/lower hex) before matching so it still resolves.
        template = Regex.Replace(template, "%7[Bb]\\s*%7[Bb]", "{{");
        template = Regex.Replace(template, "%7[Dd]\\s*%7[Dd]", "}}");

        var fallbacks = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "coordinationNote", "Không có ghi chú phối hợp." },
            { "quantity", "Chưa nhập" },
            { "usageStartAt", "Chưa chọn thời gian" },
            { "usageEndAt", "Chưa chọn thời gian" },
            { "departmentName", "Chưa chọn phòng ban" },
            { "departmentHeadName", "Chưa chọn phòng ban" },
            { "departmentHeadEmail", "Chưa chọn phòng ban" },
            { "headName", "Chưa chọn phòng ban" },
            { "headEmail", "Chưa chọn phòng ban" },
            { "logisticsItemTitle", "Chưa có thông tin" },
            { "logisticsItemType", "Chưa có thông tin" },
            { "logisticsDescription", "Chưa có thông tin" }
        };

        return Regex.Replace(template, @"\{\{\s*([\w]+)\s*\}\}", match =>
        {
            var key = match.Groups[1].Value;

            // System action-URL vars are NEVER substituted into the editable body — leave the
            // placeholder intact so StripActionArtifacts removes the whole anchor afterwards (the
            // backend injects the real action block). This prevents the generic "Chưa có thông tin"
            // fallback from blanking the placeholder and leaving the legacy anchor behind (duplicate).
            if (ActionUrlVarNames.Contains(key))
                return match.Value;

            // Check provided context first (case-insensitive)
            foreach (var kvp in context)
            {
                if (string.Equals(kvp.Key, key, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Value) && kvp.Value != "Chưa có thông tin")
                        return kvp.Value;
                }
            }

            // Apply specific fallback based on contextType
            if (contextType == "PARTICIPANT_INVITATION")
            {
                if (string.Equals(key, "coordinationNote", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "LogisticsNote", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Log warning here if possible, but we don't have logger inject. Just return empty string to strip it.
                    return string.Empty;
                }
            }

            if (contextType == "LOGISTICS_REQUEST" || contextType == "GENERAL")
            {
                if (string.Equals(key, "coordinationNote", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "LogisticsNote", System.StringComparison.OrdinalIgnoreCase))
                {
                    return "Không có ghi chú phối hợp.";
                }
            }

            if (fallbacks.TryGetValue(key, out var fallbackValue))
                return fallbackValue;
                
            // Generic fallback so no token is exposed
            return "Chưa có thông tin";
        });
    }
}
