using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Resolves the real email of a seeded user so a fixture that creates a visit request through the
/// AUTHENTICATED create names the acting user's OWN mailbox.
///
/// That endpoint is self-registration only (<c>RegistrantIdentityRules</c>): the session vouches for the
/// caller's address and nothing else. Fixtures used to hard-code "registrant@example.com" regardless of who
/// was acting, which described a request nobody could actually submit — so tests for unrelated features
/// (expenses, minutes, agendas…) were seeding from an impossible state. Reading the address back from the
/// database keeps those seeds honest without every test having to know what the seed data contains.
/// </summary>
internal static class V2SeedActor
{
    private static readonly ConcurrentDictionary<ulong, string> Cache = new();

    private static string ConnString => DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    /// <summary>The user's email as stored. Cached: a fixture may call this once per seeded request.</summary>
    public static string Email(ulong userId) => Cache.GetOrAdd(userId, id =>
    {
        using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

        return db.Users.AsNoTracking()
            .Where(u => u.UserId == id)
            .Select(u => u.Email)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Seed user {id} has no email — the fixture cannot build a self-registration for it.");
    });
}
