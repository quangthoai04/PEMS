using System.Net;
using PEMS.Application.EmailActions;
using PEMS.Domain.Constants;

namespace PEMS.Api.Email;

/// <summary>
/// Server-rendered HTML for the public (no-login) email-action pages. The GET landing shows a
/// confirm button that POSTs back to the same URL — so a mail-scanner prefetch of the link can never
/// consume the one-time token. Self-contained inline styles (these open directly from an email).
/// </summary>
public static class EmailActionHtmlPages
{
    /// <summary>The GET landing: a confirm page for a still-valid token, or a terminal message.
    /// ACCEPT shows a confirm button with an optional note field; DECLINE shows a form requiring a
    /// reason (so a GET click never mutates data — only the POST does).</summary>
    public static string RenderLanding(EmailActionInfoResult info)
    {
        if (info.Status != EmailActionViewStatuses.Valid)
            return RenderMessage(TerminalTitle(info.Status, info.Context, info.Action, info.CurrentResponse),
                TerminalBody(info.Status, info.Context, info.CurrentResponse), TerminalAccent(info.Status));

        var isAccept = info.Action == "ACCEPT";
        var accent = isAccept ? "#10b981" : "#ef4444";
        var wording = PresentationFor(info.Context);

        var details = $@"
      <div style=""background:#f0f7ff;border-left:4px solid #004c91;border-radius:8px;padding:16px 20px;margin:20px 0;text-align:left"">
        <ul style=""margin:0;padding-left:20px;color:#374151;font-size:13px;line-height:1.7"">
          <li><strong>Đoàn khách:</strong> {HE(info.DelegationName)}</li>
          <li><strong>Cơ sở:</strong> {HE(info.CampusName)}</li>
          <li><strong>Thời gian:</strong> {HE(info.PlannedTimeText)}</li>
          <li><strong>{wording.RoleLineLabel}:</strong> {HE(info.ParticipantRoleLabel)}</li>
        </ul>
      </div>";

        // DECLINE: render the reason form. The reason is mandatory and only the POST applies it.
        if (!isAccept)
        {
            var declineContent = $@"
      <p style=""color:#374151;font-size:15px"">Xin chào <strong>{HE(info.RecipientName)}</strong>,</p>
      <p style=""color:#374151;font-size:14px"">{wording.DeclineIntro}</p>
      {details}
      {DeclineForm(null, null, wording.DeclinePlaceholder, wording.DeclineButton)}";
            return Layout(wording.DeclineTitle, declineContent, accent);
        }

        // ACCEPT: confirm button, plus an optional note the invitee can leave for the host.
        var content = $@"
      <p style=""color:#374151;font-size:15px"">Xin chào <strong>{HE(info.RecipientName)}</strong>,</p>
      <p style=""color:#374151;font-size:14px"">{wording.AcceptIntro}</p>
      {details}
      <form method=""post"" style=""margin-top:8px;text-align:left"">
        <label style=""display:block;font-size:13px;font-weight:bold;color:#374151;margin-bottom:6px"">Ghi chú thêm (không bắt buộc)</label>
        <textarea name=""note"" maxlength=""1000"" rows=""3"" placeholder=""Ví dụ: Tôi sẽ có mặt sớm 15 phút...""
          style=""width:100%;box-sizing:border-box;padding:12px;border:1px solid #d1d5db;border-radius:10px;font-size:14px;font-family:inherit;resize:vertical;outline:none""></textarea>
        <button type=""submit"" style=""margin-top:14px;display:block;width:100%;background:{accent};color:#fff;border:none;cursor:pointer;font-weight:bold;font-size:15px;padding:14px 28px;border-radius:10px"">{HE(wording.AcceptButton)}</button>
      </form>
      <p style=""color:#9ca3af;font-size:12px;margin-top:14px"">Liên kết này chỉ sử dụng được một lần.</p>";

        return Layout(wording.AcceptTitle, content, accent);
    }

    /// <summary>
    /// Wording for one public context, replacing the single <c>isLogisticsRequest</c> boolean that used
    /// to collapse every non-logistics-request context (direct invite, Department-Staff assignment,
    /// logistics assignee) into the same participant-invitation copy. Unknown/legacy contexts (e.g. a
    /// surviving LOGISTICS_PROPOSAL_RESPONSE token — proposals are Portal-only, spec BUG-07) fall back
    /// to the direct-invitation wording only because RenderLanding is never reached for them: GET/POST
    /// both resolve those to a terminal status before this is called.
    /// </summary>
    private readonly record struct Presentation(
        string RoleLineLabel, string AcceptTitle, string AcceptIntro, string AcceptButton,
        string DeclineTitle, string DeclineIntro, string DeclinePlaceholder, string DeclineButton,
        string AlreadyRespondedBody);

