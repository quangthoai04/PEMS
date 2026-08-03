using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// Reads the contact requirement a template is actually configured with, for the callers that need to
/// judge a BODY against it — the editor's contract, the content validator's two write paths.
///
/// <para>
/// Separate from <see cref="IEmailContactPolicyStore"/> because those callers need one field and must not
/// fail when the whole store cannot be read. A database without
/// <c>2026-08-03_email_contact_information_block.sql</c> would otherwise take the template EDITOR down
/// with it: the contract endpoint feeds the compose screen too, and answering it with a 422 about a
/// missing table would replace a working screen with an error over a policy the shipped defaults can
/// answer perfectly well. The settings card still reports the missing patch by name — that is the screen
/// whose job it is.
/// </para>
/// <para>
/// Capability is applied here as well, so an UNSUPPORTED template reads as NONE whatever a stray row
/// says. A row that switched the block on for a credential-bearing mail must not be able to make the
/// block legal in its body by the back door.
/// </para>
/// </summary>
public static class EffectiveContactRequirement
{
    public static async Task<EmailContactRequirement> ResolveAsync(
        IEmailContactPolicyStore store, string? templateCode, CancellationToken cancellationToken)
    {
        if (!EmailContactCapabilities.Supports(templateCode))
            return EmailContactRequirement.NONE;

        var shipped = EmailContactPolicyDefaults.For(templateCode).Requirement;

        if (store is null || string.IsNullOrWhiteSpace(templateCode)) return shipped;

        try
        {
            var policy = await store.ResolveAsync(templateCode!, null, null, cancellationToken);
            return policy.Requirement;
        }
        catch (BusinessRuleException ex) when (
            ex.ErrorCode == EmailErrorCodes.ContactPolicyStoreUnavailable
            || ex.ErrorCode == EmailErrorCodes.ContactConfigurationInvalid)
        {
            // An unreadable store, or a stored combination the store itself refuses. Either way the
            // shipped default is the honest floor: it is what a fresh database would resolve, and it
            // keeps the answer to "may this body keep its block" independent of a fault the operator on
            // this screen cannot fix from here.
            return shipped;
        }
    }
}
