using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// The effective policy for one send, after the cascade has been applied. Every field is resolved — this
/// is what the renderer and the contract read, and nothing downstream needs to know which level supplied
/// which value.
/// </summary>
public sealed record EmailContactPolicyResolution(
    EmailContactRequirement Requirement,
    EmailContactSource ContactSource,
    bool ShowEmail,
    bool ShowPhone,
    bool ShowDepartment,
    bool ShowCampus,
    bool ShowSender,
    string HeadingVi,
    string HeadingEn,
    EmailReplyToSource ReplyToSource)
{
    public bool RendersBlock => Requirement != EmailContactRequirement.NONE;

    public string Heading(string language) =>
        EmailLanguages.Normalize(language) == EmailLanguages.En ? HeadingEn : HeadingVi;
}

/// <summary>
/// The policy this application ships for every registered template, and the base every cascade level
/// falls back to.
///
/// <para>
/// These are the SHIPPED DEFAULTS, in code, and they are the seed for <c>email_contact_policies</c> and
/// the target of "restore defaults" on the template screen. Keeping them here rather than only in SQL is
/// what lets a fresh database, an upgraded database and the running code be checked against one another
/// — the same arrangement <see cref="EmailTemplateDefaults"/> already uses for template content.
/// </para>
/// <para>
/// The classification comes from an audit of what each template's text actually asks the recipient to do
/// (see <c>docs/Ver2Carnh/send email setup/EMAIL_TABLE_ROOTCAUSE_AND_CONTACT_DESIGN.md</c> §4). A template
/// is REQUIRED only where the words instruct the reader to contact somebody; telling them to do so and
/// showing no address is the defect, so those fail closed rather than ship a dead instruction.
/// </para>
/// </summary>
public static class EmailContactPolicyDefaults
{
    public const string DefaultHeadingVi = "Thông tin liên hệ";
    public const string DefaultHeadingEn = "Contact information";

    /// <summary>
    /// The bottom of the cascade. Chosen so that a level which says nothing produces a block that is safe
    /// rather than one that is silent: OPTIONAL renders when a contact exists and never blocks a send.
    /// </summary>
    public static readonly EmailContactPolicyResolution SystemBaseline = new(
        Requirement: EmailContactRequirement.OPTIONAL,
        ContactSource: EmailContactSource.SUPPORT_CONTACT,
        ShowEmail: true,
        ShowPhone: true,
        ShowDepartment: false,
        ShowCampus: false,
        ShowSender: false,
        HeadingVi: DefaultHeadingVi,
        HeadingEn: DefaultHeadingEn,
        ReplyToSource: EmailReplyToSource.NONE);

    private static EmailContactPolicyResolution P(
        EmailContactRequirement requirement,
        EmailContactSource source,
        bool phone = true,
        bool department = false,
        bool campus = false,
        bool sender = false,
        EmailReplyToSource replyTo = EmailReplyToSource.NONE)
        => SystemBaseline with
        {
            Requirement = requirement,
            ContactSource = source,
            ShowPhone = phone,
            ShowDepartment = department,
            ShowCampus = campus,
            ShowSender = sender,
            ReplyToSource = replyTo,
        };

    private static readonly EmailContactPolicyResolution NoContact =
        SystemBaseline with { Requirement = EmailContactRequirement.NONE };