    private static Presentation PresentationFor(string? context) => context switch
    {
        EmailActionContexts.ParticipationAssignmentResponse => new(
            RoleLineLabel: "Nhiệm vụ",
            AcceptTitle: "Xác nhận nhận nhiệm vụ",
            AcceptIntro: "Bạn sắp <strong>xác nhận nhận</strong> nhiệm vụ tham gia hỗ trợ tiếp khách được phân công dưới đây.",
            AcceptButton: "Xác nhận nhận nhiệm vụ",
            DeclineTitle: "Từ chối nhiệm vụ được phân công",
            DeclineIntro: "Bạn sắp <strong>từ chối</strong> nhiệm vụ tham gia hỗ trợ tiếp khách được phân công dưới đây. Vui lòng cho biết lý do từ chối để bộ phận điều phối cập nhật kế hoạch.",
            DeclinePlaceholder: "Ví dụ: Tôi bận lịch cá nhân vào thời gian này...",
            DeclineButton: "Gửi phản hồi từ chối",
            AlreadyRespondedBody: "Bạn đã phản hồi nhiệm vụ được phân công này rồi"),
        EmailActionContexts.LogisticsRequestResponse => new(
            RoleLineLabel: "Hạng mục",
            AcceptTitle: "Xác nhận tiếp nhận yêu cầu logistics",
            AcceptIntro: "Bạn sắp <strong>tiếp nhận</strong> yêu cầu hậu cần dưới đây.",
            AcceptButton: "Xác nhận tiếp nhận",
            DeclineTitle: "Từ chối yêu cầu logistics",
            DeclineIntro: "Bạn sắp <strong>từ chối</strong> yêu cầu hậu cần dưới đây. Vui lòng cho biết lý do từ chối để Host/bộ phận điều phối cập nhật kế hoạch.",
            DeclinePlaceholder: "Nhập lý do từ chối yêu cầu logistics...",
            DeclineButton: "Xác nhận từ chối",
            AlreadyRespondedBody: "Bạn đã phản hồi yêu cầu logistics này rồi"),
        EmailActionContexts.LogisticsAssigneeResponse => new(
            RoleLineLabel: "Nhiệm vụ hậu cần",
            AcceptTitle: "Xác nhận nhận nhiệm vụ hậu cần",
            AcceptIntro: "Bạn sắp <strong>xác nhận nhận</strong> nhiệm vụ hậu cần dưới đây.",
            AcceptButton: "Xác nhận nhận nhiệm vụ",
            DeclineTitle: "Từ chối nhiệm vụ hậu cần",
            DeclineIntro: "Bạn sắp <strong>từ chối</strong> nhiệm vụ hậu cần dưới đây. Vui lòng cho biết lý do từ chối để bộ phận điều phối cập nhật kế hoạch.",
            DeclinePlaceholder: "Nhập lý do từ chối nhiệm vụ hậu cần...",
            DeclineButton: "Xác nhận từ chối",
            AlreadyRespondedBody: "Bạn đã phản hồi nhiệm vụ hậu cần này rồi"),
        _ => new( // PARTICIPATION_RESPONSE (direct invitation) — also the safe default
            RoleLineLabel: "Vai trò",
            AcceptTitle: "Xác nhận chấp nhận lời mời",
            AcceptIntro: "Bạn sắp <strong>chấp nhận</strong> lời mời tham gia hỗ trợ tiếp khách dưới đây.",
            AcceptButton: "Xác nhận chấp nhận",
            DeclineTitle: "Từ chối lời mời tham gia",
            DeclineIntro: "Bạn sắp <strong>từ chối</strong> lời mời tham gia hỗ trợ tiếp khách dưới đây. Vui lòng cho biết lý do từ chối để bộ phận điều phối cập nhật kế hoạch.",
            DeclinePlaceholder: "Ví dụ: Tôi bận lịch cá nhân vào thời gian này...",
            DeclineButton: "Gửi phản hồi từ chối",
            AlreadyRespondedBody: "Bạn đã trả lời lời mời này rồi"),
    };

    /// <summary>The POST result page after the token is consumed.</summary>
    public static string RenderResult(EmailActionExecuteResult res)
    {
        // Decline reason missing/invalid → token was NOT consumed; re-render the form with the error.
        if (res.Status == EmailActionViewStatuses.ReasonRequired)
        {
            var wording = PresentationFor(res.Context);
            var content = $@"
      <p style=""color:#374151;font-size:14px"">Vui lòng cho biết lý do từ chối để bộ phận điều phối cập nhật kế hoạch.</p>
      {DeclineForm(res.SubmittedReason, res.Message, wording.DeclinePlaceholder, wording.DeclineButton)}";
            return Layout(wording.DeclineTitle, content, "#ef4444");
        }

        var accent = res.Status switch
        {
            EmailActionViewStatuses.Success => res.Action == "DECLINE" ? "#ef4444" : "#10b981",
            EmailActionViewStatuses.AlreadyResponded => "#d97706",
            EmailActionViewStatuses.Expired => "#d97706",
            _ => "#6b7280",
        };

        var title = res.Status switch
        {
            EmailActionViewStatuses.Success => res.Action == "DECLINE" ? "Đã ghi nhận từ chối" : "Đã ghi nhận chấp nhận",
            EmailActionViewStatuses.AlreadyResponded => "Đã phản hồi trước đó",
            EmailActionViewStatuses.Expired => "Liên kết đã hết hạn",
            _ => "Liên kết không hợp lệ",
        };

        var body = $@"<p style=""color:#374151;font-size:15px"">{HE(res.Message)}</p>";
        if (!string.IsNullOrWhiteSpace(res.DelegationName))
            body += $@"<p style=""color:#6b7280;font-size:13px"">Đoàn khách: <strong>{HE(res.DelegationName)}</strong></p>";

        return RenderMessage(title, body, accent);
    }

