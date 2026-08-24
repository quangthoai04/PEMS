using System.Threading;
using System.Threading.Tasks;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// Recomputes a request's canonical scope/mixed/fingerprint projection from its CURRENT persisted
/// per-campus state. A thin Application-layer port (plan CanhIter3FixBug, decision V) over
/// <c>V2CanonicalRefresh.RecomputeAsync</c>, which lives in PEMS.Infrastructure and cannot be called
/// directly from Application-layer handlers — this interface is the seam so
/// <c>UpdateOperationalContactProfileCommandHandler</c> can keep canonical content in sync exactly like
/// <c>VisitSafeEditService</c> already does, without either duplicating the computation or inverting the
/// project's dependency direction.
/// </summary>
public interface ICanonicalContentRefresher
{
    /// <summary>
    /// Recomputes <c>VisitScope</c>/<c>HasMixedCampusDetails</c>/<c>BusinessFingerprint</c> onto the
    /// TRACKED <paramref name="visit"/> entity from its currently-loaded campus instances and guest
    /// members. Does not call SaveChanges — the caller's own transaction/commit makes it durable.
    /// </summary>
    Task RecomputeAsync(VisitRequest visit, CancellationToken cancellationToken);
}
