using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;

namespace PEMS.IntegrationTests.Database;

/// <summary>
/// Vietnam-time persistence policy against the REAL MySQL pems_test database:
///   AC-05 — every pooled application connection runs with session time_zone '+07:00'
///           so CURRENT_TIMESTAMP / trigger NOW() generate Vietnam wall-clock;
///   plus a diagnostic assertion that NOW() agrees with VietnamTime.Now() regardless of
///   the OS timezone the test process runs under.
/// </summary>
public sealed class VietnamTimePersistenceTests : IAsyncLifetime
{
    private readonly PemsWebApplicationFactory _factory = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private sealed class SessionTimeRow
    {
        public string Tz { get; set; } = "";
        public DateTime DbNow { get; set; }
        public DateTime DbUtc { get; set; }
    }

    [Fact]
    public async Task Application_Connection_Uses_Plus7_Session_TimeZone()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var row = await db.Database
            .SqlQueryRaw<SessionTimeRow>(
                "SELECT @@session.time_zone AS Tz, NOW() AS DbNow, UTC_TIMESTAMP() AS DbUtc")
            .SingleAsync();

        Assert.Equal("+07:00", row.Tz);

        // NOW() must be UTC+7 (CURRENT_TIMESTAMP defaults produce Vietnam wall-clock).
        var offset = row.DbNow - row.DbUtc;
        Assert.InRange(offset.TotalMinutes, 7 * 60 - 1, 7 * 60 + 1);

        // And it must agree with the application clock (small skew window between
        // the DB server clock and this machine).
        var skew = (row.DbNow - VietnamTime.Now()).Duration();
        Assert.True(skew < TimeSpan.FromMinutes(2),
            $"MySQL NOW() ({row.DbNow:O}) drifts {skew} from VietnamTime.Now() — session timezone not applied?");
    }

    [Fact]
    public async Task Pooled_Connections_Keep_Plus7_Across_Scopes()
    {
        // The interceptor must fire on every open — including pooled reuse. Exercise a few
        // scopes/connections in sequence; each one must still see +07:00.
        for (var i = 0; i < 3; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tz = await db.Database
                .SqlQueryRaw<string>("SELECT @@session.time_zone AS Value")
                .SingleAsync();
            Assert.Equal("+07:00", tz);
        }
    }
}
