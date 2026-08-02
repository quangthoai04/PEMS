namespace PEMS.Application.Emails.Contact;

/// <summary>
/// The system-wide support contact, bound from the <c>SupportContact</c> configuration section.
///
/// <para>
/// Configuration rather than a database row because it is deployment identity, not business data: it does
/// not vary by campus, it is not edited as part of anybody's workflow, and it has to be answerable even
/// for the two account notices that may not read a campus (see
/// <c>EmailContactPolicyDefaults</c>). Putting it in the policy table would also have mixed policy with
/// contact data in the one table built to keep them apart.
/// </para>
/// <para>
/// There is deliberately NO default value. A template classified REQUIRED with source SUPPORT_CONTACT and
/// no configured address fails closed — a fabricated placeholder like "support@example.com" would be a
/// dead instruction shipped to a recipient, which is the defect this work exists to remove.
/// </para>
/// </summary>
public sealed class EmailSupportContactOptions
{
    public const string SectionName = "SupportContact";

    /// <summary>Display name of the unit, e.g. "Bộ phận Quản trị hệ thống PEMS".</summary>
    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>True when there is enough here to put in front of a recipient.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Name)
        && (!string.IsNullOrWhiteSpace(Email) || !string.IsNullOrWhiteSpace(Phone));
}
