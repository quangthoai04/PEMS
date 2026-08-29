using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Notifications;

/// <summary>
/// DB-TXN-010: <c>NotificationService.CreateManyAsync</c> proactively checks for an existing
/// <c>(RecipientUserId, DedupeKey)</c> pair before inserting - a plain read with nothing holding the
/// gap shut. Two callers racing the exact same dedupe key (two overlapping reminder-job ticks, two
/// requests hitting the same idempotent notify path) could both pass that check and then one of them
/// would lose the <c>uq_notifications_recipient_dedupe</c> unique-constraint race at
/// <c>SaveChangesAsync</c>, throwing an unhandled <c>DbUpdateException</c> out of what is usually a
/// side effect inside a larger caller's own business transaction.
///
/// <para>
/// Rather than trying to hit one exact race window with two threads and a barrier (unreliable for a
/// window this narrow - see <c>UpdateProposedHostConcurrencyTests</c>'s own reasoning for why that
/// approach was abandoned there), this fires many concurrent identical calls at once. Real network
/// round trips and thread scheduling jitter across that many overlapping attempts make at least one
/// genuine race near-certain, without needing to instrument the service for testability.
/// </para>
/// </summary>
public sealed class NotificationDedupeConcurrencyTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "IT-NOTIF-TXN010";
    private const int Concurrency = 10;

    private readonly PemsWebApplicationFactory _factory;
    private ulong _recipientUserId;

    public NotificationDedupeConcurrencyTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var visitorRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Visitor).Select(r => r.RoleId).FirstAsync();

        var recipient = new User
        {
            FullName = $"{TestPrefix} Recipient",
            Email = $"recipient_{Guid.NewGuid():N}@pems.test",
            RoleId = visitorRoleId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(recipient);
        await db.SaveChangesAsync();
        _recipientUserId = recipient.UserId;
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await FixtureCleanup.For(db)
            .Root("users", $"user_id = {_recipientUserId}")
            .RunAsync();
    }

    [Fact]
    public async Task Concurrent_creates_with_the_same_dedupe_key_never_throw_and_leave_exactly_one_row()
    {
        var dedupeKey = $"{TestPrefix}-{Guid.NewGuid():N}";

        var attempts = Enumerable.Range(0, Concurrency).Select(i => Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var service = new NotificationService(db);

            return await Record.ExceptionAsync(() => service.CreateAsync(
                new CreateNotificationRequest(
                    _recipientUserId,
                    "Test notification",
                    $"Attempt {i}",
                    "IT_TEST_DEDUPE",
                    DedupeKey: dedupeKey),
                CancellationToken.None));
        })).ToArray();

        var results = await Task.WhenAll(attempts);

        // No caller ever saw a raw duplicate-key exception - every race resolved to a graceful no-op.
        Assert.All(results, ex => Assert.Null(ex));

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await checkDb.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == _recipientUserId && n.DedupeKey == dedupeKey)
            .ToListAsync();

        // Exactly one survivor: every concurrent attempt raced the same key, and the constraint (now
        // recovered from gracefully instead of crashing) still did its job of allowing only one.
        Assert.Single(rows);
    }

    [Fact]
    public async Task An_uncontended_create_with_a_dedupe_key_still_succeeds()
    {
        var dedupeKey = $"{TestPrefix}-solo-{Guid.NewGuid():N}";

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = new NotificationService(db);

        var thrown = await Record.ExceptionAsync(() => service.CreateAsync(
            new CreateNotificationRequest(
                _recipientUserId, "Solo", "Solo attempt", "IT_TEST_DEDUPE", DedupeKey: dedupeKey),
            CancellationToken.None));
        Assert.Null(thrown);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await checkDb.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == _recipientUserId && n.DedupeKey == dedupeKey)
            .ToListAsync();
        Assert.Single(rows);
    }
}
