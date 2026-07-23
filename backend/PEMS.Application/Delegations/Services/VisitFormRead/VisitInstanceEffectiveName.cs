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
/// Phase-F helper: the EFFECTIVE per-instance delegation name for list/report/notification surfaces.
/// A MIXED per-campus v2 request's business content comes from THIS instance's detail — never the
/// global projection (smallest campus), never a sibling. v1 and non-mixed v2 keep the global field,
/// where it is byte-identical to every instance's detail by construction. A mixed v2 row whose detail
/// is missing yields null (no silent global fallback).
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
