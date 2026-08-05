using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Domain.Constants;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// How many recipients a system template may address, and whether copies are allowed.
/// </summary>
public enum EmailRecipientPolicy
{
    /// <summary>
    /// Exactly one TO, no CC, no BCC, and one message per person. Used for anything carrying an OTP, a
    /// one-time token, a personal action URL or personalised content — putting two such people on one
    /// message hands each of them the other's link.
    /// </summary>
    SingleRecipientNoCopies,

    /// <summary>
    /// The caller decides the recipient list. CC/BCC are permitted only when the caller passes them
    /// explicitly; the template never contributes an address of its own.
    /// </summary>
    CallerControlled,
}

/// <summary>Which language the renderer resolves first for a template.</summary>
public enum EmailLanguagePreference
{
    ViThenEn,
    EnThenVi,
}

/// <summary>
/// Policy metadata for one system template. Deliberately carries NO subject, body or HTML: content is
/// owned by <c>email_templates</c> in the database and nothing else (see decision D-01). If a string in
/// this file ever reads like an email sentence, the contract has been broken.
/// </summary>
public sealed record SystemEmailTemplate(
    string TemplateCode,
    string Purpose,
    EmailRecipientPolicy RecipientPolicy,
    bool HasSensitiveAction,
    IReadOnlyList<string> DeclaredVariables,
    EmailLanguagePreference LanguagePreference = EmailLanguagePreference.ViThenEn)
{
    /// <summary>One message per person — always true for the single-recipient policy.</summary>
    public bool RequiresPerRecipientSend => RecipientPolicy == EmailRecipientPolicy.SingleRecipientNoCopies;

    /// <summary>Whether the dispatcher may accept CC/BCC for this template.</summary>
    public bool AllowsCopies => RecipientPolicy == EmailRecipientPolicy.CallerControlled;
}

/// <summary>
/// The registry of every system-generated email PEMS sends. It is the code-side half of the contract
/// whose database-side half is the <c>email_templates</c> seed; a contract test asserts the two sets are
/// identical in both directions, so neither an orphan template nor an unregistered caller can survive.
///
/// A code appears here ONLY if it has a real production caller. A template with no caller is not added
/// "because the seed has it" — that is how the previous catalog grew nine dead entries.
/// </summary>
public static class SystemEmailTemplates
{
    // ── ACCOUNT ──────────────────────────────────────────────────────────────
    public const string AccountEmailConfirmation = "ACCOUNT_EMAIL_CONFIRMATION";
    public const string AccountPendingEmailChangedOldNotice = "ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE";
    public const string AccountActivated = "ACCOUNT_ACTIVATED";
    public const string AccountEmailChangedOldNotice = "ACCOUNT_EMAIL_CHANGED_OLD_NOTICE";
    public const string AccountEmailChangedNewNotice = "ACCOUNT_EMAIL_CHANGED_NEW_NOTICE";
    public const string AccountRoleChanged = "ACCOUNT_ROLE_CHANGED";
    public const string AccountStaffLeaderAssigned = "ACCOUNT_STAFF_LEADER_ASSIGNED";
    public const string AccountStaffLeaderReplaced = "ACCOUNT_STAFF_LEADER_REPLACED";

    // ── DEPARTMENT PERSONNEL (Department Leader manages their own staff) ──────
    public const string DeptPersonnelAccountDisabled = "DEPT_PERSONNEL_ACCOUNT_DISABLED";
    public const string DeptPersonnelAccountEnabled = "DEPT_PERSONNEL_ACCOUNT_ENABLED";
    public const string DeptLeadershipGranted = "DEPT_LEADERSHIP_GRANTED";
    public const string DeptLeadershipHandedOver = "DEPT_LEADERSHIP_HANDED_OVER";

    // ── AUTH ─────────────────────────────────────────────────────────────────
    public const string AuthPasswordResetOtp = "AUTH_PASSWORD_RESET_OTP";

    // ── VISIT_REQUEST ────────────────────────────────────────────────────────
    public const string VisitRequestOtp = "VISIT_REQUEST_OTP";
    public const string VisitContactClaim = "VISIT_CONTACT_CLAIM";
    public const string VisitContactTransfer = "VISIT_CONTACT_TRANSFER";

