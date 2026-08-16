using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.CreateAccount;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.CreateAccount;

/// <summary>
/// Corrects an earlier claim (made from reading AccountProvisioningRules.ResolveAsync's
/// `case RoleCodes.Visitor:` branch in isolation) that CreateAccountCommandHandler's ACCOUNT_CREATED
/// notification is reachable with a VISITOR recipient. Tracing every caller confirms it is NOT:
///
/// - An HO caller is pre-gated in Handle() itself ("HO chỉ được tạo tài khoản HO hoặc Staff Leader.")
///   before AccountProvisioningRules.ResolveAsync is ever invoked — VISITOR can never reach it.
/// - A Staff Leader caller goes through AccountProvisioningRules.ResolveStaffLeaderTargetAsync, a
///   SEPARATE method whose switch has no VISITOR case at all (default throws
///   "Staff Leader chỉ được tạo hoặc cập nhật sang Staff, Department Leader hoặc Student.").
/// - No other caller reaches Handle() at all (EnsureCallerMayCreateAccounts refuses everyone else).
///
/// The `case RoleCodes.Visitor:` branch in ResolveAsync is reachable only from
/// UpdateAccountRoleCommandHandler (a role CHANGE on an existing account, not creation) — and that
/// handler has no INotificationService dependency at all, so it never produces an in-app
/// notification either way. ACCOUNT_CREATED-for-Visitor metadata was still added defensively (same
/// guard condition, harmless if never true), but there is currently no live path that exercises it —
/// this is NOT the semantic-metadata gap the earlier investigation claimed it was.
/// </summary>
public class CreateAccountVisitorRoleUnreachableTests
{
    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();
        public FakeDateTimeService Clock { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public FakeSystemEmailDispatcher Dispatcher { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public Mock<PEMS.Application.Accounts.Common.IAccountEmailConfirmationService> Confirmations { get; } = new();
        public CreateAccountCommandHandler Handler { get; }

        public Harness()
        {
            Notifications.Setup(n => n.CreateAsync(
                    It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Handler = new CreateAccountCommandHandler(
                Db, Actor, Hasher.Object, Clock, new AuthOptions(), Dispatcher, Notifications.Object, Confirmations.Object);
        }

        public Task<CreateAccountResponse> Run(CreateAccountCommand cmd) => Handler.Handle(cmd, CancellationToken.None);
    }

    private static CreateAccountCommand VisitorCmd() => new()
    {
        RoleCode = RoleCodes.Visitor,
        FullName = "Nguyễn Văn Khách",
        Email = "guest.candidate@gmail.com",
    };

    [Fact]
    public async Task HoCaller_RequestingVisitorRole_IsRefusedBeforeAnyProvisioningRuleRuns()
    {
        var h = new Harness();
        h.Db.Campuses.Add(Uc106TestData.CreateCampus());
        h.Db.Roles.Add(Uc106TestData.CreateRole(2, RoleCodes.Ho));
        h.Actor.RoleCode = RoleCodes.Ho;
        h.Actor.SubRole = null;
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(VisitorCmd()));
        Assert.Contains("HO chỉ được tạo tài khoản HO hoặc Staff Leader", ex.Message);
        Assert.False(await h.Db.Users.AnyAsync(u => u.Email == "guest.candidate@gmail.com"));
    }

    [Fact]
    public async Task StaffLeaderCaller_RequestingVisitorRole_IsRefused()
    {
        var h = new Harness(); // FakeCurrentUserService defaults to Staff Leader, campus 1
        h.Db.Campuses.Add(Uc106TestData.CreateCampus());
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(VisitorCmd()));
        Assert.Contains("Staff Leader chỉ được tạo hoặc cập nhật sang Staff, Department Leader hoặc Student", ex.Message);
        Assert.False(await h.Db.Users.AnyAsync(u => u.Email == "guest.candidate@gmail.com"));
    }
}
