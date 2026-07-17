using System.Threading;
using System.Threading.Tasks;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Issues the primary-contact INITIAL_CLAIM invitation for a per-campus v2 request (plan §16.4):
/// mints a single-use email-action token (context <c>VISIT_CONTACT_CLAIM</c>, target
/// <c>VISIT_REQUEST_IDENTITY_CHANGE</c> — the raw token lives ONLY in the email link; the DB stores its
/// SHA-256) and sends the invitation email pointing at the frontend claim page. Called AFTER the creating
/// transaction commits (create-v2 / resend / replace): a rollback never emails anyone, and a crash between
/// commit and send is recovered by the registrant's resend. The generic anonymous email-action handler
/// REJECTS this context — accepting requires an authenticated session whose email matches the claim.
/// </summary>
public interface IVisitContactClaimService
{
    /// <summary>
    /// Mints + persists a claim token for the identity change (must be PENDING) and sends the invitation
    /// email. Returns the raw token (for tests/diagnostics), or null when the claim is not PENDING anymore.
    /// Email failure is logged, not thrown — the token stays valid and the registrant can resend.
    /// </summary>
    Task<string?> SendInvitationAsync(ulong identityChangeId, CancellationToken cancellationToken);

    /// <summary>
    /// Locks (<c>SELECT … FOR UPDATE</c>) and returns the identity-change row, tracked, or null. MUST be
    /// called inside the caller's transaction — concurrent accept/decline/resend/replace serialize on it.
    /// (Raw-SQL locking lives here because the Application layer has no relational EF dependency.)
    /// </summary>
    Task<VisitRequestIdentityChange?> LockClaimAsync(ulong identityChangeId, CancellationToken cancellationToken);

    /// <summary>Locks (FOR UPDATE) and returns the request's PENDING INITIAL_CLAIM, if any (tracked).</summary>
    Task<VisitRequestIdentityChange?> LockPendingInitialClaimAsync(ulong visitRequestId, CancellationToken cancellationToken);
}