    // ── helpers ──

    /// <summary>The decline-reason form, shared by the GET landing and the validation-error re-render.
    /// Posts <c>declineReason</c> back to the same URL (method=post, no action). Server-side validation
    /// in the handler is the real gate; the HTML5 attributes are a best-effort client hint.</summary>
    private static string DeclineForm(string? reason, string? error,
        string placeholder = "Ví dụ: Tôi bận lịch cá nhân vào thời gian này...",
        string submitLabel = "Gửi phản hồi từ chối")
    {
        var borderColor = string.IsNullOrEmpty(error) ? "#d1d5db" : "#ef4444";
        var errorHtml = string.IsNullOrEmpty(error)
            ? string.Empty
            : $@"<p style=""color:#ef4444;font-size:13px;font-weight:bold;margin:8px 0 0"">{HE(error)}</p>";
        return $@"
      <form method=""post"" style=""margin-top:8px;text-align:left"">
        <label style=""display:block;font-size:13px;font-weight:bold;color:#374151;margin-bottom:6px"">Lý do từ chối <span style=""color:#ef4444"">*</span></label>
        <textarea name=""declineReason"" required minlength=""5"" maxlength=""1000"" rows=""4"" placeholder=""{HE(placeholder)}"" style=""width:100%;box-sizing:border-box;padding:12px;border:1px solid {borderColor};border-radius:10px;font-size:14px;font-family:inherit;resize:vertical;outline:none"">{HE(reason)}</textarea>
        {errorHtml}
        <button type=""submit"" style=""margin-top:14px;display:block;width:100%;background:#ef4444;color:#fff;border:none;cursor:pointer;font-weight:bold;font-size:15px;padding:14px 28px;border-radius:10px"">{HE(submitLabel)}</button>
        <p style=""color:#9ca3af;font-size:12px;margin-top:12px;text-align:center"">Lý do cần từ 5 đến 1000 ký tự. Liên kết này chỉ sử dụng được một lần.</p>
      </form>";
    }

    private static string TerminalTitle(string status, string? context, string? action, string? currentResponse) => status switch
    {
        EmailActionViewStatuses.AlreadyResponded => "Đã phản hồi trước đó",
        EmailActionViewStatuses.Expired => "Liên kết đã hết hạn",
        _ => "Liên kết không hợp lệ",
    };

    private static string TerminalBody(string status, string? context, string? currentResponse) => status switch
    {
        EmailActionViewStatuses.AlreadyResponded =>
            $@"<p style=""color:#374151;font-size:15px"">{HE(PresentationFor(context).AlreadyRespondedBody)}{(currentResponse == "ACCEPTED" ? " (Chấp nhận)" : currentResponse == "DECLINED" ? " (Từ chối)" : string.Empty)}.</p>",
        EmailActionViewStatuses.Expired =>
            @"<p style=""color:#374151;font-size:15px"">Liên kết phản hồi đã hết hạn. Vui lòng liên hệ người gửi để được hỗ trợ.</p>",
        _ =>
            @"<p style=""color:#374151;font-size:15px"">Liên kết không hợp lệ hoặc đã bị thu hồi.</p>",
    };

    private static string TerminalAccent(string status) => status switch
    {
        EmailActionViewStatuses.AlreadyResponded => "#d97706",
        EmailActionViewStatuses.Expired => "#d97706",
        _ => "#6b7280",
    };

    private static string RenderMessage(string title, string bodyHtml, string accent)
        => Layout(title, $@"<div style=""text-align:center"">{bodyHtml}</div>", accent);

    private static string Layout(string title, string contentHtml, string accent) => $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>{HE(title)} — PEMS</title>
</head>
<body style=""font-family:Arial,sans-serif;background:#f4f6f9;margin:0;padding:24px"">
  <div style=""max-width:560px;margin:40px auto;background:#fff;border-radius:14px;overflow:hidden;box-shadow:0 4px 16px rgba(0,0,0,.08)"">
    <div style=""background:linear-gradient(135deg,#004c91,#013565);padding:26px 32px"">
      <h1 style=""color:#fff;margin:0;font-size:20px"">PEMS — Campus Visit</h1>
      <p style=""color:#b3c8e8;margin:6px 0 0;font-size:13px"">FPT University</p>
    </div>
    <div style=""padding:32px;text-align:center"">
      <h2 style=""color:{accent};font-size:19px;margin:0 0 16px"">{HE(title)}</h2>
      {contentHtml}
    </div>
    <div style=""background:#f9fafb;padding:14px 32px;text-align:center"">
      <p style=""color:#9ca3af;font-size:11px;margin:0"">© 2026 PEMS — FPT University. Không trả lời email này.</p>
    </div>
  </div>
</body>
</html>";

    private static string HE(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
