using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Authentication.Commands.LoginviaCredentials;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Authentication;

/// <summary>
/// DB-TXN-002: <see cref="LoginviaCredentialsCommandHandler"/> used to increment
/// <c>users.failed_login_count</c> as a plain read-modify-write
/// (<c>user.FailedLoginCount += 1; SaveChangesAsync()</c>) on an entity loaded without any lock. Two
/// concurrent bad-password attempts on the same account both read the same pre-increment value, and
/// the loser's SaveChanges silently overwrote the winner's — one real attempt was never counted, which
/// is exactly the gap a brute-force script racing several connections at once would want.
///
/// <para>
/// A green unit suite cannot see this: EF InMemory has no row locks and its SaveChanges has no
/// last-writer-wins race to lose. This drives several genuinely concurrent MySQL connections at the
/// same account and asserts the FINAL committed count, not an in-memory value any one caller saw.
/// </para>
/// </summary>
public sealed class LoginLockoutConcurrencyTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TestPrefix = "[IT-LOGIN-LOCKOUT] ";
    private const string TargetEmail = "it-login-lockout-target@pems.test";
    private const string CorrectPassword = "CorrectPassword123!";
    private static readonly TimeSpan RaceWait = TimeSpan.FromSeconds(15);

    private readonly PemsWebApplicationFactory _factory;
    private ulong _targetUserId;

    public LoginLockoutConcurrencyTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var studentRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCodes.Student).Select(r => r.RoleId).FirstAsync();
        var campusId = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active).OrderBy(c => c.CampusId)
            .Select(c => c.CampusId).FirstAsync();

        var target = new User
        {
            FullName = $"{TestPrefix}Target",
            Email = TargetEmail,
            RoleId = studentRoleId,
            PrimaryCampusId = campusId,
            StudentCode = $"ITLL{DateTime.Now:HHmmssfff}",
            PasswordHash = hasher.Hash(CorrectPassword),
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(target);
        await db.SaveChangesAsync();
        _targetUserId = target.UserId;
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await FixtureCleanup.For(db)
            .Root("users", $"email = '{TargetEmail}'")
            .RunAsync();
    }

    /// <summary>
    /// Fires several bad-password attempts at the SAME account from genuinely separate connections at
    /// once. The final failed_login_count must equal exactly how many attempts were made - a lost
    /// update means the number is smaller than that.
    /// </summary>
    [Fact]
    public async Task Concurrent_bad_password_attempts_are_all_counted_exactly_once()
    {
        const int concurrentAttempts = 8;

        var tasks = Enumerable.Range(0, concurrentAttempts).Select(_ => Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var handler = CreateHandler(scope);
            // Any outcome is acceptable here (InvalidCredentials or, if this run happens to cross the
            // configured lockout threshold, AccountLocked) - what matters is only the final count below.
            await Record.ExceptionAsync(() => handler.Handle(
                new LoginviaCredentialsCommand { Email = TargetEmail, Password = "DefinitelyWrongPassword!" },
                CancellationToken.None));
        })).ToArray();

        await Task.WhenAll(tasks).WaitAsync(RaceWait);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await checkDb.Users.AsNoTracking().FirstAsync(u => u.UserId == _targetUserId);

        Assert.Equal(concurrentAttempts, user.FailedLoginCount);
    }

    /// <summary>The ordinary, uncontended path still works: one bad attempt, one increment.</summary>
    [Fact]
    public async Task A_single_bad_password_attempt_increments_by_exactly_one()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var handler = CreateHandler(scope);
            await Assert.ThrowsAsync<PEMS.Application.Common.Exceptions.AuthBusinessException>(() => handler.Handle(
                new LoginviaCredentialsCommand { Email = TargetEmail, Password = "DefinitelyWrongPassword!" },
                CancellationToken.None));
        }

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await checkDb.Users.AsNoTracking().FirstAsync(u => u.UserId == _targetUserId);
        Assert.Equal(1, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
    }

    /// <summary>A correct password still resets the counter - the fix must not disturb the success path.</summary>
    [Fact]
    public async Task A_successful_login_resets_the_counter()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var handler = CreateHandler(scope);
            await Assert.ThrowsAsync<PEMS.Application.Common.Exceptions.AuthBusinessException>(() => handler.Handle(
                new LoginviaCredentialsCommand { Email = TargetEmail, Password = "DefinitelyWrongPassword!" },
                CancellationToken.None));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var handler = CreateHandler(scope);
            var response = await handler.Handle(
                new LoginviaCredentialsCommand { Email = TargetEmail, Password = CorrectPassword },
                CancellationToken.None);
            Assert.NotNull(response);
        }

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await checkDb.Users.AsNoTracking().FirstAsync(u => u.UserId == _targetUserId);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
    }

    private LoginviaCredentialsCommandHandler CreateHandler(IServiceScope scope)
    {
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        return new LoginviaCredentialsCommandHandler(
            db,
            sp.GetRequiredService<IPasswordHasher>(),
            sp.GetRequiredService<ISessionService>(),
            sp.GetRequiredService<IJwtTokenService>(),
            sp.GetRequiredService<ISecurityAuditService>(),
            sp.GetRequiredService<IDateTimeService>(),
            sp.GetRequiredService<AuthOptions>(),
            sp.GetRequiredService<IConfiguration>());
    }
}
