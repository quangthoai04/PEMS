using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using PEMS.Application.Common.Interfaces;

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

    /// <summary>
    /// A single login-required link. <paramref name="note"/> is the sentence printed under the button and
    /// MUST describe what this particular recipient can do there — the default names the Department
    /// Leader's options because that is the flow this block was written for, and it is wrong for every
    /// other caller. The expense reminder passes its own; a caller that has nothing to add passes "".
    /// </summary>
    public static string DetailLinkBlock(
        string detailUrl,
        string label = "Mở yêu cầu để xử lý",
        string note = "Sau khi đăng nhập, Trưởng phòng có thể chấp nhận xử lý, từ chối yêu cầu, gán nhân sự hoặc đề xuất thay đổi. Thao tác xử lý yêu cầu yêu cầu đăng nhập hệ thống.")
    {
        var noteHtml = string.IsNullOrWhiteSpace(note)
            ? string.Empty
            : $@"<p style=""color:#6b7280;font-size:12px;margin-top:8px"">{HE(note)}</p>";

        return WrapActionBlock($@"<div style=""text-align:center;margin:24px 0"">
            <a href=""{HE(detailUrl)}"" style=""display:inline-block;background:#004c91;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">{HE(label)}</a>
        </div>{noteHtml}");
    }

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

    /// <summary>Host approve/reject of a Department change proposal (LOGISTICS_PROPOSAL_RESPONSE).</summary>
    public static string LogisticsProposalActionBlock(string approveUrl, string rejectUrl, string detailUrl, string detailLabel = "Xem chi tiết trong hệ thống")
    {
        return WrapActionBlock($@"<div style=""text-align:center;margin:24px 0"">
            <a href=""{HE(approveUrl)}"" style=""display:inline-block;background:#10b981;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Chấp nhận đề xuất</a>
            <a href=""{HE(rejectUrl)}"" style=""display:inline-block;background:#ef4444;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Từ chối đề xuất</a>
            <a href=""{HE(detailUrl)}"" style=""display:inline-block;background:#004c91;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">{HE(detailLabel)}</a>
        </div>
        <p style=""color:#6b7280;font-size:12px;margin-top:8px"">Lưu ý: <strong>Chấp nhận / Từ chối đề xuất</strong> là thao tác trực tiếp (không yêu cầu đăng nhập). <strong>Xem chi tiết</strong> yêu cầu đăng nhập hệ thống.</p>
        <p style=""color:#9ca3af;font-size:12px;margin-top:12px"">Liên kết phản hồi trực tiếp sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p>");
    }

    public static string LogisticsAssigneeActionBlock(string acceptUrl, string declineUrl, string detailUrl, string detailLabel = "Xem chi tiết trong hệ thống")
    {
        return WrapActionBlock($@"<div style=""text-align:center;margin:24px 0"">
            <a href=""{HE(acceptUrl)}"" style=""display:inline-block;background:#10b981;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Chấp nhận nhiệm vụ</a>
            <a href=""{HE(declineUrl)}"" style=""display:inline-block;background:#ef4444;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Từ chối nhiệm vụ</a>
            <a href=""{HE(detailUrl)}"" style=""display:inline-block;background:#004c91;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">{HE(detailLabel)}</a>
        </div>
        <p style=""color:#6b7280;font-size:12px;margin-top:8px"">Lưu ý: Thao tác <strong>Xem chi tiết / Đề xuất thay đổi</strong> yêu cầu đăng nhập hệ thống.</p>
        <p style=""color:#9ca3af;font-size:12px;margin-top:12px"">Liên kết phản hồi trực tiếp sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p>");
    }

    /// <summary>
    /// The reminder's "open the visit" button. A plain login-required link — no token, nothing to
    /// consume, nothing that grants access on its own. That is why a reminder's body is kept in full in
    /// the email history while the token-bearing mails have theirs stripped.
    /// </summary>
    public static string VisitDetailBlock(string detailUrl)
        => WrapActionBlock($@"<div style=""text-align:center;margin:24px 0"">
            <a href=""{HE(detailUrl)}"" style=""display:inline-block;background:#004c91;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 24px;border-radius:10px"">Xem chi tiết chuyến tiếp khách</a>
        </div>
        <p style=""color:#6b7280;font-size:12px"">Liên kết yêu cầu đăng nhập hệ thống PEMS.</p>");

    // ── Account action blocks ──
    // The URL is built by the backend from App:FrontendBaseUrl and, for confirmation, carries a
    // one-time token. Neither ever appears in the editable template body — a template author must not be
    // able to move, delete or fabricate the button that activates an account.

    /// <summary>
    /// Confirm-email button plus the same link in plain text, for clients that strip buttons.
    ///
    /// <para>
    /// The label comes from <c>EmailActionTemplates.ConfirmEmailLabel</c> so the send and the editor's
    /// preview cannot disagree about what the button says. It defaults here rather than being required,
    /// so the eight existing call sites keep working unchanged.
    /// </para>
    /// </summary>
    public static string ConfirmEmailBlock(string confirmUrl, string? label = null)
        => WrapActionBlock($@"<div style=""text-align:center;margin:24px 0"">
            <a href=""{HE(confirmUrl)}"" style=""display:inline-block;background:#004c91;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 24px;border-radius:10px"">{HE(label ?? EmailActionTemplates.ConfirmEmailLabel(EmailLanguages.Vi))}</a>
        </div>
        <p style=""color:#6b7280;font-size:12px;word-break:break-all"">Hoặc mở liên kết: {HE(confirmUrl)}</p>");

    /// <summary>Sign-in button for the activated-account notice. No token — just the portal address.</summary>
    public static string LoginBlock(string loginUrl)
        => WrapActionBlock($@"<div style=""text-align:center;margin:24px 0"">
            <a href=""{HE(loginUrl)}"" style=""display:inline-block;background:#004c91;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 24px;border-radius:10px"">Đăng nhập Internal Portal</a>
        </div>
        <p style=""color:#6b7280;font-size:12px"">Đăng nhập bằng chính địa chỉ email này qua SSO / Google / FEID.</p>");

    /// <summary>
    /// The claim/transfer invitation button: one link to a page that asks the reader to sign in with the
    /// Google account of THIS address before accepting or declining. Deliberately not a direct-execute
    /// link — opening a mail preview must not accept a role on somebody's behalf.
    /// </summary>
    /// <summary>
    /// The operational-contact invitation's two actions.
    ///
    /// <para>
    /// Both links open a CONFIRMATION PAGE; neither performs the action. That is not a UX preference,
    /// it is the reason the flow is safe: Outlook, Gmail and mail-security scanners prefetch URLs, so a
    /// link that accepted on GET would be answered by a virus scanner rather than by the person. The
    /// page validates the token read-only, shows the current visit summary, and only a click there
    /// POSTs the answer.
    /// </para>
    /// <para>
    /// Two separate tokens, one per intended action, so the page knows which button was pressed without
    /// trusting a query parameter — and so a scanner that follows the decline link cannot be used to
    /// accept. Signing in is not required for either.
    /// </para>
    /// </summary>
    public static string ContactRoleInvitationBlock(string acceptUrl, string declineUrl, DateTime expiresAt)
        => WrapActionBlock($@"<div style=""text-align:center;margin:24px 0"">
            <a href=""{HE(acceptUrl)}"" style=""display:inline-block;background:#10b981;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Xác nhận</a>
            <a href=""{HE(declineUrl)}"" style=""display:inline-block;background:#ef4444;color:#fff;text-decoration:none;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Từ chối</a>
        </div>
        <p style=""color:#6b7280;font-size:12px;word-break:break-all"">Hoặc mở liên kết: {HE(acceptUrl)}</p>
        <p style=""color:#6b7280;font-size:12px"">Liên kết có hiệu lực đến <strong>{expiresAt:HH:mm dd/MM/yyyy}</strong> và chỉ dùng được một lần. Bạn <strong>không cần đăng nhập</strong> — hệ thống sẽ mở trang xác nhận để bạn xem thông tin mới nhất trước khi quyết định.</p>
        <p style=""color:#6b7280;font-size:12px"">Thông tin chuyến thăm có thể được người đăng ký cập nhật. Vui lòng xem thông tin mới nhất tại trang xác nhận.</p>");

    // ── Disabled action blocks (preview only — no live URLs/tokens) ──

    /// <summary>
    /// Preview stand-in for <see cref="ConfirmEmailBlock"/> — no token is minted, and no anchor exists,
    /// so a click has nowhere to go.
    /// </summary>
    public static string DisabledConfirmEmailBlock(string? label = null)
        => $@"<div style=""text-align:center;margin:24px 0"">
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 24px;border-radius:10px"">{HE(label ?? EmailActionTemplates.ConfirmEmailLabel(EmailLanguages.Vi))}</span>
        </div>";

    /// <summary>Preview stand-in for <see cref="LoginBlock"/>.</summary>
    public static string DisabledLoginBlock()
        => @"<div style=""text-align:center;margin:24px 0"">
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 24px;border-radius:10px"">Đăng nhập Internal Portal</span>
        </div>";


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
    /// Preview stand-in for a template that carries <c>{{actionBlock}}</c> but has no entry in
    /// <see cref="EmailActionTemplates"/> (G11 / R-106).
    ///
    /// <para>
    /// Nine of the fourteen templates that use the placeholder are in this position, and before this
    /// existed they could not be previewed at all: with no trusted block supplied, <c>{{actionBlock}}</c>
    /// survived rendering and the renderer refused the whole message. An operator editing one of those
    /// templates got an error instead of a preview, which is a poor way to learn that the registry is
    /// incomplete.
    /// </para>
    /// <para>
    /// It deliberately names NO business outcome — no "Chấp nhận", no "Từ chối", no "Xác nhận". What
    /// buttons those emails should carry is a product decision that has not been made, and inventing
    /// labels here would put an answer to it in front of operators as though it had been. The block shows
    /// where the action area sits and says who fills it in; nothing more.
    /// </para>
    /// </summary>
    public static string DisabledUnspecifiedActionBlock(string language)
    {
        var text = EmailLanguages.Normalize(language) == EmailLanguages.En
            ? "Action area — the system adds the buttons and their one-time links when the email is sent."
            : "Khu vực nút thao tác — hệ thống gắn nút và liên kết một lần khi gửi email thật.";

        return $@"<div style=""text-align:center;margin:24px 0"">
            <span style=""display:inline-block;border:1px dashed #9aa6b2;color:#6b7280;font-size:13px;padding:12px 22px;border-radius:10px"">{HE(text)}</span>
        </div>";
    }

    /// <summary>
    /// Stand-in for the setup-progress tables in a preview.
    ///
    /// <para>
    /// A preview has no visit, so there is no setup to tabulate — and inventing one would show an
    /// operator a delegation, guests and a schedule that do not exist, which is worse than showing
    /// nothing: it reads as real data and invites edits to content the template does not control.
    /// This says where the tables go and where they come from, and stops there.
    /// </para>
    /// <para>
    /// It must be supplied even though it is inert. Without it the placeholder stays unresolved and the
    /// renderer fails the preview closed, so the template-management screen would refuse to preview the
    /// one template that uses it.
    /// </para>
    /// </summary>
    public static string DisabledSetupSummaryBlock(string language)
    {
        var text = EmailLanguages.Normalize(language) == EmailLanguages.En
            ? "Setup tables — visit overview, delegation members, FPT participants, schedule and preparation status. The system builds them from the visit's data when the email is sent."
            : "Bảng thông tin chuẩn bị — thông tin chung, danh sách khách, thành phần phía FPT, lịch trình và trạng thái chuẩn bị. Hệ thống dựng từ dữ liệu chuyến thăm khi gửi email thật.";

        return $@"<div style=""margin:18px 0"">
            <span style=""display:block;border:1px dashed #9aa6b2;color:#6b7280;font-size:13px;padding:14px 18px;border-radius:10px"">{HE(text)}</span>
        </div>";
    }

    /// <summary>
    /// Preview stand-in for <see cref="ContactRoleInvitationBlock"/> — same single button, same words, no
    /// anchor and no minted claim URL.
    /// </summary>
    public static string DisabledContactRoleInvitationBlock()
        => @"<div style=""text-align:center;margin:24px 0"">
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Xác nhận</span>
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Từ chối</span>
        </div>";

    /// <summary>
    /// Preview stand-in for <see cref="LogisticsProposalActionBlock"/>. Kept separate from
    /// <see cref="DisabledLogisticsActionBlock"/> because the Host is answering a change PROPOSAL: the
    /// real mail says "Chấp nhận đề xuất / Từ chối đề xuất", and showing "Đồng ý / Từ chối / Hành động
    /// khác" would promise an operator a set of buttons this template does not send.
    /// </summary>
    public static string DisabledLogisticsProposalActionBlock(string detailLabel = "Xem chi tiết trong hệ thống")
    {
        return $@"<div style=""text-align:center;margin:24px 0"">
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Chấp nhận đề xuất</span>
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Từ chối đề xuất</span>
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">{HE(detailLabel)}</span>
        </div>";
    }

    /// <summary>
    /// Preview stand-in for <see cref="LogisticsAssigneeActionBlock"/> (BUG-09) — 3 buttons
    /// (Accept/Decline/Detail), matching the real send exactly instead of falling back to the generic
    /// 2-button <see cref="DisabledAcceptDeclineBlock"/>.
    /// </summary>
    public static string DisabledLogisticsAssigneeActionBlock(string detailLabel = "Xem chi tiết trong hệ thống")
    {
        return $@"<div style=""text-align:center;margin:24px 0"">
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Chấp nhận nhiệm vụ</span>
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">Từ chối nhiệm vụ</span>
            <span style=""display:inline-block;background:#9aa6b2;color:#fff;font-weight:bold;font-size:14px;padding:12px 22px;border-radius:10px;margin:6px"">{HE(detailLabel)}</span>
        </div>";
    }

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

        // Refuse malformed markers rather than doing a best-effort removal. Step 0 below deletes
        // everything between START and END; if the markers do not form exactly one well-ordered block,
        // that deletion removes the wrong span — and the caller most in need of it is the email history,
        // where "the wrong span" means a live one-time link stays in a table every internal role can read.
        AssertActionBlockStructure(html);

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
        //
        //    The system-block node is EXEMPT, and the exemption is load-bearing: that node is an empty
        //    <div> by design (see EmailSystemBlockNodes), so without the exclusion this step would delete
        //    the author's chosen position for the action area and the block would fall back to being
        //    appended at the end — reintroducing, silently, the exact defect the node exists to fix.
        s = Regex.Replace(
            s,
            @"<(p|div)(?![^>]*\bdata-system-block\b)(?:\s[^>]*)?>(?:\s|\||&nbsp;|&amp;|<br\s*/?>)*</\1>",
            string.Empty, RegexOptions.IgnoreCase);

        return s.Trim();
    }

    // ── Action-block integrity ──

    /// <summary>Matches either canonical marker, however the surrounding comment syntax is written.</summary>
    private static readonly Regex ActionBlockMarkerPattern =
        new(@"PEMS_ACTION_BLOCK_(START|END)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Regex for pulling the real URLs back out of a built action block.</summary>
    private static readonly Regex HrefPattern =
        new(@"href\s*=\s*""([^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>True when the content mentions an action-block marker at all.</summary>
    public static bool ContainsActionBlockMarker(string? html)
        => !string.IsNullOrEmpty(html) && ActionBlockMarkerPattern.IsMatch(html!);

    /// <summary>
    /// Asserts the content contains either no action-block markers, or exactly one START…END pair in that
    /// order. Anything else — a stray END, an unclosed START, nesting, or a second block — is refused
    /// with <see cref="EmailErrorCodes.ActionBlockMalformed"/>.
    ///
    /// <para>
    /// Zero markers is valid and common: template bodies and freshly authored content have none. What is
    /// never valid is a shape where "the action block" does not identify a single unambiguous span, since
    /// both the stripper and the history policy are defined in terms of that span.
    /// </para>
    /// </summary>
    public static void AssertActionBlockStructure(string? html)
    {
        if (string.IsNullOrEmpty(html)) return;

        var open = false;
        var completed = 0;

        foreach (Match m in ActionBlockMarkerPattern.Matches(html!))
        {
            var isStart = string.Equals(m.Groups[1].Value, "START", StringComparison.OrdinalIgnoreCase);

            if (isStart)
            {
                if (open) throw Malformed("khối hành động lồng nhau");
                open = true;
            }
            else
            {
                if (!open) throw Malformed("dấu kết thúc khối hành động đứng trước dấu bắt đầu");
                open = false;
                completed++;
            }
        }

        if (open) throw Malformed("khối hành động thiếu dấu kết thúc");
        if (completed > 1) throw Malformed($"có {completed} khối hành động (chỉ cho phép 1)");
    }

    private static PEMS.Application.Common.Exceptions.BusinessRuleException Malformed(string detail)
        => new($"Nội dung email có cấu trúc khối hành động không hợp lệ: {detail}.",
            EmailErrorCodes.ActionBlockMalformed);

    /// <summary>
    /// Every URL a built trusted block links to, in the exact spelling the block writes into the body
    /// (HTML-encoded), plus the decoded spelling. Used to PROVE afterwards that a stripped body no longer
    /// carries the one-time link — a check that needs the real URLs and therefore cannot be done by
    /// pattern-matching the body alone.
    /// </summary>
    public static IReadOnlyList<string> ExtractActionUrls(
        System.Collections.Generic.IReadOnlyDictionary<string, string>? trustedBlocks)
    {
        var urls = new System.Collections.Generic.List<string>();
        if (trustedBlocks is null) return urls;

        foreach (var html in trustedBlocks.Values)
        {
            if (string.IsNullOrEmpty(html)) continue;

            foreach (Match m in HrefPattern.Matches(html))
            {
                var encoded = m.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(encoded)) continue;

                urls.Add(encoded);
                var decoded = WebUtility.HtmlDecode(encoded);
                if (!string.Equals(decoded, encoded, StringComparison.Ordinal)) urls.Add(decoded);
            }
        }

        return urls;
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
