using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.EditPendingAccountEmail;
using PEMS.Application.Accounts.Commands.ResendAccountEmailConfirmation;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.EmailConfirmation;

/// <summary>
/// P0 #1 resend + edit-email for pending accounts: both are admin-authorized (HO / the account's Staff
/// Leader), refuse non-pending accounts, and re-issue a fresh token (which supersedes the old one — no
/// duplicate account). Resend is rate-limited (cooldown + max). Edit validates/normalizes the new email,
/// rejects duplicates, updates the address and re-confirms.
/// </summary>
public class ResendAndEditPendingEmailTests
{
    private const ulong CampusA = 1;               // == FakeCurrentUserService default campus (Staff Leader)
    private const ulong TargetUserId = 700;
    private const string OwnerEmail = "owner@fpt.edu.vn";

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();   // Staff Leader, campus 1
        public FakeDateTimeService Clock { get; } = new();
        public Mock<IAccountEmailConfirmationService> Confirmations { get; } = new();
        public Mock<IEmailService> Email { get; } = new();

        public Harness()
        {
            Confirmations.Setup(c => c.IssuePendingAsync(
                    It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("raw");
            Confirmations.Setup(c => c.BuildConfirmUrl(It.IsAny<string>())).Returns("http://x/confirm-email?token=raw");
            Email.Setup(e => e.TrySendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(EmailDeliveryResult.Sent());
        }

        public ResendAccountEmailConfirmationCommandHandler Resend() => new(Db, Actor, Clock, Confirmations.Object, Email.Object);
        public EditPendingAccountEmailCommandHandler Edit() => new(Db, Actor, Clock, Confirmations.Object, Email.Object);
    }

    private static User SeedUser(Harness h, string? status = null, string email = OwnerEmail, ulong campus = CampusA)
    {
        var user = Uc106TestData.CreateUser(TargetUserId, Uc106TestData.StudentRoleId, null, campus);
        user.Email = email;
        user.Status = status ?? UserStatuses.PendingEmailConfirmation;
        h.Db.Users.Add(user);
        h.Db.SaveChanges();
        return user;
    }

    private static void SeedPendingConfirmation(Harness h, int resendCount, int createdSecondsAgo)
    {
        h.Db.AccountEmailConfirmations.Add(new AccountEmailConfirmation
        {
            UserId = TargetUserId,
            TargetEmail = OwnerEmail,
            TokenHash = new string('a', 64),
            Status = AccountEmailConfirmationStatuses.Pending,
            ExpiresAt = h.Clock.VietnamNow.AddDays(1),
            ResendCount = resendCount,
            CreatedAt = h.Clock.VietnamNow.AddSeconds(-createdSecondsAgo),
        });
        h.Db.SaveChanges();
    }

    // ── Resend ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Unauthorized_actor_cannot_resend()
    {
        var h = new Harness();
        SeedUser(h);
        h.Actor.RoleCode = RoleCodes.Department;   // not HO, not the campus Staff Leader
        h.Actor.SubRole = UserSubRoles.Staff;

        await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None));
    }

    [Fact]
    public async Task Non_pending_account_cannot_resend()
    {
        var h = new Harness();
        SeedUser(h, status: UserStatuses.Active);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None));
        Assert.Equal("ACCOUNT_NOT_PENDING", ex.ErrorCode);
    }

    [Fact]
    public async Task Resend_within_cooldown_is_rejected()
    {
        var h = new Harness();
        SeedUser(h);
        SeedPendingConfirmation(h, resendCount: 0, createdSecondsAgo: 5);   // just issued

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None));
        Assert.Equal("RESEND_TOO_SOON", ex.ErrorCode);
        h.Confirmations.Verify(c => c.IssuePendingAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resend_over_max_limit_is_rejected()
    {
        var h = new Harness();
        SeedUser(h);
        SeedPendingConfirmation(h, resendCount: 5, createdSecondsAgo: 120);   // past cooldown, at max

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None));
        Assert.Equal("RESEND_LIMIT_REACHED", ex.ErrorCode);
    }

    [Fact]
    public async Task Resend_success_reissues_token_and_reports_truthful_status()
    {
        var h = new Harness();
        SeedUser(h);
        SeedPendingConfirmation(h, resendCount: 1, createdSecondsAgo: 120);   // past cooldown, below max

        var res = await h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None);

        Assert.True(res.Success);
        Assert.Equal("SENT", res.EmailNotificationStatus);
        h.Confirmations.Verify(c => c.IssuePendingAsync(TargetUserId, OwnerEmail, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Edit email ─────────────────────────────────────────────────────────
    [Fact]
    public async Task Unauthorized_actor_cannot_edit_email()
    {
        var h = new Harness();
        SeedUser(h);
        h.Actor.PrimaryCampusId = 2;   // Staff Leader of a different campus

        await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Edit().Handle(new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" }, CancellationToken.None));
    }

    [Fact]
    public async Task Edit_non_pending_account_is_rejected()
    {
        var h = new Harness();
        SeedUser(h, status: UserStatuses.Active);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Edit().Handle(new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" }, CancellationToken.None));
        Assert.Equal("ACCOUNT_NOT_PENDING", ex.ErrorCode);
    }

    [Fact]
    public async Task Edit_invalid_email_is_rejected()
    {
        var h = new Harness();
        SeedUser(h);

        await Assert.ThrowsAsync<ValidationException>(
            () => h.Edit().Handle(new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "not-an-email" }, CancellationToken.None));
    }

    [Fact]
    public async Task Edit_duplicate_email_conflicts()
    {
        var h = new Harness();
        SeedUser(h);
        var other = Uc106TestData.CreateUser(701, Uc106TestData.StudentRoleId, null, CampusA);
        other.Email = "taken@fpt.edu.vn";
        h.Db.Users.Add(other);
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Edit().Handle(new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "taken@fpt.edu.vn" }, CancellationToken.None));
        Assert.Equal(AccountErrorCodes.EmailAlreadyExists, ex.ErrorCode);
    }

    [Fact]
    public async Task Edit_success_updates_email_and_reissues_confirmation_for_new_address()
    {
        var h = new Harness();
        SeedUser(h);

        var res = await h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "  New.Owner@FPT.EDU.VN " }, CancellationToken.None);

        Assert.True(res.Success);
        Assert.Equal("new.owner@fpt.edu.vn", res.Email);   // normalized
        Assert.Equal("SENT", res.EmailNotificationStatus);
        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetUserId);
        Assert.Equal("new.owner@fpt.edu.vn", user.Email);
        // A fresh token bound to the NEW email is issued (the old one is superseded / no longer matches).
        h.Confirmations.Verify(c => c.IssuePendingAsync(TargetUserId, "new.owner@fpt.edu.vn", false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
