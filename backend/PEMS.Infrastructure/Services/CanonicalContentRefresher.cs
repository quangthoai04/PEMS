using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Infrastructure-side implementation of <see cref="ICanonicalContentRefresher"/> — a thin wrapper over
/// <see cref="V2CanonicalRefresh"/> (defined alongside <see cref="VisitSafeEditService"/>, internal to
/// this assembly), so an Application-layer handler like
/// <c>UpdateOperationalContactProfileCommandHandler</c> can recompute canonical content without either
/// duplicating <see cref="V2CanonicalRefresh"/>'s logic or PEMS.Application referencing PEMS.Infrastructure
/// directly (plan CanhIter3FixBug, decision V).
/// </summary>
public sealed class CanonicalContentRefresher : ICanonicalContentRefresher
{
    private readonly IApplicationDbContext _db;

    public CanonicalContentRefresher(IApplicationDbContext db) => _db = db;

    public Task RecomputeAsync(VisitRequest visit, CancellationToken cancellationToken)
        => V2CanonicalRefresh.RecomputeAsync(_db, visit, cancellationToken);
}