    // ── VISIT_PARTICIPANT ────────────────────────────────────────────────────
    public const string VisitParticipantInvitation = "VISIT_PARTICIPANT_INVITATION";
    public const string VisitStudentInvitation = "VISIT_STUDENT_INVITATION";
    public const string VisitDepartmentLeaderInvitation = "VISIT_DEPARTMENT_LEADER_INVITATION";
    public const string VisitDepartmentStaffAssignment = "VISIT_DEPARTMENT_STAFF_ASSIGNMENT";

    // ── VISIT_REMINDER ───────────────────────────────────────────────────────
    public const string VisitReminderHost = "VISIT_REMINDER_HOST";
    public const string VisitReminderParticipants = "VISIT_REMINDER_PARTICIPANTS";

    // ── LOGISTICS ────────────────────────────────────────────────────────────
    public const string LogisticsRequestToDepartment = "LOGISTICS_REQUEST_TO_DEPARTMENT";
    public const string LogisticsAssigneeAssignment = "LOGISTICS_ASSIGNEE_ASSIGNMENT";
    public const string LogisticsChangeProposalToHost = "LOGISTICS_CHANGE_PROPOSAL_TO_HOST";
    public const string LogisticsExpenseReportReminder = "LOGISTICS_EXPENSE_REPORT_REMINDER";

    // ── REPORT ───────────────────────────────────────────────────────────────
    /// <summary>
    /// The Host's manual "cập nhật chuẩn bị" to the guest side, carrying the Schedule Report as an
    /// attachment. Deliberately NOT an invitation: it bears no token and no per-person action link,
    /// which is exactly why it may address a list the Host controls instead of one person at a time.
    /// </summary>
    public const string VisitSetupProgressUpdate = "VISIT_SETUP_PROGRESS_UPDATE";
    public const string ReportCampusOperation = "REPORT_CAMPUS_OPERATION";
    public const string ReportDepartmentCollaboration = "REPORT_DEPARTMENT_COLLABORATION";
    public const string ReportDepartmentInvoice = "REPORT_DEPARTMENT_INVOICE";
    public const string ReportPersonnelPerformance = "REPORT_PERSONNEL_PERFORMANCE";

