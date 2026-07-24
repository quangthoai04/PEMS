using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Services.VisitFormRead;

/// <summary>
/// The EFFECTIVE per-instance delegation name for list/report/notification surfaces.
///
/// The name always comes from THIS instance's own detail row — never from a sibling campus, and never
/// from the request, which carries no delegation name at all. That holds whether or not the request's
/// campuses agree: when they do agree the value is identical everywhere by construction, so reading the
/// target instance is simply the correct read rather than a special case.
///
/// An instance with no detail row yields null. There is nothing to fall back to, and inventing a name
/// would hide the inconsistency from the surface that displays it.
/// </summary>
public static class VisitInstanceEffectiveName
{
    /// <summary>Batched lookup: instance id → effective delegation name (single JOIN query, no N+1).</summary>
    public static async Task<Dictionary<ulong, string?>> ForInstancesAsync(
        IApplicationDbContext db, IReadOnlyCollection<ulong> instanceIds, CancellationToken ct)
    {
        if (instanceIds.Count == 0) return new Dictionary<ulong, string?>();
        return await db.VisitRequestCampuses
            .Where(c => instanceIds.Contains(c.VisitInstanceId))
            .Select(c => new
            {
                c.VisitInstanceId,
                Name = c.FormDetail != null ? c.FormDetail.DelegationName : null,
            })
            .ToDictionaryAsync(x => x.VisitInstanceId, x => (string?)x.Name, ct);
    }

    /// <summary>In-memory variant for a loaded pair (the caller must have included the instance detail).</summary>
    public static string? Of(VisitRequest request, VisitInstanceFormDetail? instanceDetail)
        => instanceDetail?.DelegationName;
}
