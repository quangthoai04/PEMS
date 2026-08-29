using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.ApiIntegrations;

namespace PEMS.Application.ApiIntegrations.Common;

/// <summary>
/// Atomically claims one unit of a monthly API quota (DB-TXN-008).
///
/// <para>
/// <c>StartFaceScanCommandHandler</c> and <c>ScanBusinessCardCommandHandler</c> both used to read
/// <c>UsedCount</c>, compare it to <c>MonthlyLimit</c>, make an external cloud call, and only THEN
/// persist <c>UsedCount += 1</c> as a plain change-tracked property on the next
/// <c>SaveChangesAsync</c> — no lock, no atomic conditional update. Two concurrent calls near the
/// monthly limit could both pass the check and both get billed by the provider (oversell), or the
/// later of two overlapping <c>SaveChangesAsync</c> calls could silently overwrite the earlier one's
/// increment (lost update), since neither request's in-memory <c>UsedCount</c> reflects the other's.
/// </para>
/// </summary>
public static class ApiUsageQuotaGuard
{
    /// <summary>
    /// Gets or creates the quota row for <paramref name="apiConfigId"/>/<paramref name="campusScopeKey"/>/
    /// <paramref name="period"/>, then atomically claims one unit against it (billable whether the
    /// caller's subsequent cloud call succeeds or fails — same rule the two callers already documented).
    /// Returns the updated quota row, or <c>null</c> if the quota is already exhausted (the caller
    /// throws its own quota-exceeded exception in that case).
    /// </summary>
    public static async Task<ApiUsageQuota?> TryClaimAsync(
        IApplicationDbContext db,
        ulong apiConfigId,
        string campusScopeKey,
        string period,
        int defaultMonthlyLimit,
        ulong? userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var quota = await GetOrCreateAsync(
            db, apiConfigId, campusScopeKey, period, defaultMonthlyLimit, userId, now, cancellationToken);

        // Atomic conditional claim: the WHERE is evaluated by MySQL against the actual row at UPDATE
        // time, under the row's own write lock — a second concurrent claim against the same row blocks
        // on that lock and then re-evaluates UsedCount < MonthlyLimit against the first claim's
        // already-committed value, so at most MonthlyLimit claims can ever succeed for one period.
        var claimed = await db.ApiUsageQuotas
            .Where(q => q.ApiUsageQuotaId == quota.ApiUsageQuotaId && q.UsedCount < q.MonthlyLimit)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(q => q.UsedCount, q => q.UsedCount + 1)
                    .SetProperty(q => q.LastUsedAt, now)
                    .SetProperty(q => q.UpdatedAt, now),
                cancellationToken);

        if (claimed == 0)
            return null;

        // ExecuteUpdateAsync writes straight to the database and does not update the change tracker —
        // refresh the in-memory instance so a caller reading quota.UsedCount/LastUsedAt afterward (e.g.
        // for a response DTO or a later unrelated SaveChangesAsync) sees the claim that just landed.
        quota.UsedCount += 1;
        quota.LastUsedAt = now;
        quota.UpdatedAt = now;
        return quota;
    }

    private static async Task<ApiUsageQuota> GetOrCreateAsync(
        IApplicationDbContext db,
        ulong apiConfigId,
        string campusScopeKey,
        string period,
        int defaultMonthlyLimit,
        ulong? userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var quota = await db.ApiUsageQuotas.FirstOrDefaultAsync(
            q => q.ApiConfigId == apiConfigId && q.CampusScopeKey == campusScopeKey && q.PeriodYyyymm == period,
            cancellationToken);
        if (quota is not null)
            return quota;

        quota = new ApiUsageQuota
        {
            ApiConfigId = apiConfigId,
            CampusScopeKey = campusScopeKey,
            PeriodYyyymm = period,
            MonthlyLimit = defaultMonthlyLimit,
            UsedCount = 0,
            CreatedAt = now,
            CreatedBy = userId,
        };
        db.ApiUsageQuotas.Add(quota);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return quota;
        }
        catch (DbUpdateException)
        {
            // Lost the create race to a concurrent first-call-of-the-month — the unique key
            // (ApiConfigId, CampusScopeKey, PeriodYyyymm) rejected our insert. Detach our half-built
            // row (Remove() on a not-yet-saved Added entity just detaches it, no DELETE is issued)
            // and read back the row the other request already committed.
            db.ApiUsageQuotas.Remove(quota);
            return await db.ApiUsageQuotas.FirstOrDefaultAsync(
                q => q.ApiConfigId == apiConfigId && q.CampusScopeKey == campusScopeKey && q.PeriodYyyymm == period,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "ApiUsageQuota insert failed on a unique-key conflict but the row it conflicted with could not be re-read.");
        }
    }
}
