using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Which concrete button/link SHAPE a template's action block draws — the single source both the real
/// send and the preview stand-in read from, so which buttons a template shows can never be decided
/// twice and disagree (Phase C of the email fidelity plan). Replaces the boolean-flag-plus-templateCode
/// special-casing <see cref="EmailActionTemplates.DisabledBlockFor"/> used to do its own dispatch with.
/// </summary>
public enum ActionPresentationKind
{
    /// <summary>One button around a one-time account-activation token.</summary>
    Confirm,
    /// <summary>Two buttons: Accept / Decline.</summary>
    AcceptDecline,
    /// <summary>Three buttons: Accept / Decline / Assign staff.</summary>
    AcceptDeclineAssign,
    /// <summary>Two buttons: Confirm / Decline (the operational-contact invitation's own wording).</summary>
    ContactRoleInvitation,
    /// <summary>Three buttons: Agree / Decline / a login-required detail link.</summary>
    LogisticsAction,
    /// <summary>Three buttons: Accept task / Decline task / a login-required detail link.</summary>
    LogisticsAssignee,
    /// <summary>One login-required detail link, no one-time token.</summary>
    DetailOnly,
}

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
    ActionPresentationKind PresentationKind,
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
    private const string LogisticsActionDesc =
        "Nút Đồng ý / Từ chối / Hành động khác sẽ được hệ thống tự gắn (kèm liên kết một lần) khi gửi email.";

    // Registered 2026-08-03. All three were sending a block into a body that declared the placeholder,
    // while the registry said the template had no action — so the editor showed no action area, the
    // preview fell back to a neutral stand-in, and the contract allowed an operator to delete the one
    // element the message exists for. What each block draws is read off the send path, not invented:
    // VisitContactClaimService builds ContactRoleInvitationBlock and ProposeRequestChangeCommand builds
    // LogisticsProposalActionBlock.
    private const string ContactRoleInvitationDesc =
        "Hai nút \"Xác nhận\" / \"Từ chối\" (mỗi nút một liên kết dùng một lần) sẽ được hệ thống tự gắn " +
        "khi gửi email. Liên kết có hạn, KHÔNG yêu cầu đăng nhập, và mở trang xác nhận để người nhận " +
        "xem thông tin mới nhất trước khi quyết định.";
    // Proposal decisions are Portal-only (spec BUG-07): the email carries no public token, only a
    // login-required detail link — Host signs in and Accepts/Rejects from the Portal.
    private const string LogisticsProposalDesc =
        "Nút \"Xem chi tiết trong hệ thống\" (yêu cầu đăng nhập) sẽ được hệ thống tự gắn khi gửi email. " +
        "Đề xuất không mang liên kết dùng một lần — Host đăng nhập và Chấp nhận/Từ chối trong hệ thống.";

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

    // ── Button labels by presentation kind — the ONE place a button's words are decided. Both
    // EmailComposition.Real*Block and its Disabled* counterpart read from here, in both languages, so
    // an operator's preview and a recipient's inbox can never show different words for the same button
    // (Phase C). Every label here is what production code ACTUALLY sends today; a new label belongs
    // here, never typed directly into a block builder again.

    public static string AcceptLabel(string language)
        => EmailLanguages.Normalize(language) == EmailLanguages.En ? "Accept" : "Chấp nhận";
    public static string DeclineLabel(string language)
        => EmailLanguages.Normalize(language) == EmailLanguages.En ? "Decline" : "Từ chối";
    public static string AssignStaffLabel(string language)
        => EmailLanguages.Normalize(language) == EmailLanguages.En ? "Assign staff" : "Gán nhân sự";
    public static string ContactConfirmLabel(string language)
        => EmailLanguages.Normalize(language) == EmailLanguages.En ? "Confirm" : "Xác nhận";
    public static string ContactDeclineLabel(string language)
        => EmailLanguages.Normalize(language) == EmailLanguages.En ? "Decline" : "Từ chối";
    /// <summary>The logistics-request direct-action "yes" button — a distinct word from
    /// <see cref="AcceptLabel"/> in both languages, matching the two different real sends.</summary>
    public static string LogisticsAgreeLabel(string language)
        => EmailLanguages.Normalize(language) == EmailLanguages.En ? "Agree" : "Đồng ý";
    public static string LogisticsAssigneeAcceptLabel(string language)
        => EmailLanguages.Normalize(language) == EmailLanguages.En ? "Accept task" : "Chấp nhận nhiệm vụ";
    public static string LogisticsAssigneeDeclineLabel(string language)
        => EmailLanguages.Normalize(language) == EmailLanguages.En ? "Decline task" : "Từ chối nhiệm vụ";
    public static string LogisticsOtherActionLabel(string language)
        => EmailLanguages.Normalize(language) == EmailLanguages.En ? "Other action" : "Hành động khác";

    public static EmailTemplateActionSpec? For(string templateCode) => templateCode switch
    {
        // Registered so the preview shows the real button. This changes NO token or route logic: the
        // confirm URL is still minted by IAccountEmailConfirmationService and injected as a trusted
        // block by the send path — the registry only says which button that block draws.
        AccountEmailConfirmation => new(true, false, false, false, false, ConfirmEmailDesc,
            System.Array.Empty<string>(), ActionPresentationKind.Confirm, HasConfirmAction: true),

        ParticipantInvitation or StudentInvitation => new(true, true, false, false, false, AcceptDeclineDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}" }, ActionPresentationKind.AcceptDecline),
        DepartmentLeaderInvitation => new(true, true, true, false, false, AcceptDeclineAssignDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}", "{{assignUrl}}" }, ActionPresentationKind.AcceptDeclineAssign),
        // The Department Leader assigns a named person, and that person still answers for themselves:
        // the mail mints their own accept/decline tokens exactly like an invitation does.
        DepartmentStaffAssignment => new(true, true, false, false, false, AcceptDeclineDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}" }, ActionPresentationKind.AcceptDecline),
        // 3-button real send (Accept/Decline/Detail via LogisticsAssigneeActionBlock) — its own kind, not
        // the generic 2-button AcceptDecline (BUG-09).
        LogisticsAssigneeAssignment => new(true, true, false, true, false, AcceptDeclineDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}" }, ActionPresentationKind.LogisticsAssignee),
        LogisticsRequestToDepartment => new(true, false, false, true, true, LogisticsActionDesc,
            new[] { "{{acceptUrl}}", "{{declineUrl}}", "{{detailUrl}}" }, ActionPresentationKind.LogisticsAction),
        VisitReminderHost or VisitReminderParticipants => new(true, false, false, true, false, VisitReminderDesc,
            System.Array.Empty<string>(), ActionPresentationKind.DetailOnly),
        LogisticsExpenseReportReminder => new(true, false, false, true, false, ExpenseReminderDesc,
            System.Array.Empty<string>(), ActionPresentationKind.DetailOnly),

        // One button around a one-time claim URL. No RequiredActionPlaceholders: the URL never appears
        // in the stored body — the backend builds the whole block — which is the same reason the
        // reminders above declare none either. Only the templates whose body historically interpolated
        // {{acceptUrl}}-style placeholders declare them.
        VisitContactClaim or VisitContactTransfer => new(true, false, false, false, false,
            ContactRoleInvitationDesc, System.Array.Empty<string>(), ActionPresentationKind.ContactRoleInvitation),

        // Detail-only, login-required (spec BUG-07) — a proposal decision is Portal-only, so this is
        // NOT an accept/decline nor a logistics-action template despite the business meaning; it is
        // shaped exactly like VisitReminderHost/LogisticsExpenseReportReminder.
        LogisticsChangeProposalToHost => new(true, false, false, true, false,
            LogisticsProposalDesc, System.Array.Empty<string>(), ActionPresentationKind.DetailOnly),

        // VISIT_REQUEST_OTP is deliberately NOT here. Its message is the code itself; no send path
        // supplies an action block for it, so registering it would make the contract demand a
        // placeholder that nothing can ever fill — and the renderer refuses an unresolved one.
        _ => null,
    };

    /// <summary>
    /// The label the real send puts on this template's detail button, in the requested language, so a
    /// preview shows the same words rather than a generic stand-in. Null for templates whose block is
    /// not a single detail link.
    /// </summary>
    public static string? DetailLinkLabelFor(string templateCode, string language)
    {
        var en = EmailLanguages.Normalize(language) == EmailLanguages.En;
        return templateCode switch
        {
            VisitReminderHost or VisitReminderParticipants
                => en ? "View visit details" : "Xem chi tiết chuyến tiếp khách",
            LogisticsExpenseReportReminder
                => en ? "Open report to declare expenses" : "Mở biên bản để kê khai chi phí",
            LogisticsRequestToDepartment
                => en ? "Open request to process" : "Mở yêu cầu để xử lý",
            LogisticsChangeProposalToHost or LogisticsAssigneeAssignment
                => en ? "View details in the system" : "Xem chi tiết trong hệ thống",
            _ => null,
        };
    }

    /// <summary>
    /// The inert action block a preview shows for this template — no live URL, no token, same visible
    /// text/labels/colours as the real send in the same language.
    ///
    /// <para>
    /// The single place this choice is made. It was previously written out three times — in the preview
    /// modal's handler, in the contract the editor fetches, and again in the tests — which is how the
    /// editor's pane and the preview modal could show different buttons for the same template while every
    /// test agreed with whichever copy it had been written against. One helper, one answer — dispatched
    /// by <see cref="ActionPresentationKind"/>, the same value a real send would need to pick its
    /// builder, rather than by re-deriving the shape from templateCode/flags a second time.
    /// </para>
    /// </summary>
    public static string DisabledBlockFor(string templateCode, string language)
    {
        var spec = For(templateCode);
        if (spec is null) return EmailComposition.DisabledUnspecifiedActionBlock(language);

        return spec.PresentationKind switch
        {
            ActionPresentationKind.Confirm =>
                EmailComposition.DisabledConfirmEmailBlock(ConfirmEmailLabel(language)),
            ActionPresentationKind.ContactRoleInvitation =>
                EmailComposition.DisabledContactRoleInvitationBlock(language),
            ActionPresentationKind.LogisticsAssignee =>
                EmailComposition.DisabledLogisticsAssigneeActionBlock(
                    language, DetailLinkLabelFor(templateCode, language) ?? "Xem chi tiết trong hệ thống"),
            ActionPresentationKind.LogisticsAction =>
                EmailComposition.DisabledLogisticsActionBlock(language),
            // VisitReminderHost/Participants' real send is VisitDetailBlock, not DetailLinkBlock — its own
            // padding (12px 24px vs 12px 22px), so it needs its own matching disabled stand-in rather
            // than the generic one every other DetailOnly template shares.
            ActionPresentationKind.DetailOnly when templateCode is VisitReminderHost or VisitReminderParticipants =>
                EmailComposition.DisabledVisitDetailBlock(language),
            ActionPresentationKind.DetailOnly =>
                EmailComposition.DisabledDetailLinkBlock(
                    DetailLinkLabelFor(templateCode, language) ?? "Mở yêu cầu để xử lý"),
            ActionPresentationKind.AcceptDeclineAssign =>
                EmailComposition.DisabledAcceptDeclineBlock(language, withAssign: true),
            _ => EmailComposition.DisabledAcceptDeclineBlock(language, withAssign: false),
        };
    }
}
