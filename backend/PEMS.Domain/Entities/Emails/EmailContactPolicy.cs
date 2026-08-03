using System;
using System.ComponentModel.DataAnnotations.Schema;
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
/// <para>
/// Every column is named EXPLICITLY, like every other entity in this project. Without the attributes
/// EF derives its own names — <c>emailcontactpolicies</c> with PascalCase columns — and none of them
/// exist, so the first query throws <c>Table 'emailcontactpolicies' doesn't exist</c> at send time and
/// takes all fourteen REQUIRED-contact templates down with it. Nothing caught that while the entity was
/// only ever read in unit tests through an in-memory stub, where names do not have to match anything.
/// </para>
[Table("email_contact_policies")]
public class EmailContactPolicy
{
    [Column("email_contact_policy_id")]
    public ulong EmailContactPolicyId { get; set; }

    /// <summary>Which cascade level this row is. Unique together with <see cref="ScopeKey"/>.</summary>
    [Column("scope_type")]
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
    [Column("scope_key")]
    public string? ScopeKey { get; set; }

    [Column("requirement")]
    public EmailContactRequirement? Requirement { get; set; }

    [Column("contact_source")]
    public EmailContactSource? ContactSource { get; set; }

    [Column("show_email")]
    public bool? ShowEmail { get; set; }

    [Column("show_phone")]
    public bool? ShowPhone { get; set; }

    [Column("show_department")]
    public bool? ShowDepartment { get; set; }

    [Column("show_campus")]
    public bool? ShowCampus { get; set; }

    /// <summary>Whether to add a "sent by …" line naming the account that pressed send.</summary>
    [Column("show_sender")]
    public bool? ShowSender { get; set; }

    [Column("heading_vi")]
    public string? HeadingVi { get; set; }

    [Column("heading_en")]
    public string? HeadingEn { get; set; }

    [Column("reply_to_source")]
    public EmailReplyToSource? ReplyToSource { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }
}
