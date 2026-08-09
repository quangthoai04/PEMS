using System.Threading;
using System.Threading.Tasks;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Mints and sends the per-campus operational-contact invitation, and provides the row locks the
/// confirmation commands serialize on.
///
/// The raw token exists only inside the email link; the database stores its SHA-256. The two halves
/// are deliberately separate and every caller uses BOTH, in this order: mint INSIDE the business
/// transaction — an invitation and the link that answers it are one fact, so they commit or roll back
/// together — and dispatch AFTER the commit. A rolled-back write must never have emailed anyone, and a
/// crash between commit and send is recovered by a resend, not by a half-sent state.
///
/// Scope is one campus. An invitation names <c>visit_instance_id</c>, and accepting it grants that
/// campus and nothing else; a person invited to three campuses answers three links.
///
/// The generic anonymous email-action handler REJECTS these contexts on purpose: taking on a campus is
/// a grant of authority, so it requires an authenticated session whose address matches the invitation,
/// never mere possession of a link.
/// </summary>
/// <summary>
/// The raw links of one invitation — one per answer, so the confirmation page learns which button was
/// pressed from the token rather than from a query parameter. They exist in memory and in the email
/// only; the database stores their SHA-256.
/// </summary>
public sealed record OperationalContactInvitationTokens(string AcceptToken, string DeclineToken);

public interface IOperationalContactInvitationService
{
    /// <summary>
    /// Mints the invitation's tokens INSIDE the caller's transaction, without sending anything.
    ///
    /// <para>
    /// This half exists because the two writes are not independent. An identity change is only
    /// answerable through a token, so committing the change and then minting the token in a separate
    /// step creates a state nobody can act on and nobody is told about: the transfer is PENDING in the
    /// database, the campus's contact cannot be changed again because a pending change already
    /// exists, no link was ever created, and the API answers 500 — so the UI reports a plain failure
    /// for an operation that half succeeded.
    /// </para>
    /// <para>
    /// Called inside the business transaction, a token failure rolls the whole thing back and the
    /// refusal the user sees is the truth. Does NOT call SaveChanges: the caller's commit is what
    /// makes both halves durable together.
    /// </para>
    /// <para>
    /// Returns null when the invitation is not PENDING or has no address to invite — "there is nothing
    /// to invite" is an answer, not a failure, so it does not roll the caller's write back. A caller
    /// whose write only makes sense WITH a link (every invitation writer here) treats null as
    /// unreachable and never dispatches for it.
    /// </para>
    /// </summary>
    Task<OperationalContactInvitationTokens?> MintInvitationTokensAsync(
        ulong identityChangeId, CancellationToken cancellationToken);

    /// <summary>
    /// Sends the invitation email for tokens already minted and COMMITTED. Best-effort by design: the
    /// business state and the links are durable, so a mail-provider failure must not undo them — it
    /// is recovered by a resend, which is a different thing from the token failure above.
    /// </summary>
    Task DispatchInvitationEmailAsync(
        ulong identityChangeId, OperationalContactInvitationTokens tokens, CancellationToken cancellationToken);

    /// <summary>
    /// Locks (<c>SELECT … FOR UPDATE</c>) and returns the invitation row, tracked, or null. MUST be
    /// called inside the caller's transaction — concurrent accept/decline/resend/replace serialize on
    /// it. (Raw-SQL locking lives here because the Application layer has no relational EF dependency.)
    /// </summary>
    Task<VisitRequestIdentityChange?> LockChangeAsync(ulong identityChangeId, CancellationToken cancellationToken);

    /// <summary>
    /// Locks (FOR UPDATE) and returns the PENDING invitation of ONE CAMPUS, if any (tracked). The DB
    /// pending-guard allows at most one per campus, so the result is unambiguous. Never widened to a
    /// request: a sibling campus's invitation is not this campus's business.
    /// </summary>
    Task<VisitRequestIdentityChange?> LockPendingChangeForInstanceAsync(
        ulong visitInstanceId, CancellationToken cancellationToken);
}
