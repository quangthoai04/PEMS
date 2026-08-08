using System;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Common.Interfaces;

/// <summary>How many messages one recovery sweep actually re-sent, per notification kind.</summary>
public sealed record VisitNotificationRecoveryResult(int Rejections, int ContactExpiries)
{
    public int Total => Rejections + ContactExpiries;
}

/// <summary>
/// Finds visit notifications whose business transition succeeded but whose message did not, and sends
/// them. Runnable from a hosted job or a test.
///
/// <para>
/// It exists because the two notifications it covers cannot be recovered by repeating the action that
/// caused them. Rejecting a campus a second time is refused — the campus is already REJECTED. Re-running
/// the invitation-expiry sweep finds nothing — it selects PENDING rows and the invitation is EXPIRED.
/// In both cases the transition is correct and final, and the only thing missing is the message.
/// </para>
/// <para>
/// So the question it asks is not "did something happen?" but "did something that happened ever produce
/// a successful message?" — answered against the email history, which the dispatcher writes before it
/// hands anything to a provider. Once a message succeeds the transition stops being selected, which is
/// what makes repeated sweeps safe: at most one successful notification per transition, ever.
/// </para>
/// <para>
/// It never reverts a business state to force a retry. An EXPIRED invitation stays EXPIRED.
/// </para>
/// </summary>
public interface IVisitNotificationRecoveryService
{
    /// <param name="vietnamNow">Sweep clock.</param>
    /// <param name="batchSize">Maximum transitions of each kind to examine in one pass.</param>
    Task<VisitNotificationRecoveryResult> RunOnceAsync(
        DateTime vietnamNow, int batchSize, CancellationToken cancellationToken);
}