    private static readonly IReadOnlyDictionary<string, SystemEmailTemplate> ByCode =
        new[]
        {
            // ACCOUNT — every one of these is a single-person notice; none may be copied.
            Single(AccountEmailConfirmation, EmailTemplatePurposes.Account, sensitive: true,
                "fullName", "roleName", "campusName", "expiresInHours"),
            // The two notices below go to the address that was just UNLINKED, which may belong to
            // somebody who has nothing to do with the account — a mistyped address. They therefore carry
            // NO variables at all: no holder name, no new address, no role/campus. Naming the account
            // holder to a stranger is the leak this deliberately avoids.
            Single(AccountPendingEmailChangedOldNotice, EmailTemplatePurposes.Account, sensitive: false),
            Single(AccountEmailChangedOldNotice, EmailTemplatePurposes.Account, sensitive: false),

            Single(AccountActivated, EmailTemplatePurposes.Account, sensitive: false,
                "fullName", "roleName", "campusName"),
            Single(AccountEmailChangedNewNotice, EmailTemplatePurposes.Account, sensitive: false,
                "fullName", "oldEmailMasked"),
            Single(AccountRoleChanged, EmailTemplatePurposes.Account, sensitive: false,
                "fullName", "oldRoleName", "newRoleName", "campusName"),
            // reason is the replacement justification the operator typed; both leader notices already
            // showed it, and dropping it would quietly remove information the recipients rely on.
            Single(AccountStaffLeaderAssigned, EmailTemplatePurposes.Account, sensitive: false,
                "fullName", "campusName", "effectiveDate", "reason"),
            Single(AccountStaffLeaderReplaced, EmailTemplatePurposes.Account, sensitive: false,
                "fullName", "campusName", "successorName", "effectiveDate", "reason"),

            // DEPARTMENT PERSONNEL — the Department Leader's own staff lifecycle. Same purpose as the
            // other account notices because that is what they are; the recipient is always the single
            // account holder the change happened to, so none of them may be copied.
            Single(DeptPersonnelAccountDisabled, EmailTemplatePurposes.Account, sensitive: false,
                "fullName", "departmentName", "reason"),
            Single(DeptPersonnelAccountEnabled, EmailTemplatePurposes.Account, sensitive: false,
                "fullName", "departmentName"),
            Single(DeptLeadershipGranted, EmailTemplatePurposes.Account, sensitive: false,
                "fullName", "departmentName"),
            Single(DeptLeadershipHandedOver, EmailTemplatePurposes.Account, sensitive: false,
                "fullName", "departmentName"),

            // AUTH — carries the reset OTP.
            Single(AuthPasswordResetOtp, EmailTemplatePurposes.Auth, sensitive: true,
                "fullName", "otpCode", "expireMinutes"),

            // VISIT_REQUEST — OTP and per-person action URLs.
            Single(VisitRequestOtp, EmailTemplatePurposes.VisitRequest, sensitive: true,
                "fullName", "otpCode", "expireMinutes"),
            Single(VisitContactClaim, EmailTemplatePurposes.VisitRequest, sensitive: true,
                "contactFullName", "requestCode", "delegationName"),
            Single(VisitContactTransfer, EmailTemplatePurposes.VisitRequest, sensitive: true,
                "contactFullName", "requestCode", "delegationName", "currentContactName"),

            // VISIT_PARTICIPANT — each invitee gets their own accept/decline token.
            Single(VisitParticipantInvitation, EmailTemplatePurposes.VisitParticipant, sensitive: true,
                "recipientName", "delegationName", "campusName", "plannedTime", "hostName", "roleLabel", "hostMessage"),
            Single(VisitStudentInvitation, EmailTemplatePurposes.VisitParticipant, sensitive: true,
                "recipientName", "delegationName", "campusName", "plannedTime", "hostName", "roleLabel", "hostMessage"),
            Single(VisitDepartmentLeaderInvitation, EmailTemplatePurposes.VisitParticipant, sensitive: true,
                "recipientName", "delegationName", "campusName", "plannedTime", "hostName", "roleLabel", "hostMessage"),
            Single(VisitDepartmentStaffAssignment, EmailTemplatePurposes.VisitParticipant, sensitive: true,
                "recipientName", "delegationName", "campusName", "plannedTime", "departmentName"),

            // VISIT_REMINDER — not token-bearing, but a participant reminder must not reveal who else
            // was invited, so it is still sent one message per person.
            Single(VisitReminderHost, EmailTemplatePurposes.VisitReminder, sensitive: false,
                "hostName", "delegationName", "campusName", "plannedStart", "plannedEnd"),
            Single(VisitReminderParticipants, EmailTemplatePurposes.VisitReminder, sensitive: false,
                "recipientName", "delegationName", "campusName", "plannedStart", "plannedEnd"),

            // LOGISTICS — the first three carry one-time accept/decline/approve links.
            //
            // logisticsDescription is the Host's "Mô tả chi tiết" — the work content the department has
            // to act on. It used to travel as "coordinationNote", which is a DIFFERENT field
            // (visit_logistics_items.offline_coordination_note) and could never legitimately appear
            // here: an OFFLINE_COORDINATED item is recorded DONE and sends no email at all, so on this
            // template that variable only ever carried the description under the wrong label, or the
            // "no coordination note" filler when the preview supplied it instead.
            //
            // dueAt is gone too. The Host has no response-deadline field any more — the server derives
            // due_at as usage-start minus 24h for its own scheduling — so printing "Hạn phản hồi" stated
            // a commitment nobody made. The column and its server-side logic are untouched; it is only
            // no longer shown to the department.
            Single(LogisticsRequestToDepartment, EmailTemplatePurposes.Logistics, sensitive: true,
                "departmentLeaderName", "requesterName", "logisticsTitle", "logisticsItemType", "quantity",
                "usageStartAt", "usageEndAt", "logisticsDescription"),
            Single(LogisticsAssigneeAssignment, EmailTemplatePurposes.Logistics, sensitive: true,
                "assigneeName", "logisticsTitle", "dueAt", "campusName", "delegationName"),
            // A proposal is a counter-offer, so the mail states WHAT is proposed against what was asked
            // for — not the rationale alone, which made the Host open the portal to find the numbers
            // they were being asked to approve.
            Single(LogisticsChangeProposalToHost, EmailTemplatePurposes.Logistics, sensitive: true,
                "hostName", "logisticsTitle", "departmentName", "delegationName",
                "originalQuantity", "proposedQuantity", "proposedUsageStartAt", "proposedUsageEndAt",
                "proposedDescription", "proposalNote"),
            CallerControlledTemplate(LogisticsExpenseReportReminder, EmailTemplatePurposes.Logistics,
                "recipientName", "itemTitle", "dueAt", "delegationName"),

            // REPORT — operational documents; the caller owns the distribution list.
            //
            // The setup-progress update sits here rather than under VISIT_PARTICIPANT because it is a
            // document distribution, not an invitation: the Host decides who is on it, the guest side
            // goes in TO and the accepted internal participants in CC, and nothing in the body grants
            // anybody anything. Reusing VISIT_PARTICIPANT_INVITATION would have inherited that
            // template's single-recipient/no-copies policy — one message per person, no CC at all —
            // which is the opposite of what this flow is for.
            // hostEmail used to be declared here, because the body told the guest to reply "so the Host
            // can update it" and the address had to be printed somewhere for that to mean anything. It is
            // gone now: {{contactInformationBlock}} carries the Host's address — along with their role and
            // telephone number, which a bare variable never could — and it resolves from the visit
            // instance rather than from whatever the caller happened to pass. Keeping both would print the
            // same mailbox twice, once without the context.
            CallerControlledTemplate(VisitSetupProgressUpdate, EmailTemplatePurposes.Report,
                "delegationName", "campusName", "plannedStart", "plannedEnd", "hostName"),
            CallerControlledTemplate(ReportCampusOperation, EmailTemplatePurposes.Report,
                "recipientName", "campusName", "periodFrom", "periodTo"),
            CallerControlledTemplate(ReportDepartmentCollaboration, EmailTemplatePurposes.Report,
                "recipientName", "departmentName", "periodFrom", "periodTo"),
            CallerControlledTemplate(ReportDepartmentInvoice, EmailTemplatePurposes.Report,
                "recipientName", "departmentName", "periodFrom", "periodTo"),
            CallerControlledTemplate(ReportPersonnelPerformance, EmailTemplatePurposes.Report,
                "personName", "scopeLabel", "periodFrom", "periodTo"),
        }
        .ToDictionary(t => t.TemplateCode, StringComparer.Ordinal);

