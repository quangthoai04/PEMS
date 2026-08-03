using PEMS.Application.Common.Interfaces;

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
    string[] RequiredActionPlaceholders,
    /// <summary>
    /// A single confirm-email button around a one-time activation token. Its own kind rather than a
    /// detail link: a detail link asks the recipient to sign in to a page they already have access to,
    /// while this one IS the credential that activates the account.
    /// </summary>
    bool HasConfirmAction = false);

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
    public const string VisitContactClaim = SystemEmailTemplates.VisitContactClaim;
    public const string VisitContactTransfer = SystemEmailTemplates.VisitContactTransfer;
    public const string LogisticsChangeProposalToHost = SystemEmailTemplates.LogisticsChangeProposalToHost;

    private const string AcceptDeclineDesc =
        "Nút Chấp nhận / Từ chối sẽ được hệ thống tự gắn (kèm liên kết một lần) khi gửi email.";
    private const string AcceptDeclineAssignDesc =
        "Nút Chấp nhận / Từ chối và liên kết Gán nhân sự sẽ được hệ thống tự gắn khi gửi email.";
    private const string DetailDesc =
        "Nút \"Mở yêu cầu để xử lý\" (yêu cầu đăng nhập) sẽ được hệ thống tự gắn khi gửi email. " +
        "Sau khi đăng nhập, Trưởng phòng có thể chấp nhận xử lý, từ chối yêu cầu, gán nhân sự hoặc đề xuất thay đổi.";
    private const string LogisticsActionDesc =
        "Nút Đồng ý / Từ chối / Hành động khác sẽ được hệ thống tự gắn (kèm liên kết một lần) khi gửi email.";

    // Registered 2026-08-03. All three were sending a block into a body that declared the placeholder,
    // while the registry said the template had no action — so the editor showed no action area, the
    // preview fell back to a neutral stand-in, and the contract allowed an operator to delete the one
    // element the message exists for. What each block draws is read off the send path, not invented:
    // VisitContactClaimService builds ContactRoleInvitationBlock and ProposeRequestChangeCommand builds
    // LogisticsProposalActionBlock.
    private const string ContactRoleInvitationDesc =
        "Nút \"Mở trang xác nhận\" (kèm liên kết một lần) sẽ được hệ thống tự gắn khi gửi email. " +
        "Liên kết có hạn và yêu cầu người nhận đăng nhập bằng đúng tài khoản Google của email này.";
    private const string LogisticsProposalDesc =
        "Nút Chấp nhận đề xuất / Từ chối đề xuất (kèm liên kết một lần, không cần đăng nhập) và " +
        "\"Xem chi tiết trong hệ thống\" sẽ được hệ thống tự gắn khi gửi email.";

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

    public const string AccountEmailConfirmation = "ACCOUNT_EMAIL_CONFIRMATION";

    private const string ConfirmEmailDesc =
        "Nút \"Xác nhận email\" (kèm liên kết một lần) sẽ được hệ thống tự gắn khi gửi email. " +
        "Liên kết kích hoạt tài khoản và chỉ dùng được một lần.";

    /// <summary>
    /// The words on the confirm-email button, in the requested language.
    ///
    /// <para>
    /// Metadata rather than a literal inside the block builder, because the SAME label has to reach two
    /// places that must never disagree: the real send and the editor's preview. It lived only in
    /// <c>EmailComposition.ConfirmEmailBlock</c> before, so the preview had no way to read it and fell
    /// back to a neutral "action area" — an operator editing this template could not see which button
    /// their words sit above.
    /// </para>
    /// </summary>
    public static string ConfirmEmailLabel(string language)
        => EmailLanguages.Normalize(language) == EmailLanguages.En ? "Confirm email" : "Xác nhận email";

    public static EmailTemplateActionSpec? For(string templateCode) => templateCode switch
    {
        // Registered so the preview shows the real button. This changes NO token or route logic: the
        // confirm URL is still minted by IAccountEmailConfirmationService and injected as a trusted
        // block by the send path — the registry only says which button that block draws.
        AccountEmailConfirmation => new(true, false, false, false, false, ConfirmEmailDesc,
            System.Array.Empty<string>(), HasConfirmAction: true),

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

        // One button around a one-time claim URL. No RequiredActionPlaceholders: the URL never appears
        // in the stored body — the backend builds the whole block — which is the same reason the
        // reminders above declare none either. Only the templates whose body historically interpolated
        // {{acceptUrl}}-style placeholders declare them.
        VisitContactClaim or VisitContactTransfer => new(true, false, false, false, false,
            ContactRoleInvitationDesc, System.Array.Empty<string>()),

        // Accept / reject / detail, like LogisticsRequestToDepartment — but its own wording, because the
        // Host is answering a change PROPOSAL, not a new request.
        LogisticsChangeProposalToHost => new(true, false, false, true, true,
            LogisticsProposalDesc, System.Array.Empty<string>()),

        // VISIT_REQUEST_OTP is deliberately NOT here. Its message is the code itself; no send path
        // supplies an action block for it, so registering it would make the contract demand a
        // placeholder that nothing can ever fill — and the renderer refuses an unresolved one.
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
        LogisticsChangeProposalToHost => "Xem chi tiết trong hệ thống",
        _ => null,
    };

    /// <summary>
    /// The inert action block a preview shows for this template — no live URL, no token.
    ///
    /// <para>
    /// The single place this choice is made. It was previously written out three times — in the preview
    /// modal's handler, in the contract the editor fetches, and again in the tests — which is how the
    /// editor's pane and the preview modal could show different buttons for the same template while every
    /// test agreed with whichever copy it had been written against. One helper, one answer.
    /// </para>
    /// </summary>
    public static string DisabledBlockFor(string templateCode, string language)
    {
        var spec = For(templateCode);

        if (spec is null) return EmailComposition.DisabledUnspecifiedActionBlock(language);
        if (spec.HasConfirmAction)
            return EmailComposition.DisabledConfirmEmailBlock(ConfirmEmailLabel(language));

        // Two blocks the boolean flags cannot tell apart from their neighbours, so they are matched by
        // code: an operator must read the words their real recipient will read, and "Chấp nhận / Từ chối"
        // is not what either of these sends. Matched before the flags below, which are deliberately
        // coarse.
        switch (templateCode)
        {
            case VisitContactClaim:
            case VisitContactTransfer:
                return EmailComposition.DisabledContactRoleInvitationBlock();
            case LogisticsChangeProposalToHost:
                return EmailComposition.DisabledLogisticsProposalActionBlock(
                    DetailLinkLabelFor(templateCode) ?? "Xem chi tiết trong hệ thống");
        }

        if (spec.HasLogisticsAction) return EmailComposition.DisabledLogisticsActionBlock();
        if (spec.HasDetailLink)
            return EmailComposition.DisabledDetailLinkBlock(
                DetailLinkLabelFor(templateCode) ?? "Mở yêu cầu để xử lý");

        return EmailComposition.DisabledAcceptDeclineBlock(spec.HasAssignLink);
    }
}
