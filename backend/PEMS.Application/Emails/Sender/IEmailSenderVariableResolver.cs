using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Emails.Sender;

/// <summary>
/// Answers "who is this message from" for one send, from the database and the configuration — never from
/// anything the client sent.
///
/// <para>
/// <b>The actor id is the only input that identifies a person, and it does not come from a request body.</b>
/// Callers pass the id already recorded against the message (<c>SentBy</c>), which the dispatcher takes
/// from the authenticated principal. A resolver that accepted a name and an address would let anybody
/// present any identity as the sender of an official message — the exact attack the removed contact
/// feature spent a validator, a candidate service and an audit row defending against. Here there is
/// nothing to defend: the only thing a client can influence is the wording, and the wording is not an
/// identity.
/// </para>
/// </summary>
public interface IEmailSenderVariableResolver
{
    /// <summary>
    /// The sender values for one message.
    /// </summary>
    /// <param name="actorUserId">
    /// The account this send is recorded against, or null for mail nobody pressed send on. Null — and an
    /// id that resolves to no active user — both produce the system sender: a background reminder and a
    /// message from a since-deactivated account are the same situation from the recipient's point of view,
    /// and inventing a person for either would be worse than naming the support unit.
    /// </param>
    /// <param name="templateCode">
    /// Read for its capability. A <c>NOT_AVAILABLE</c> template resolves the system sender rather than a
    /// person, so that a body which somehow acquired <c>{{senderName}}</c> cannot print the operator who
    /// triggered an OTP.
    /// </param>
    Task<EmailSenderVariables> ResolveAsync(
        ulong? actorUserId,
        string? templateCode,
        CancellationToken cancellationToken = default);
}
