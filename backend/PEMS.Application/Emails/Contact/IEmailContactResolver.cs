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
    EmailContactAddress? ReplyTo);

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
}
