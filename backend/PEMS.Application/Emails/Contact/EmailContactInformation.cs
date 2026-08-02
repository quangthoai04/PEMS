using System;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// A resolved reply contact — who the recipient should get in touch with, and what is publishable about
/// them.
///
/// <para>
/// Deliberately NOT the same type as a sender or a Host. The three answer different questions and are
/// allowed to differ: Head Office may press send on a mail whose reply contact is a campus Host, and
/// reusing one <c>name/email</c> record for all three is how "who do I reply to" quietly becomes "whoever
/// clicked the button".
/// </para>
/// <para>
/// Every field except <see cref="DisplayName"/> is optional, because every one of them is optional in the
/// database: <c>users.phone</c> is nullable, a department has no address of its own, and a campus may not
/// have had one filled in. A field with no value is omitted from the rendered block rather than shown as
/// "N/A", which tells the recipient nothing and looks like a fault.
/// </para>
/// </summary>
/// <param name="Source">Which resolver branch produced this, for diagnostics and tests.</param>
/// <param name="DisplayName">Person or unit name. The one field that must be present.</param>
/// <param name="RoleLabel">Business role in this context — "người phụ trách tiếp đón", not a system role.</param>
/// <param name="DepartmentName">Only rendered when the policy allows it.</param>
/// <param name="CampusName">Only rendered when the policy allows it.</param>
/// <param name="Email">Work address. Its absence is what makes a REQUIRED policy fail.</param>
/// <param name="Phone">Work telephone. Nullable everywhere, so its absence is never a failure.</param>
public sealed record EmailContactInformation(
    EmailContactSource Source,
    string DisplayName,
    string? RoleLabel = null,
    string? DepartmentName = null,
    string? CampusName = null,
    string? Email = null,
    string? Phone = null)
{
    /// <summary>
    /// Whether this contact is actually actionable. A name with no way to reach it is not a contact —
    /// printing one under "please get in touch" is the original defect wearing a different hat.
    /// </summary>
    public bool IsReachable =>
        !string.IsNullOrWhiteSpace(Email) || !string.IsNullOrWhiteSpace(Phone);
}