    private static readonly IReadOnlyDictionary<string, EmailContactPolicyResolution> ByTemplate =
        new Dictionary<string, EmailContactPolicyResolution>(StringComparer.Ordinal)
        {
            // ── Credential-bearing mail: no block ────────────────────────────
            // Nothing in the text asks the reader to contact anybody, and a block on a message carrying a
            // one-time code only widens what a forwarded or stolen copy discloses.
            [SystemEmailTemplates.AccountEmailConfirmation] = NoContact,
            [SystemEmailTemplates.AuthPasswordResetOtp] = NoContact,
            [SystemEmailTemplates.VisitRequestOtp] = NoContact,

            // The Host is the recipient here, so a "contact the Host" block would name them to themselves.
            [SystemEmailTemplates.VisitReminderHost] = NoContact,

            // ── Notices to an address that may not belong to the account ─────
            // These two go to the address that was just UNLINKED — possibly a stranger's, reached by a
            // typo. The registry gives them no variables at all so as not to name the account holder to
            // that stranger, and the contact block has to honour the same limit: SYSTEM support only.
            // A campus contact would disclose which campus the account belongs to, which is the same leak.
            [SystemEmailTemplates.AccountPendingEmailChangedOldNotice] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.SUPPORT_CONTACT, phone: true),
            [SystemEmailTemplates.AccountEmailChangedOldNotice] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.SUPPORT_CONTACT, phone: true),

            // ── Account lifecycle, recipient is the account holder ───────────
            [SystemEmailTemplates.AccountActivated] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.CAMPUS_DEFAULT, campus: true),
            [SystemEmailTemplates.AccountEmailChangedNewNotice] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.CAMPUS_DEFAULT, campus: true),
            [SystemEmailTemplates.AccountRoleChanged] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.CAMPUS_DEFAULT, campus: true),
            [SystemEmailTemplates.AccountStaffLeaderAssigned] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.CAMPUS_DEFAULT, campus: true),
            [SystemEmailTemplates.AccountStaffLeaderReplaced] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.SUPPORT_CONTACT),

            // ── Department personnel ─────────────────────────────────────────
            // "vui lòng liên hệ Trưởng phòng phụ trách" — the department head, by name.
            [SystemEmailTemplates.DeptPersonnelAccountDisabled] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.DEPARTMENT_DEFAULT, department: true),
            [SystemEmailTemplates.DeptPersonnelAccountEnabled] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.DEPARTMENT_DEFAULT, department: true),
            [SystemEmailTemplates.DeptLeadershipGranted] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.DEPARTMENT_DEFAULT, department: true),
            [SystemEmailTemplates.DeptLeadershipHandedOver] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.DEPARTMENT_DEFAULT, department: true),

            // ── Visit request contact claim/transfer ─────────────────────────
            // Guest-facing and token-bearing. No Host is assigned at claim time, so the campus is the only
            // honest answer to "who do I ask about this".
            [SystemEmailTemplates.VisitContactClaim] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.CAMPUS_DEFAULT, campus: true),
            [SystemEmailTemplates.VisitContactTransfer] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.CAMPUS_DEFAULT, campus: true),

            // ── Invitations and assignments: the Host, per campus ────────────
            // Each of these already prints "Người phụ trách tiếp đón: {{hostName}}" and then gives the
            // invitee no way to reach them. Reply-To goes to the contact so a plain reply works.
            [SystemEmailTemplates.VisitParticipantInvitation] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.HOST,
                    campus: true, replyTo: EmailReplyToSource.CONTACT),
            [SystemEmailTemplates.VisitStudentInvitation] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.HOST,
                    campus: true, replyTo: EmailReplyToSource.CONTACT),
            [SystemEmailTemplates.VisitDepartmentLeaderInvitation] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.HOST,
                    campus: true, replyTo: EmailReplyToSource.CONTACT),
            [SystemEmailTemplates.VisitDepartmentStaffAssignment] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.HOST,
                    campus: true, replyTo: EmailReplyToSource.CONTACT),
            [SystemEmailTemplates.VisitReminderParticipants] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.HOST,
                    campus: true, replyTo: EmailReplyToSource.CONTACT),

            // ── Logistics ────────────────────────────────────────────────────
            // The department needs to coordinate with whoever asked; that is the Host, or the requester
            // when the visit has no Host yet.
            [SystemEmailTemplates.LogisticsRequestToDepartment] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.HOST_THEN_SENDER,
                    campus: true, replyTo: EmailReplyToSource.CONTACT),
            [SystemEmailTemplates.LogisticsAssigneeAssignment] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.DEPARTMENT_DEFAULT, department: true),
            // A counter-offer: the Host has to be able to reach the department that proposed it.
            [SystemEmailTemplates.LogisticsChangeProposalToHost] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.DEPARTMENT_DEFAULT,
                    department: true, replyTo: EmailReplyToSource.CONTACT),
            [SystemEmailTemplates.LogisticsExpenseReportReminder] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.SENDER, sender: true),

            // ── Reports and the setup-progress update ────────────────────────
            // The setup-progress mail already carried hostName/hostEmail as variables; the block replaces
            // them so the address comes from the visit rather than from whatever the template says.
            [SystemEmailTemplates.VisitSetupProgressUpdate] =
                P(EmailContactRequirement.REQUIRED, EmailContactSource.HOST,
                    campus: true, replyTo: EmailReplyToSource.CONTACT),
            [SystemEmailTemplates.ReportCampusOperation] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.SENDER, sender: true),
            [SystemEmailTemplates.ReportDepartmentCollaboration] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.SENDER, sender: true),
            [SystemEmailTemplates.ReportDepartmentInvoice] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.SENDER, sender: true),
            [SystemEmailTemplates.ReportPersonnelPerformance] =
                P(EmailContactRequirement.OPTIONAL, EmailContactSource.SENDER, sender: true),
        };

    /// <summary>The shipped policy for a template, or the system baseline when it has none of its own.</summary>
    public static EmailContactPolicyResolution For(string? templateCode)
        => templateCode is not null && ByTemplate.TryGetValue(templateCode, out var p) ? p : SystemBaseline;

    /// <summary>Every template that ships an explicit policy — the seed set and the parity-test source.</summary>
    public static IReadOnlyCollection<string> ConfiguredTemplateCodes => ByTemplate.Keys.ToList();

    /// <summary>
    /// Templates whose body must carry <c>{{contactInformationBlock}}</c>. Used by the template-content
    /// validator so an operator is refused at save time rather than at send time.
    /// </summary>
    public static bool RequiresContactBlock(string? templateCode)
        => For(templateCode).Requirement == EmailContactRequirement.REQUIRED;
}
