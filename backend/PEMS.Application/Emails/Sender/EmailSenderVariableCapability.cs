using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Sender;

/// <summary>
/// Whether a template may carry sender variables, and whether the person sending it may edit the message
/// before it goes out.
///
/// <para>
/// Two questions, one enum, because they are not independent: a template nobody may edit at runtime can
/// still print who it is from, but a template that may not name a sender at all can hardly offer an editor
/// for the sentence naming them. The three values are the only three combinations that mean anything.
/// </para>
/// <para>
/// This is NOT the old <c>EmailContactCapability</c> renamed. That one asked whether a configured
/// third-party contact card could be attached, and its levels (UNSUPPORTED / SUPPORTED / REQUIRED) were
/// about whether an operator could choose NONE. This one asks who a message is from and who may rewrite
/// it, and has no notion of a requirement level — a template either offers the variables or it does not,
/// and whether the body uses them is the administrator's editorial choice, not a setting.
/// </para>
/// </summary>
public enum EmailSenderVariableCapability
{
    /// <summary>
    /// The variables may not appear. No sender group in the picker, no <c>{{sender*}}</c> accepted by the
    /// save, no runtime editor. Used where naming a person would add surface to a message whose entire
    /// content is a one-time credential.
    /// </summary>
    NOT_AVAILABLE,

    /// <summary>
    /// The administrator may put sender variables in the body, but the message is sent by a background job
    /// or as an automatic consequence of an action — there is no modal, no preview, and nobody to edit it.
    /// </summary>
    AVAILABLE_READ_ONLY_RUNTIME,

    /// <summary>
    /// The administrator may put sender variables in the body AND the person sending prepares the message
    /// themselves: they see the resolved preview, may open the editor, and must pass a final preview before
    /// it is sent.
    /// </summary>
    AVAILABLE_EDITABLE_RUNTIME,
}

/// <param name="ReasonCode">Stable, matched by clients; never the Vietnamese sentence, which is for a person.</param>
public sealed record EmailSenderVariableCapabilityInfo(
    EmailSenderVariableCapability Capability,
    string ReasonCode,
    string ReasonVi,
    string ReasonEn)
{
    /// <summary>True when the body may contain <c>{{sender*}}</c> and the picker may offer the group.</summary>
    public bool VariablesAllowed => Capability != EmailSenderVariableCapability.NOT_AVAILABLE;

    /// <summary>True when the send flow may offer a "Chỉnh sửa" button.</summary>
    public bool RuntimeEditable => Capability == EmailSenderVariableCapability.AVAILABLE_EDITABLE_RUNTIME;
}

/// <summary>
/// Classifies every registered template, from what the message carries and who causes it to be sent.
///
/// <para>
/// <b>Enumerated, never derived.</b> Neither the placeholder nor the presence of an <c>EmailOverride</c>
/// parameter on the command decides this — see <c>NotFromThePlaceholder</c> below. The classification is a
/// product decision recorded here, and <c>EmailSenderVariableCapabilityTests</c> asserts the map covers
/// the registry exactly, so a new template cannot be added without one.
/// </para>
/// </summary>
public static class EmailSenderVariableCapabilities
{
    public const string ReasonOneTimeCredential = "ONE_TIME_CREDENTIAL";
    public const string ReasonAutomatedSend = "AUTOMATED_SEND";
    public const string ReasonPreparedBySender = "PREPARED_BY_SENDER";

    /// <summary>
    /// Templates whose message IS a credential. The code or link is the whole point of the mail; anything
    /// else in it is extra surface on something that may be forwarded, quoted or read over a shoulder —
    /// and none of these texts asks the reader to get in touch with anybody, so there is nothing for a
    /// sender line to serve.
    ///
    /// <para>
    /// Exactly the three the plan names (§3.1). <c>VISIT_REMINDER_HOST</c> is deliberately NOT here even
    /// though the removed contact feature refused it: that refusal was because a CONTACT block would have
    /// printed the recipient's own details back at them, which is a fact about contacts and not about
    /// senders. The Host is the recipient of that reminder; the sender is the system.
    /// </para>
    /// </summary>
    private static readonly IReadOnlySet<string> NotAvailable = new HashSet<string>(StringComparer.Ordinal)
    {
        SystemEmailTemplates.AccountEmailConfirmation,
        SystemEmailTemplates.AuthPasswordResetOtp,
        SystemEmailTemplates.VisitRequestOtp,
    };

