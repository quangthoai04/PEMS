using System;
using System.Collections.Generic;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// A per-message change to the reply contact, as the client is allowed to express it.
///
/// <para>
/// Structured fields, never markup. The block's HTML is built by
/// <see cref="EmailContactHtmlRenderer"/> from values the backend resolved or validated, so a sender can
/// change WHO the recipient is told to contact without ever being able to change what the block looks
/// like — or to attribute a hand-typed mailbox to a Host by writing a table that resembles the real one.
/// That is the same line the template screen sits on (configure a block, never author one), moved to the
/// one place a sender needs it.
/// </para>
/// <para>
/// It applies to ONE message. Nothing here writes to <c>email_contact_policies</c>: a Host who names a
/// different colleague on today's logistics request has not changed what tomorrow's request will say.
/// </para>
/// </summary>
/// <param name="Mode">One of <see cref="EmailContactOverrideModes"/>. Required.</param>
/// <param name="UserId">The chosen account, for <c>SYSTEM_USER</c> only.</param>
/// <param name="DisplayName">Manual contact's name. <c>MANUAL</c> only.</param>
/// <param name="RoleLabel">Manual contact's business role. <c>MANUAL</c> only.</param>
/// <param name="Email">Manual contact's address. <c>MANUAL</c> only.</param>
/// <param name="Phone">Manual contact's telephone. <c>MANUAL</c> only.</param>
/// <param name="DepartmentName">Manual contact's unit — descriptive text, never an authorisation.</param>
/// <param name="CampusName">Manual contact's campus — descriptive text, never an authorisation.</param>
/// <param name="ReplyToMode">One of <see cref="EmailContactReplyToModes"/>. Null means POLICY_DEFAULT.</param>
/// <param name="HideForThisEmail">Drop the block from this one message. OPTIONAL policies only.</param>
/// <param name="Reason">Why the sender changed it. Required for <c>MANUAL</c>.</param>
public sealed record EmailContactOverrideInput(
    string? Mode = null,
    ulong? UserId = null,
    string? DisplayName = null,
    string? RoleLabel = null,
    string? Email = null,
    string? Phone = null,
    string? DepartmentName = null,
    string? CampusName = null,
    string? ReplyToMode = null,
    bool? HideForThisEmail = null,
    string? Reason = null)
{
    /// <summary>
    /// True when this instance asks for nothing at all — the shape a client sends when the user opened
    /// the contact editor and closed it again. Treated as "no override" rather than as a validation
    /// error, because refusing it would make an untouched form a reason a message cannot be sent.
    /// </summary>
    public bool IsNoOp =>
        (string.IsNullOrWhiteSpace(Mode) || EmailContactOverrideModes.IsTemplateDefault(Mode))
        && HideForThisEmail != true
        && string.IsNullOrWhiteSpace(ReplyToMode);
}

/// <summary>The three ways one message may answer "who should the recipient contact?".</summary>
public static class EmailContactOverrideModes
{
    /// <summary>Whatever the configured policy resolves. The default, and the only mode with no data.</summary>
    public const string TemplateDefault = "TEMPLATE_DEFAULT";

    /// <summary>
    /// A PEMS account, chosen by id. The client sends the id and nothing else: name, address, telephone,
    /// department and campus are read from <c>users</c> at resolve time, so a chosen contact cannot be
    /// shown to a recipient with details that are not theirs.
    /// </summary>
    public const string SystemUser = "SYSTEM_USER";

    /// <summary>
    /// Somebody with no PEMS account — a caterer, a partner's coordinator. Plain text throughout, and
    /// marked in the audit as hand-entered, because nothing behind it can be verified.
    /// </summary>
    public const string Manual = "MANUAL";

    public static readonly IReadOnlyList<string> All = new[] { TemplateDefault, SystemUser, Manual };

    public static bool IsTemplateDefault(string? mode)
        => string.IsNullOrWhiteSpace(mode)
           || string.Equals(mode!.Trim(), TemplateDefault, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Where replies to this one message should go.</summary>
public static class EmailContactReplyToModes
{
    /// <summary>Use the template's configured <see cref="EmailReplyToSource"/>. The default.</summary>
    public const string PolicyDefault = "POLICY_DEFAULT";

    /// <summary>The contact shown in the block. Only valid when that contact has a usable address.</summary>
    public const string Contact = "CONTACT";

    /// <summary>The account pressing send, read from the database — never from the request body.</summary>
    public const string Sender = "SENDER";

    /// <summary>No Reply-To header at all.</summary>
    public const string None = "NONE";

    public static readonly IReadOnlyList<string> All = new[] { PolicyDefault, Contact, Sender, None };
}

/// <summary>
/// Length ceilings for the hand-entered fields.
///
/// <para>
/// Matched to the columns the same values are read from in the other two modes — <c>users.full_name</c>,
/// <c>users.email</c>, <c>users.phone</c>, <c>departments.name</c>, <c>campuses.name</c> — so a manual
/// contact can never be longer than a real one and the block cannot be used as a place to paste a
/// paragraph into somebody else's email.
/// </para>
/// </summary>
public static class EmailContactOverrideLimits
{
    public const int DisplayNameMax = 150;
    public const int RoleLabelMax = 100;
    public const int EmailMax = 150;
    public const int PhoneMax = 30;
    public const int DepartmentNameMax = 150;
    public const int CampusNameMax = 150;
    public const int ReasonMax = 300;
}

/// <summary>
/// A validated, authorised override — the only form the resolver accepts.
///
/// <para>
/// It cannot be constructed outside this assembly's validator, which is deliberate and the same
/// arrangement <see cref="Common.SystemEmailContent.AuthoredByUser"/> uses: "was this checked?" is
/// answered by the type rather than by remembering to call something.
/// </para>
/// </summary>
public sealed record NormalizedContactOverride
{
    internal NormalizedContactOverride(
        string mode,
        ulong? userId,
        string? displayName,
        string? roleLabel,
        string? email,
        string? phone,
        string? departmentName,
        string? campusName,
        string replyToMode,
        bool hideForThisEmail,
        string? reason)
    {
        Mode = mode;
        UserId = userId;
        DisplayName = displayName;
        RoleLabel = roleLabel;
        Email = email;
        Phone = phone;
        DepartmentName = departmentName;
        CampusName = campusName;
        ReplyToMode = replyToMode;
        HideForThisEmail = hideForThisEmail;
        Reason = reason;
    }

    public string Mode { get; }
    public ulong? UserId { get; }
    public string? DisplayName { get; }
    public string? RoleLabel { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public string? DepartmentName { get; }
    public string? CampusName { get; }
    public string ReplyToMode { get; }
    public bool HideForThisEmail { get; }
    public string? Reason { get; }

    public bool IsSystemUser => Mode == EmailContactOverrideModes.SystemUser;
    public bool IsManual => Mode == EmailContactOverrideModes.Manual;

    /// <summary>True when this override changes nothing the resolver would not have done anyway.</summary>
    public bool ChangesNothing =>
        Mode == EmailContactOverrideModes.TemplateDefault
        && !HideForThisEmail
        && ReplyToMode == EmailContactReplyToModes.PolicyDefault;
}
