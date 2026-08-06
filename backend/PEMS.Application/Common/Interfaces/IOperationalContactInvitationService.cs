using System.Threading;
using System.Threading.Tasks;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Mints and sends the per-campus operational-contact invitation, and provides the row locks the
/// confirmation commands serialize on.
///
/// The raw token exists only inside the email link; the database stores its SHA-256. Sending happens
/// AFTER the business transaction commits — a rolled-back write must never have emailed anyone, and a
/// crash between commit and send is recovered by a resend, not by a half-sent state.
///
/// Scope is one campus. An invitation names <c>visit_instance_id</c>, and accepting it grants that
/// campus and nothing else; a person invited to three campuses answers three links.
///
/// The generic anonymous email-action handler REJECTS these contexts on purpose: taking on a campus is
/// a grant of authority, so it requires an authenticated session whose address matches the invitation,
/// never mere possession of a link.
/// </summary>
public interface IOperationalContactInvitationService
{
    /// <summary>
    /// Mints + persists a token for a PENDING invitation of either kind and sends the matching email.
    /// Returns the raw token (tests/diagnostics) or null when the invitation is no longer PENDING.
    /// Email failure is logged, not thrown: the token stays valid and a resend recovers.
    /// </summary>
    Task<string?> SendInvitationAsync(ulong identityChangeId, CancellationToken cancellationToken);

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
