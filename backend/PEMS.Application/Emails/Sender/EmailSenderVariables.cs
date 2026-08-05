using System;
using System.Collections.Generic;
using System.Linq;

namespace PEMS.Application.Emails.Sender;

/// <summary>
/// The names of the six variables that describe WHO SENT a message.
///
/// <para>
/// They replace <c>{{contactInformationBlock}}</c> and the whole contact policy behind it. The difference
/// is not cosmetic. A contact was a CONFIGURED THIRD PARTY — the Host, a campus mailbox, a colleague the
/// sender picked from a list — resolved by a cascade an operator maintained, rendered as backend-built
/// markup an operator could position but never author. A sender is the account that pressed send, and
/// nothing else. There is one answer, it comes from authentication, and it needs no policy to choose it.
/// </para>
/// <para>
/// <b>Ordinary variables, deliberately.</b> They are declared in <c>variables_text</c>, described in
/// <see cref="Common.EmailVariableCatalog"/>, HTML-encoded on substitution and interpolated exactly like
/// <c>{{fullName}}</c> — they are NOT a trusted block. That is what lets an administrator lay the sender
/// information out as a paragraph, a table, or a single line in a footer, and lets a template that has no
/// use for it simply not mention it. A block could only ever be positioned; these can be composed.
/// </para>
/// </summary>
public static class EmailSenderVariableNames
{
    public const string Name = "senderName";
    public const string Role = "senderRole";
    public const string Email = "senderEmail";
    public const string Phone = "senderPhone";
    public const string Department = "senderDepartment";
    public const string Campus = "senderCampus";

    /// <summary>Every sender variable, in the order the variable picker shows them.</summary>
    public static readonly IReadOnlyList<string> All =
        new[] { Name, Role, Email, Phone, Department, Campus };

    private static readonly HashSet<string> Lookup = new(All, StringComparer.Ordinal);

    /// <summary>True when the placeholder name is one of the six.</summary>
    public static bool IsSenderVariable(string? placeholderName)
        => placeholderName is not null && Lookup.Contains(placeholderName);

    /// <summary>The sender variables used anywhere in the given placeholder names, deduplicated.</summary>
    public static IReadOnlyList<string> UsedIn(IEnumerable<string> placeholderNames)
        => placeholderNames
            .Where(IsSenderVariable)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
}

/// <summary>
/// The resolved sender of one message, as values ready to be substituted.
///
/// <para>
/// Every field but <see cref="Name"/> is optional, because every one of them is optional in the database:
/// <c>users.phone</c> is nullable, a user need not belong to a department, and a campus may not be set.
/// A missing value substitutes as the empty string rather than "N/A" — the renderer's contract is that a
/// declared variable always has a value, and an absent telephone number is an absence, not a fault.
/// </para>
/// <para>
/// <b>These are data, never a template.</b> A user whose name legitimately contains braces — or one who
/// typed <c>{{visitCode}}</c> into their profile hoping it would be interpolated — has that text encoded
/// and printed verbatim. The renderer substitutes in ONE pass over the template (a regex replace whose
/// replacement output is never rescanned), so nothing here can introduce a second round of interpolation.
/// See <c>EmailSenderVariableResolverTests.Sender_values_that_look_like_placeholders_are_not_parsed</c>.
/// </para>
/// </summary>
/// <param name="IsSystemSender">
/// True when no human pressed send — a reminder job, an OTP, an account notice — and the values therefore
/// describe the PEMS support unit rather than a person. Carried so a caller can tell the two apart without
/// comparing the name against a configured string.
/// </param>
public sealed record EmailSenderVariables(
    string Name,
    string? Role = null,
    string? Email = null,
    string? Phone = null,
    string? Department = null,
    string? Campus = null,
    bool IsSystemSender = false)
{
    /// <summary>
    /// The six values keyed by variable name, with nulls flattened to empty strings.
    ///
    /// <para>
    /// Returns ALL six regardless of which the template declares. The caller filters down to the declared
    /// set — the renderer refuses a supplied variable the template does not declare — and doing the
    /// filtering there rather than here keeps this type independent of any one template.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, string> ToVariableValues()
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EmailSenderVariableNames.Name] = Name ?? string.Empty,
            [EmailSenderVariableNames.Role] = Role ?? string.Empty,
            [EmailSenderVariableNames.Email] = Email ?? string.Empty,
            [EmailSenderVariableNames.Phone] = Phone ?? string.Empty,
            [EmailSenderVariableNames.Department] = Department ?? string.Empty,
            [EmailSenderVariableNames.Campus] = Campus ?? string.Empty,
        };
}