    private static SystemEmailTemplate Single(
        string code, string purpose, bool sensitive, params string[] variables)
        => new(code, purpose, EmailRecipientPolicy.SingleRecipientNoCopies, sensitive,
            WithSenderVariables(code, variables));

    private static SystemEmailTemplate CallerControlledTemplate(
        string code, string purpose, params string[] variables)
        => new(code, purpose, EmailRecipientPolicy.CallerControlled, HasSensitiveAction: false,
            WithSenderVariables(code, variables));

    /// <summary>
    /// Appends the six sender variables to every template whose capability permits them.
    ///
    /// <para>
    /// Derived from the capability map rather than listed per template, and the direction matters: the map
    /// is the product decision about which templates may name a sender, so declaring the variables anywhere
    /// it says <c>NOT_AVAILABLE</c> — or omitting them anywhere it does not — would be a drift with no
    /// single place to notice it. Here the two cannot disagree.
    /// </para>
    /// <para>
    /// <b>Declared is not the same as used.</b> A declared variable must be SUPPLIED at render time (the
    /// renderer refuses a template missing a value for one) but need not appear in the body. That is what
    /// makes the sender group an editorial choice: an administrator may add <c>{{senderPhone}}</c> to a
    /// body months from now and it resolves immediately, with no code change and no re-seed — while a
    /// template that never mentions a sender is unaffected by the values being available.
    /// </para>
    /// <para>
    /// The values themselves are merged in by <see cref="ISystemEmailDispatcher"/>, once, rather than by
    /// each of the ~30 call sites. Callers keep passing exactly their own business variables.
    /// </para>
    /// </summary>
    private static string[] WithSenderVariables(string code, string[] variables)
        => Sender.EmailSenderVariableCapabilities.AllowsVariables(code)
            ? variables.Concat(Sender.EmailSenderVariableNames.All).ToArray()
            : variables;

    /// <summary>Every registered template code.</summary>
    public static IReadOnlyCollection<string> AllCodes => (IReadOnlyCollection<string>)ByCode.Keys;

    /// <summary>Every registered template.</summary>
    public static IReadOnlyCollection<SystemEmailTemplate> All => (IReadOnlyCollection<SystemEmailTemplate>)ByCode.Values;

    /// <summary>Looks up a template, or null when the code is not a registered system template.</summary>
    public static SystemEmailTemplate? Find(string? templateCode)
        => templateCode is not null && ByCode.TryGetValue(templateCode, out var t) ? t : null;

    /// <summary>True when the code belongs to a system template (as opposed to user-authored mail).</summary>
    public static bool IsSystemTemplate(string? templateCode) => Find(templateCode) is not null;
}
