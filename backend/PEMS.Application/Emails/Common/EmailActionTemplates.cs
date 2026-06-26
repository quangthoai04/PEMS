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
    string SystemActionDescription,
    string[] RequiredActionPlaceholders);

public static class EmailActionTemplates
{
    public const string ParticipantInvitation = "VISIT_PARTICIPANT_INVITATION";
    public const string StudentInvitation = "VISIT_STUDENT_INVITATION";
    public const string DepartmentLeaderInvitation = "VISIT_DEPARTMENT_LEADER_INVITATION";
    public const string LogisticsAssigneeAssignment = "LOGISTICS_ASSIGNEE_ASSIGNMENT";
    public const string LogisticsRequestToDepartment = "LOGISTICS_REQUEST_TO_DEPARTMENT";

    private const string AcceptDeclineDesc =
        "Nút Chấp nhận / Từ chối sẽ được hệ thống tự gắn (kèm liên kết một lần) khi gửi email.";
    private const string AcceptDeclineAssignDesc =
        "Nút Chấp nhận / Từ chối và liên kết Gán nhân sự sẽ được hệ thống tự gắn khi gửi email.";
    private const string DetailDesc =
        "Nút Xem chi tiết yêu cầu (yêu cầu đăng nhập) sẽ được hệ thống tự gắn khi gửi email.";

    public static EmailTemplateActionSpec? For(string templateCode) => templateCode switch
    {
        ParticipantInvitation or StudentInvitation => new(true, true, false, false, AcceptDeclineDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}" }),
        DepartmentLeaderInvitation => new(true, true, true, false, AcceptDeclineAssignDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}", "{{assignUrl}}" }),
        LogisticsAssigneeAssignment => new(true, true, false, false, AcceptDeclineDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}" }),
        LogisticsRequestToDepartment => new(true, false, false, true, DetailDesc,
            new[] { "{{detailUrl}}" }),
        _ => null,
    };
}
