namespace PEMS.Domain.Constants;

/// <summary>
/// The ONLY accepted values for <c>email_templates.purpose</c>.
///
/// This is deliberately separate from <c>otp_tokens.purpose</c> (see <c>PEMS.Shared.OtpPurpose</c>):
/// the two columns share a name but are unrelated concepts. Before this type existed, the email-template
/// create/update validators reused the OTP allowlist, so the template API rejected every purpose the
/// seed actually stores (ACCOUNT, LOGISTICS, VISIT_PARTICIPANT, …) — a template could not be created or
/// edited at all. Keep the two catalogs apart.
///
/// The DB column is <c>VARCHAR(100)</c>, not an ENUM, so extending this list needs no migration — but it
/// DOES need the seed, the frontend option list and the contract test to be updated in the same change.
/// </summary>
public static class EmailTemplatePurposes
{
    /// <summary>Account creation, activation, email change, role change, staff-leader assignment.</summary>
    public const string Account = "ACCOUNT";

    /// <summary>Authentication flows that are not account lifecycle — password reset.</summary>
    public const string Auth = "AUTH";

    /// <summary>Visit-request OTP and primary-contact claim/transfer.</summary>
    public const string VisitRequest = "VISIT_REQUEST";

    /// <summary>Inviting or assigning people to support a visit.</summary>
    public const string VisitParticipant = "VISIT_PARTICIPANT";

    /// <summary>Scheduled reminders before a visit.</summary>
    public const string VisitReminder = "VISIT_REMINDER";

    /// <summary>Logistics request, assignment, change proposal, expense reminder.</summary>
    public const string Logistics = "LOGISTICS";

    /// <summary>Operational reports and invoices (with PDF attachments).</summary>
    public const string Report = "REPORT";

    /// <summary>Every valid purpose. Used by validators, the contract test and the frontend option list.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Account, Auth, VisitRequest, VisitParticipant, VisitReminder, Logistics, Report,
    };

    public static bool IsValid(string? value)
        => value is not null && All.Contains(value, StringComparer.Ordinal);
}
