namespace PEMS.Application.Emails.Sender;

/// <summary>
/// The identity a message sends under when nobody pressed send, bound from the <c>SupportContact</c>
/// configuration section.
///
/// <para>
/// The section name is deliberately the one the removed contact feature already used, so no deployment
/// has to change a setting to keep working. What changed is what the values MEAN: they used to be one of
/// six selectable contact sources, chosen per template by a policy cascade; they are now the answer to a
/// single question — who is this message from, when the answer is "the system".
/// </para>
/// <para>
/// Configuration rather than a database row because it is deployment identity, not business data: it does
/// not vary by campus, nobody edits it as part of a workflow, and it has to be answerable for the two
/// account notices that may not read a campus at all (the address that was just unlinked may belong to a
/// stranger reached by a typo, so naming their campus would disclose which one the account belongs to).
/// </para>
/// <para>
/// There is deliberately NO fabricated default. A template that prints <c>{{senderEmail}}</c> with nothing
/// configured prints an empty string — an absence a reader can see — rather than a plausible-looking
/// <c>support@example.com</c> that quietly swallows replies.
/// </para>
/// </summary>
public sealed class EmailSystemSenderOptions
{
    public const string SectionName = "SupportContact";

    /// <summary>Display name of the unit, e.g. "Bộ phận hỗ trợ PEMS".</summary>
    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>
    /// The unit label printed for <c>{{senderDepartment}}</c>. Defaults to "PEMS" rather than being left
    /// empty: a system message has no department, and printing the product name is both true and the thing
    /// a recipient can act on.
    /// </summary>
    public string? Department { get; set; }

    /// <summary>True when there is enough here to put in front of a recipient.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Name)
        && (!string.IsNullOrWhiteSpace(Email) || !string.IsNullOrWhiteSpace(Phone));
}
