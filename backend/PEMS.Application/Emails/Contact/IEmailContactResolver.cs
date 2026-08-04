using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// What a send knows about itself when it asks "who should the recipient contact?".
///
/// <para>
/// <see cref="VisitInstanceId"/> is the per-CAMPUS id, never the visit request id, and the distinction is
/// load-bearing: a request spanning three campuses has three Hosts, and resolving from the request would
/// let a guest invited to Hà Nội be told to contact the Host in Cần Thơ. There is deliberately no
/// overload that takes a request id.
/// </para>
/// </summary>
/// <param name="TemplateCode">Selects the policy.</param>
/// <param name="Language">VI or EN — drives the role labels inside the block.</param>
/// <param name="VisitInstanceId">The campus-scoped visit, when the send belongs to one.</param>
/// <param name="CampusId">Used by CAMPUS_DEFAULT when there is no visit.</param>
/// <param name="DepartmentId">Used by DEPARTMENT_DEFAULT.</param>
/// <param name="SenderUserId">The account performing the action, if any.</param>
public sealed record EmailContactRequest(
    string TemplateCode,
    string Language,
    ulong? VisitInstanceId = null,
    ulong? CampusId = null,
    ulong? DepartmentId = null,
    ulong? SenderUserId = null);

/// <summary>The contact, the policy that produced it, and the block ready to inject.</summary>
/// <param name="Policy">The effective policy after the cascade.</param>
/// <param name="Contact">Null when nothing resolved — only legal for a non-REQUIRED policy.</param>
/// <param name="BlockHtml">Trusted HTML, or empty when the policy renders no block.</param>
/// <param name="ReplyTo">The address the message's Reply-To should carry, or null to leave it alone.</param>
public sealed record EmailContactResolution(
    EmailContactPolicyResolution Policy,
    EmailContactInformation? Contact,
    string BlockHtml,
    EmailContactAddress? ReplyTo)
{
    /// <summary>
    /// Which of <see cref="EmailContactOverrideModes"/> produced <see cref="Contact"/>.
    ///
    /// <para>
    /// Reported rather than inferred from <c>Contact.Source</c>, because the two answer different
    /// questions: a chosen colleague and a policy-resolved sender can both arrive as
    /// <see cref="EmailContactSource.SENDER"/>, and only this field distinguishes "the policy said so"
    /// from "somebody decided so for this one message" — which is what the audit row and the preview both
    /// need to say out loud.
    /// </para>
    /// </summary>
    public string Mode { get; init; } = EmailContactOverrideModes.TemplateDefault;

    /// <summary>
    /// True when a block WOULD have rendered and the sender asked for it to be left off this message.
    /// Distinct from an empty <see cref="BlockHtml"/> under a policy that never had one.
    /// </summary>
    public bool HiddenForThisEmail { get; init; }
}

/// <summary>A validated Reply-To address with its display name.</summary>
public sealed record EmailContactAddress(string Email, string? DisplayName);

/// <summary>
/// Resolves the reply contact for one send, applying the configured cascade and failing closed when a
/// template that promises a contact cannot produce one.
/// </summary>
public interface IEmailContactResolver
{
    Task<EmailContactResolution> ResolveAsync(
        EmailContactRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same resolution, with a per-message override the sender asked for.
    ///
    /// <para>
    /// The override is re-validated, re-authorised and re-read from the database HERE, on every call, and
    /// that is the point of routing it through the resolver rather than letting a caller assemble a
    /// contact and hand it over. Preview and send therefore run the identical code over the identical
    /// input, so "what the host approved is what the recipient receives" is a property of the design
    /// rather than of two implementations that currently agree.
    /// </para>
    /// <para>
    /// A null override is exactly the two-argument call: no special case, no second path.
    /// </para>
    /// </summary>
    /// <param name="overrideInput">
    /// Raw client input. Deliberately not the normalized type: the validation belongs to whoever applies
    /// the override, so no caller can skip it by constructing something that looks checked.
    /// </param>
    /// <param name="actorUserId">
    /// Who is asking. Required for a <c>SYSTEM_USER</c> override, because the set of people they may name
    /// is theirs, not the message's.
    /// </param>
    Task<EmailContactResolution> ResolveAsync(
        EmailContactRequest request,
        EmailContactOverrideInput? overrideInput,
        ulong? actorUserId,
        CancellationToken cancellationToken = default);
}
