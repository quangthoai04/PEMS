using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.CreateAccount;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.CreateAccount;

/// <summary>
/// P0 #1 email reuse: creating an account for an email that belongs to a never-confirmed pending-flow SHELL
/// (created via this flow, never CONFIRMED/ACTIVE, currently CANCELLED/EXPIRED, no live token) recycles the
/// SAME user row (re-provisioned from scratch, back to PENDING) — no duplicate. Any account that was ever
/// active/confirmed, a legacy account with no confirmation history, or one still awaiting a live
/// confirmation, conflicts as before (no silent reactivation).
/// </summary>
public class CreateAccountRecycleTests
{
    private const string Email = "new.student@fpt.edu.vn";
    private const ulong ExistingId = 900001;

    private static (CreateAccountCommandHandler handler, TestApplicationDbContext db) Build()
    {
        var db = TestApplicationDbContext.Create();
        db.Campuses.Add(Uc106TestData.CreateCampus());
        db.Roles.Add(Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
        db.SaveChanges();

        var dispatcher = new FakeSystemEmailDispatcher();
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var confirmations = new Mock<IAccountEmailConfirmationService>();
        confirmations.Setup(c => c.IssuePendingAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("raw");
        confirmations.Setup(c => c.BuildConfirmUrl(It.IsAny<string>())).Returns("http://x/confirm-email?token=raw");
        confirmations.Setup(c => c.ExpiryHours).Returns(24);

        var handler = new CreateAccountCommandHandler(
            db, new FakeCurrentUserService(), new Mock<IPasswordHasher>().Object, new FakeDateTimeService(),
            new AuthOptions(), dispatcher, notifications.Object, confirmations.Object);
        return (handler, db);
    }

    private static CreateAccountCommand StudentCmd() => new()
    {
        RoleCode = RoleCodes.Student,
        FullName = "Tran Van C",
        Email = Email,
        StudentCode = "SE123456",
    };

    private static void SeedExisting(TestApplicationDbContext db, string status, string? confirmationStatus, DateTime? confExpires = null)
    {
        var user = Uc106TestData.CreateUser(ExistingId, Uc106TestData.StudentRoleId, null, null);
        user.Email = Email;
        user.Status = status;
        user.CreatedVia = CreatedViaValues.ManualCreated;
        db.Users.Add(user);
        if (confirmationStatus is not null)
        {
            db.AccountEmailConfirmations.Add(new AccountEmailConfirmation
            {
                UserId = ExistingId,
                TargetEmail = Email,
                TokenHash = "hh-" + confirmationStatus,
                Status = confirmationStatus,
                ExpiresAt = confExpires ?? new DateTime(2026, 7, 1),
                CreatedAt = new DateTime(2026, 6, 1),
            });
        }
        db.SaveChanges();
    }

    [Fact]
    public async Task Cancelled_never_confirmed_shell_is_recycled_in_place()
    {
        var (handler, db) = Build();
        SeedExisting(db, UserStatuses.Inactive, AccountEmailConfirmationStatuses.Cancelled);

        var res = await handler.Handle(StudentCmd(), CancellationToken.None);

        Assert.Equal(ExistingId, res.UserId);                                  // SAME row reused
        Assert.Single(await db.Users.Where(u => u.Email == Email).ToListAsync());  // no duplicate
        Assert.Equal(UserStatuses.PendingEmailConfirmation, (await db.Users.SingleAsync(u => u.Email == Email)).Status);
    }

    [Fact]
    public async Task Expired_never_confirmed_shell_is_recycled()
    {
        var (handler, db) = Build();
        SeedExisting(db, UserStatuses.PendingEmailConfirmation, AccountEmailConfirmationStatuses.Expired);

        var res = await handler.Handle(StudentCmd(), CancellationToken.None);

        Assert.Equal(ExistingId, res.UserId);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, (await db.Users.SingleAsync(u => u.Email == Email)).Status);
    }

    [Fact]
    public async Task Confirmed_or_active_account_conflicts()
    {
        var (handler, db) = Build();
        SeedExisting(db, UserStatuses.Active, AccountEmailConfirmationStatuses.Confirmed);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(StudentCmd(), CancellationToken.None));
    }

    [Fact]
    public async Task Legacy_account_without_confirmation_history_conflicts()
    {
        var (handler, db) = Build();
        SeedExisting(db, UserStatuses.Inactive, confirmationStatus: null);   // no confirmation rows → not a pending shell

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(StudentCmd(), CancellationToken.None));
    }

    [Fact]
    public async Task Account_still_awaiting_a_live_confirmation_conflicts()
    {
        var (handler, db) = Build();
        SeedExisting(db, UserStatuses.PendingEmailConfirmation, AccountEmailConfirmationStatuses.Pending,
            confExpires: new FakeDateTimeService().VietnamNow.AddDays(1));   // live token — do not hijack

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(StudentCmd(), CancellationToken.None));
    }
}
