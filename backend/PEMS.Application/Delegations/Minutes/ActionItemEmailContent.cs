using PEMS.Application.Emails.Common;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// Shared plain-FYI email bodies for a meeting-minutes action item's responsible person — no
/// accept/decline action tokens, just informational. Used both at assignment time
/// (<see cref="SaveMinutesCommandHandler"/>) and at the due-date reminder tick
/// (ActionItemDueReminderHostedService, PEMS.Infrastructure), so the two moments read consistently.
/// </summary>
public static class ActionItemEmailContent
{
    public static string AssignedSubject(string title) => $"[PEMS] Bạn được giao đầu việc — {title}";

    public static string AssignedBodyHtml(string assigneeName, string title, string? dueDateText, string delegationName)
    {
        string HE(string? s) => EmailComposition.HE(s);
        return $@"<p>Xin chào <strong>{HE(assigneeName)}</strong>,</p>
<p>Bạn được phân công phụ trách 1 đầu việc trong biên bản cuộc họp của đoàn <strong>{HE(delegationName)}</strong>.</p>
<div style=""background:#f0f7ff;border-left:4px solid #004c91;border-radius:8px;padding:16px 20px;margin:20px 0"">
  <ul style=""margin:0;padding-left:20px;line-height:1.7"">
    <li><strong>Công việc:</strong> {HE(title)}</li>
    <li><strong>Hạn hoàn thành:</strong> {HE(dueDateText ?? "Chưa có hạn cụ thể")}</li>
    <li><strong>Đoàn khách:</strong> {HE(delegationName)}</li>
  </ul>
</div>
<p>Vui lòng vào hệ thống PEMS để xem chi tiết và cập nhật tiến độ.</p>";
    }

    public static string DueReminderSubject(string title) => $"[PEMS] Đến hạn hoàn thành đầu việc — {title}";

    public static string DueReminderBodyHtml(string assigneeName, string title, string dueDateText, string delegationName)
    {
        string HE(string? s) => EmailComposition.HE(s);
        return $@"<p>Xin chào <strong>{HE(assigneeName)}</strong>,</p>
<p>Công việc bạn phụ trách trong biên bản cuộc họp của đoàn <strong>{HE(delegationName)}</strong> đã đến hạn hoàn thành.</p>
<div style=""background:#fff7ed;border-left:4px solid #f37021;border-radius:8px;padding:16px 20px;margin:20px 0"">
  <ul style=""margin:0;padding-left:20px;line-height:1.7"">
    <li><strong>Công việc:</strong> {HE(title)}</li>
    <li><strong>Hạn hoàn thành:</strong> {HE(dueDateText)}</li>
    <li><strong>Đoàn khách:</strong> {HE(delegationName)}</li>
  </ul>
</div>
<p>Vui lòng hoàn thành sớm và cập nhật trạng thái trên hệ thống PEMS.</p>";
    }
}
