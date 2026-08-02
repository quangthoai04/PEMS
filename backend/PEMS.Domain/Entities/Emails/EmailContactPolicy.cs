using System;
using PEMS.Domain.Enums;

namespace PEMS.Domain.Entities.Emails;

/// <summary>
/// One level of the "who should the recipient contact, and what may we show about them" configuration.
///
/// <para>
/// This row holds POLICY, never contact DATA. There is no name, address or telephone number on it, and
/// that is the point: an operator may say "this template shows the Host's work email", but may not type
/// in an email and claim it is the Host's. The addresses themselves are read at send time from
/// <c>users</c>, <c>campuses</c> and <c>departments</c>, which is also what keeps them current when
/// somebody changes role.
/// </para>
/// <para>
/// Every optional column is NULLABLE so that <b>unset</b> and <b>false</b> stay distinguishable. A campus
/// row saying "do not show the phone number" is a decision; a campus row that simply says nothing about
/// the phone number must inherit whatever the system default says. Collapsing the two into a non-null
/// boolean would make every unspecified field silently mean "no".
/// </para>
/// </summary>
public class EmailContactPolicy
{
    public ulong EmailContactPolicyId { get; set; }

    /// <summary>Which cascade level this row is. Unique together with <see cref="ScopeKey"/>.</summary>
    public EmailContactScopeType ScopeType { get; set; }

    /// <summary>
    /// Template code, campus id or department id as text; NULL for the single SYSTEM row.
    ///
    /// <para>
    /// A string rather than three nullable foreign keys because the levels are alternatives, not
    /// companions: a row is a template row or a campus row, never both, and three columns of which
    /// exactly one may be set is a constraint the database cannot state as cleanly as a discriminator.
    /// </para>
    /// </summary>
    public string? ScopeKey { get; set; }

    public EmailContactRequirement? Requirement { get; set; }

    public EmailContactSource? ContactSource { get; set; }

    public bool? ShowEmail { get; set; }
    public bool? ShowPhone { get; set; }
    public bool? ShowDepartment { get; set; }
    public bool? ShowCampus { get; set; }

    /// <summary>Whether to add a "sent by …" line naming the account that pressed send.</summary>
    public bool? ShowSender { get; set; }

    public string? HeadingVi { get; set; }
    public string? HeadingEn { get; set; }

    public EmailReplyToSource? ReplyToSource { get; set; }

    public DateTime CreatedAt { get; set; }
    public ulong? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ulong? UpdatedBy { get; set; }
}
