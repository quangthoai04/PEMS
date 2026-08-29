using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.ApiIntegrations;

/// <summary>
/// DB-TXN-008: <c>StartFaceScanCommandHandler</c> and <c>ScanBusinessCardCommandHandler</c> both used
/// to read <c>ApiUsageQuota.UsedCount</c>, compare it to <c>MonthlyLimit</c>, make an external cloud
/// call, and only THEN persist <c>UsedCount += 1</c> as a plain change-tracked property — no lock, no
/// atomic conditional update. Two concurrent calls near the monthly limit could both pass the check
/// (oversell) or the later of two overlapping <c>SaveChangesAsync</c> calls could silently overwrite
/// the earlier one's increment (lost update). The fix, <see cref="ApiUsageQuotaGuard.TryClaimAsync"/>,
/// is what both handlers now call, so proving it race-free proves both callers race-free for this
/// concern.
///
/// <para>
/// Same deterministic shape as <c>UpdateProposedHostConcurrencyTests</c> rather than racing a
/// sub-millisecond window: connection A takes a real <c>SELECT ... FOR UPDATE</c> on the quota row
/// and, while still holding it, sets <c>UsedCount</c> to the limit (simulating "another claim already
/// consumed the last unit"), then commits. Connection B is the real guard, invoked concurrently.
/// <c>ExecuteUpdateAsync</c> issues a single autocommit <c>UPDATE ... WHERE UsedCount &lt; MonthlyLimit</c>
/// that must take the same row's write lock before it can run at all, so B genuinely blocks on A's
/// lock and then re-evaluates the WHERE clause against A's freshly COMMITTED value — not a value B
/// read earlier. A guard that read-then-later-wrote without an atomic conditional update (the original
/// bug) would instead decide off its own stale read and claim a unit that no longer exists.
/// </para>
/// </summary>
public sealed class ApiUsageQuotaGuardConcurrencyTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "IT-QUOTA-TXN008";
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(15);

    private readonly PemsWebApplicationFactory _factory;
    private ulong _apiConfigId;

    public ApiUsageQuotaGuardConcurrencyTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var config = new ApiConfiguration
        {
            ApiCode = $"{TestPrefix}-{Guid.NewGuid():N}"[..40],
            Name = $"{TestPrefix} test config",
            BaseUrl = "https://example.invalid/test",
            Status = "ACTIVE",
            CreatedAt = DateTime.Now,
        };
        db.ApiConfigurations.Add(config);
        await db.SaveChangesAsync();
        _apiConfigId = config.ApiConfigId;
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await FixtureCleanup.For(db)
            .Root("api_configurations", $"api_config_id = {_apiConfigId}")
            .RunAsync();
    }

    [Fact]
    public async Task A_concurrent_claim_cannot_slip_past_a_limit_another_claim_just_reached()
    {
        const string period = "209912"; // far-future period, never touched by real traffic.
        const int monthlyLimit = 1;

        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            seedDb.ApiUsageQuotas.Add(new ApiUsageQuota
            {
                ApiConfigId = _apiConfigId,
                CampusScopeKey = "GLOBAL",
                PeriodYyyymm = period,
                MonthlyLimit = monthlyLimit,
                UsedCount = 0,
                CreatedAt = DateTime.Now,
            });
            await seedDb.SaveChangesAsync();
        }

        var hold = TimeSpan.FromMilliseconds(750);
        var blockedFloor = TimeSpan.FromMilliseconds(400);
        var lockHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Connection A: lock the quota row and push it to the limit while holding the lock.
        var exhausting = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT api_usage_quota_id FROM api_usage_quotas WHERE api_config_id = {_apiConfigId} AND campus_scope_key = 'GLOBAL' AND period_yyyymm = {period} FOR UPDATE");

            lockHeld.SetResult();
            await Task.Delay(hold);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE api_usage_quotas SET used_count = {monthlyLimit} WHERE api_config_id = {_apiConfigId} AND campus_scope_key = 'GLOBAL' AND period_yyyymm = {period}");

            await tx.CommitAsync();
        });

        // Connection B: the real guard, invoked while A still holds the row locked.
        var claiming = Task.Run(async () =>
        {
            await lockHeld.Task;

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var blocked = Stopwatch.StartNew();
            var result = await ApiUsageQuotaGuard.TryClaimAsync(
                db, _apiConfigId, "GLOBAL", period, monthlyLimit, userId: null, DateTime.Now, CancellationToken.None);
            blocked.Stop();
            return (Result: result, Waited: blocked.Elapsed);
        });

        await exhausting.WaitAsync(LockWait);
        var (result, waited) = await claiming.WaitAsync(LockWait);

        Assert.True(waited >= blockedFloor,
            $"The claim returned after only {waited.TotalMilliseconds:F0} ms while connection A held the row "
            + $"locked for {hold.TotalMilliseconds:F0} ms — it did not actually contend on the row lock.");

        // Having waited, it saw the committed UsedCount == MonthlyLimit and correctly refused the claim
        // — a stale-read guard would have claimed it anyway (oversell).
        Assert.Null(result);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await checkDb.ApiUsageQuotas.AsNoTracking().FirstAsync(
            q => q.ApiConfigId == _apiConfigId && q.CampusScopeKey == "GLOBAL" && q.PeriodYyyymm == period);
        // Still exactly monthlyLimit — B's refused claim did not sneak an extra increment in anywhere.
        Assert.Equal(monthlyLimit, row.UsedCount);
    }

    [Fact]
    public async Task An_uncontended_claim_creates_the_row_and_increments_it_by_exactly_one()
    {
        const string period = "209911";

        var result = await ClaimOnFreshScopeAsync(period, defaultMonthlyLimit: 5);

        Assert.NotNull(result);
        Assert.Equal(1, result!.UsedCount);
        Assert.Equal(5, result.MonthlyLimit);
        Assert.NotNull(result.LastUsedAt);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await checkDb.ApiUsageQuotas.AsNoTracking().FirstAsync(
            q => q.ApiConfigId == _apiConfigId && q.CampusScopeKey == "GLOBAL" && q.PeriodYyyymm == period);
        Assert.Equal(1, row.UsedCount);
    }

    private async Task<ApiUsageQuota?> ClaimOnFreshScopeAsync(string period, int defaultMonthlyLimit)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await ApiUsageQuotaGuard.TryClaimAsync(
            db, _apiConfigId, "GLOBAL", period, defaultMonthlyLimit, userId: null, DateTime.Now, CancellationToken.None);
    }
}
