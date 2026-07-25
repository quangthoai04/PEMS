using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.EmailConfirmation;

/// <summary>
/// P0 #1 proactive expiry sweep: overdue PENDING tokens become EXPIRED; a pending account older than the
/// grace period with no live token is auto-cancelled (deactivated, tokens cancelled, reserved Head slot
/// released); accounts within grace or still holding a live token are left alone; and the sweep is idempotent.
/// </summary>
public class AccountEmailConfirmationMaintenanceTests
{
    private const ulong UserId = 700;

    // FakeDateTimeService.VietnamNow == 2026-07-12 15:00; grace is 7 days → cutoff 2026-07-05 15:00.
    private static readonly DateTime Now = new FakeDateTimeService().VietnamNow;
    private static readonly DateTime Stale = Now.AddDays(-10);   // before cutoff
    private static readonly DateTime Recent = Now.AddDays(-1);   // after cutoff

    private static AccountEmailConfirmationMaintenance NewSut(TestApplicationDbContext db) => new(db, new FakeDateTimeService());

    private static User AddPendingUser(TestApplicationDbContext db, DateTime createdAt, ulong id = UserId)
    {
        var user = Uc106TestData.CreateUser(id, Uc106TestData.StaffRoleId, UserSubRoles.Leader, 1);
        user.Email = $"pending{id}@fpt.edu.vn";
        user.Status = UserStatuses.PendingEmailConfirmation;
        user.CreatedAt = createdAt;
        db.Users.Add(user);
        return user;
    }

    private static void AddToken(TestApplicationDbContext db, DateTime expiresAt, ulong userId = UserId, string status = "PENDING")
    {
        db.AccountEmailConfirmations.Add(new AccountEmailConfirmation
        {
            UserId = userId,
            TargetEmail = $"pending{userId}@fpt.edu.vn",
            TokenHash = $"h{userId}-{expiresAt.Ticks}",
            Status = status,
            ExpiresAt = expiresAt,
            CreatedAt = Now.AddDays(-8),
        });
    }

    [Fact]
    public async Task Overdue_token_is_expired_but_a_live_token_is_left_alone()
    {
        var db = TestApplicationDbContext.Create();
        AddPendingUser(db, Recent);                       // young account so it is NOT cancelled
        AddToken(db, expiresAt: Now.AddHours(-1));        // overdue
        AddToken(db, expiresAt: Now.AddDays(2), userId: UserId);   // live
        db.SaveChanges();

        var result = await NewSut(db).RunAsync(CancellationToken.None);

        Assert.Equal(1, result.TokensExpired);
        Assert.Equal(0, result.AccountsCancelled);
        Assert.Equal(1, await db.AccountEmailConfirmations.CountAsync(c => c.Status == AccountEmailConfirmationStatuses.Expired));
        Assert.Equal(1, await db.AccountEmailConfirmations.CountAsync(c => c.Status == AccountEmailConfirmationStatuses.Pending));
    }

    [Fact]
    public async Task Stale_pending_account_with_no_live_token_is_auto_cancelled_and_reservation_released()
    {
        var db = TestApplicationDbContext.Create();
        AddPendingUser(db, Stale);
        AddToken(db, expiresAt: Now.AddHours(-2));         // overdue → expired → no live token
        var campus = Uc106TestData.CreateCampus(1);
        campus.IcHeadUserId = UserId;                      // reserved by the pending user
        db.Campuses.Add(campus);
        db.SaveChanges();

        var result = await NewSut(db).RunAsync(CancellationToken.None);

        Assert.Equal(1, result.AccountsCancelled);
        Assert.True(result.ReservationsReleased >= 1);
        Assert.Equal(UserStatuses.Inactive, (await db.Users.SingleAsync()).Status);
        Assert.Null((await db.Campuses.SingleAsync()).IcHeadUserId);
        Assert.Equal(AccountEmailConfirmationStatuses.Cancelled, (await db.AccountEmailConfirmations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Recent_pending_account_is_not_cancelled()
    {
        var db = TestApplicationDbContext.Create();
        AddPendingUser(db, Recent);
        AddToken(db, expiresAt: Now.AddHours(-2));   // even with an overdue token, the account is within grace
        db.SaveChanges();

        var result = await NewSut(db).RunAsync(CancellationToken.None);

        Assert.Equal(0, result.AccountsCancelled);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, (await db.Users.SingleAsync()).Status);
    }

    [Fact]
    public async Task Old_pending_account_with_a_live_token_is_not_cancelled()
    {
        var db = TestApplicationDbContext.Create();
        AddPendingUser(db, Stale);
        AddToken(db, expiresAt: Now.AddDays(3));   // still live (e.g. an admin resent recently)
        db.SaveChanges();

        var result = await NewSut(db).RunAsync(CancellationToken.None);

        Assert.Equal(0, result.AccountsCancelled);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, (await db.Users.SingleAsync()).Status);
    }

    [Fact]
    public async Task Sweep_is_idempotent()
    {
        var db = TestApplicationDbContext.Create();
        AddPendingUser(db, Stale);
        AddToken(db, expiresAt: Now.AddHours(-2));
        db.SaveChanges();

        await NewSut(db).RunAsync(CancellationToken.None);
        var second = await NewSut(db).RunAsync(CancellationToken.None);   // nothing left to do

        Assert.Equal(0, second.TokensExpired);
        Assert.Equal(0, second.AccountsCancelled);
    }
}