    /// <summary>
    /// Templates a person prepares and sends themselves, through a preview they must pass before the
    /// message leaves.
    ///
    /// <para>
    /// The set is small and every member earns its place from the SEND FLOW, not from the subject matter:
    /// each of these is reached from a screen where somebody composes, previews and presses send, and each
    /// therefore has a human sender whose name the recipient can act on. Everything else — including mail
    /// an actor causes but does not compose, such as a logistics assignment — is read-only at runtime.
    /// </para>
    /// <para>
    /// <b>NotFromThePlaceholder.</b> It would be simpler to say "editable when the body writes
    /// <c>{{senderName}}</c>", and it would be wrong in both directions: an administrator who adds the
    /// variable to a reminder must not thereby open a runtime editor on a background job, and one who
    /// removes it from an invitation must not thereby take the editor away from the Host who relies on it.
    /// Capability is about the flow; the placeholder is about the wording.
    /// </para>
    /// </summary>
    private static readonly IReadOnlySet<string> EditableRuntime = new HashSet<string>(StringComparer.Ordinal)
    {
        // The Host writes to a department and needs to be reachable for the coordination that follows.
        SystemEmailTemplates.LogisticsRequestToDepartment,

        // A counter-offer the department composes; the Host has to be able to reach whoever proposed it.
        SystemEmailTemplates.LogisticsChangeProposalToHost,

        // The Department Leader hands a logistics item to one of their staff, from a screen that opens
        // the compose modal. Its subject matter looks automatic — "you have been assigned" — but the
        // FLOW is a person composing, which is what decides this.
        SystemEmailTemplates.LogisticsAssigneeAssignment,

        // The three invitations and the assignment: each opens the compose modal from a visit screen.
        SystemEmailTemplates.VisitParticipantInvitation,
        SystemEmailTemplates.VisitStudentInvitation,
        SystemEmailTemplates.VisitDepartmentLeaderInvitation,
        SystemEmailTemplates.VisitDepartmentStaffAssignment,

        // The Host's manual "cập nhật chuẩn bị" to the guest side. The most-edited message in the product.
        SystemEmailTemplates.VisitSetupProgressUpdate,
    };

    /// <summary>The capability of a template, with the reason a person can read.</summary>
    public static EmailSenderVariableCapabilityInfo For(string? templateCode)
    {
        if (templateCode is not null && NotAvailable.Contains(templateCode))
            return new EmailSenderVariableCapabilityInfo(
                EmailSenderVariableCapability.NOT_AVAILABLE,
                ReasonOneTimeCredential,
                "Mẫu này mang mã hoặc liên kết dùng một lần nên không hiển thị thông tin người gửi.",
                "This template carries a one-time code or link, so it does not show sender information.");

        if (templateCode is not null && EditableRuntime.Contains(templateCode))
            return new EmailSenderVariableCapabilityInfo(
                EmailSenderVariableCapability.AVAILABLE_EDITABLE_RUNTIME,
                ReasonPreparedBySender,
                "Người gửi tự chuẩn bị email này nên được xem trước và chỉnh sửa nội dung trước khi gửi.",
                "The sender prepares this email, so they may preview and edit it before sending.");

        return new EmailSenderVariableCapabilityInfo(
            EmailSenderVariableCapability.AVAILABLE_READ_ONLY_RUNTIME,
            ReasonAutomatedSend,
            "Mẫu này được hệ thống gửi tự động; nội dung chỉ sửa được trong màn quản lý mẫu email.",
            "This template is sent automatically; its wording is edited on the template-management screen.");
    }

    /// <summary>True when the body of this template may contain <c>{{sender*}}</c> at all.</summary>
    public static bool AllowsVariables(string? templateCode) => For(templateCode).VariablesAllowed;

    /// <summary>True when the send flow may offer a runtime editor.</summary>
    public static bool IsRuntimeEditable(string? templateCode) => For(templateCode).RuntimeEditable;

    /// <summary>Every template that may not name a sender — the parity-test source.</summary>
    public static IReadOnlyCollection<string> NotAvailableTemplateCodes => NotAvailable.ToList();

    /// <summary>Every template a person prepares and sends themselves — the parity-test source.</summary>
    public static IReadOnlyCollection<string> EditableRuntimeTemplateCodes => EditableRuntime.ToList();
}
