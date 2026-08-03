namespace PEMS.Application.Emails.Common;

/// <summary>
/// Declares, per template code, which system action block the backend injects at send time. This is
/// what lets the preview show a read-only action area and the send path append real tokens to the
/// (possibly edited) content. Codes not listed are plain templates with no system action.
/// </summary>
public sealed record EmailTemplateActionSpec(
    bool IsActionTemplate,
    bool HasAcceptDecline,
    bool HasAssignLink,
    bool HasDetailLink,
    bool HasLogisticsAction,
    string SystemActionDescription,
    string[] RequiredActionPlaceholders);

public static class EmailActionTemplates
{
    public const string ParticipantInvitation = "VISIT_PARTICIPANT_INVITATION";
    public const string StudentInvitation = "VISIT_STUDENT_INVITATION";
    public const string DepartmentLeaderInvitation = "VISIT_DEPARTMENT_LEADER_INVITATION";
    public const string DepartmentStaffAssignment = "VISIT_DEPARTMENT_STAFF_ASSIGNMENT";
    public const string LogisticsAssigneeAssignment = "LOGISTICS_ASSIGNEE_ASSIGNMENT";
    public const string LogisticsRequestToDepartment = "LOGISTICS_REQUEST_TO_DEPARTMENT";
    public const string LogisticsExpenseReportReminder = "LOGISTICS_EXPENSE_REPORT_REMINDER";
    public const string VisitReminderHost = "VISIT_REMINDER_HOST";
    public const string VisitReminderParticipants = "VISIT_REMINDER_PARTICIPANTS";

    private const string AcceptDeclineDesc =
        "Nút Chấp nhận / Từ chối sẽ được hệ thống tự gắn (kèm liên kết một lần) khi gửi email.";
    private const string AcceptDeclineAssignDesc =
        "Nút Chấp nhận / Từ chối và liên kết Gán nhân sự sẽ được hệ thống tự gắn khi gửi email.";
    private const string DetailDesc =
        "Nút \"Mở yêu cầu để xử lý\" (yêu cầu đăng nhập) sẽ được hệ thống tự gắn khi gửi email. " +
        "Sau khi đăng nhập, Trưởng phòng có thể chấp nhận xử lý, từ chối yêu cầu, gán nhân sự hoặc đề xuất thay đổi.";
    private const string LogisticsActionDesc =
        "Nút Đồng ý / Từ chối / Hành động khác sẽ được hệ thống tự gắn (kèm liên kết một lần) khi gửi email.";

    // The three below carry NO one-time token. Their block is a plain login-required link to a page the
    // recipient already has access to, so there is nothing for a token to grant — which is also why
    // these messages keep their body in full in the email history. They are registered all the same:
    // the link is built by the backend from App:FrontendBaseUrl and injected as a trusted block, so it
    // is a system action in every sense that matters here, and the editor must be told it is required.
    private const string VisitReminderDesc =
        "Nút \"Xem chi tiết chuyến tiếp khách\" (yêu cầu đăng nhập) sẽ được hệ thống tự gắn khi gửi email. " +
        "Nhắc lịch không mang liên kết dùng một lần.";
    private const string ExpenseReminderDesc =
        "Nút \"Mở biên bản để kê khai chi phí\" (yêu cầu đăng nhập) sẽ được hệ thống tự gắn khi gửi email. " +
        "Nhắc kê khai không mang liên kết dùng một lần.";

    public static EmailTemplateActionSpec? For(string templateCode) => templateCode switch
    {
        ParticipantInvitation or StudentInvitation => new(true, true, false, false, false, AcceptDeclineDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}" }),
        DepartmentLeaderInvitation => new(true, true, true, false, false, AcceptDeclineAssignDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}", "{{assignUrl}}" }),
        // The Department Leader assigns a named person, and that person still answers for themselves:
        // the mail mints their own accept/decline tokens exactly like an invitation does.
        DepartmentStaffAssignment => new(true, true, false, false, false, AcceptDeclineDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}" }),
        LogisticsAssigneeAssignment => new(true, true, false, false, false, AcceptDeclineDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}" }),
        LogisticsRequestToDepartment => new(true, false, false, true, true, LogisticsActionDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}", "{{detailUrl}}" }),
        VisitReminderHost or VisitReminderParticipants => new(true, false, false, true, false, VisitReminderDesc,
            System.Array.Empty<string>()),
        LogisticsExpenseReportReminder => new(true, false, false, true, false, ExpenseReminderDesc,
            System.Array.Empty<string>()),
        _ => null,
    };

    /// <summary>
    /// The label the real send puts on this template's detail button, so a preview shows the same words
    /// rather than a generic stand-in. Null for templates whose block is not a single detail link.
    /// </summary>
    public static string? DetailLinkLabelFor(string templateCode) => templateCode switch
    {
        VisitReminderHost or VisitReminderParticipants => "Xem chi tiết chuyến tiếp khách",
        LogisticsExpenseReportReminder => "Mở biên bản để kê khai chi phí",
        LogisticsRequestToDepartment => "Mở yêu cầu để xử lý",
        _ => null,
    };
}
