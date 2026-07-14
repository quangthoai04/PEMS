using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.ManageAccountStatus;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.ManageAccountStatus;

/// <summary>
/// Unit tests for the HO scope of <see cref="ManageAccountStatusCommandHandler"/> after HO_BASIC_INFO
/// spec §14: an HO may now enable/disable another HO (any campus) or a Staff Leader, only between
/// ACTIVE and INACTIVE, never LOCKED, and never their own account.
/// </summary>
public class ManageAccountStatusHoScopeTests
{
    private const ulong Campus = Uc106TestData.CampusId;   // 1
    private const ulong HoRoleId = 2;
    private const ulong ActorId = 800;

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new()
        {
            UserId = ActorId, RoleCode = RoleCodes.Ho, SubRole = null, RoleId = HoRoleId, PrimaryCampusId = Campus,
        };
        public FakeDateTimeService Clock { get; } = new();
        public RecordingSessionService Sessions { get; }
        public Mock<ISecurityAuditService> Audit { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public ManageAccountStatusCommandHandler Handler { get; }

        public Harness()
        {
            Sessions = new RecordingSessionService(Db);
            Notifications
                .Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Handler = new ManageAccountStatusCommandHandler(
                Db, Actor, Sessions, Audit.Object, Clock, Notifications.Object);
        }

        public Task<ManageAccountStatusResponse> Run(ManageAccountStatusCommand cmd)
            => Handler.Handle(cmd, CancellationToken.None);
    }

    private static Harness CreateHarness()
    {
        var h = new Harness();
        h.Db.Campuses.Add(Uc106TestData.CreateCampus());
        h.Db.Campuses.Add(Uc106TestData.CreateCampus(2));
        h.Db.Roles.AddRange(
            Uc106TestData.CreateRole(HoRoleId, RoleCodes.Ho),
            Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff),
            Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
        h.Db.Users.Add(Ho(ActorId, Campus));
        h.Db.SaveChanges();
        return h;
    }

    private static User Ho(ulong id, ulong campus, string status = EntityStatuses.Active)
    {
        var u = Uc106TestData.CreateUser(id, HoRoleId, null, null, campus);
        u.Status = status;
        return u;
    }

    [Fact]
    public async Task HoDisablesAnotherHo_OnDifferentCampus_Succeeds_AndRevokesSessions()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, campus: 2));   // different campus
        h.Db.SaveChanges();

        var res = await h.Run(new ManageAccountStatusCommand { UserId = 810, Status = "INACTIVE" });

        Assert.Equal(UserStatuses.Inactive, res.Status);
        var user = await h.Db.Users.SingleAsync(u => u.UserId == 810);
        Assert.Equal(UserStatuses.Inactive, user.Status);
        Assert.Contains(h.Sessions.RevokeAllCalls,
            c => c.UserId == 810 && c.Reason == SessionRevokeReasons.AccountDeactivated);
    }

    [Fact]
    public async Task HoReenablesAnotherHo_ResetsLockoutMetadata()
    {
        var h = CreateHarness();
        var target = Ho(810, campus: Campus, status: UserStatuses.Inactive);
        target.FailedLoginCount = 5;
        target.LockedUntil = new DateTime(2026, 8, 1);
        h.Db.Users.Add(target);
        h.Db.SaveChanges();

        await h.Run(new ManageAccountStatusCommand { UserId = 810, Status = "ACTIVE" });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == 810);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockedUntil);
        Assert.Empty(h.Sessions.RevokeAllCalls);   // enabling never revokes
    }

    [Fact]
    public async Task HoCannotSendLockedStatus()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, campus: Campus));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            h.Run(new ManageAccountStatusCommand { UserId = 810, Status = "LOCKED" }));
    }

    [Fact]
    public async Task HoCannotToggleLockedTarget()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Ho(810, campus: Campus, status: UserStatuses.Locked));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            h.Run(new ManageAccountStatusCommand { UserId = 810, Status = "ACTIVE" }));
    }

    [Fact]
    public async Task HoCannotChangeOwnStatus()
    {
        var h = CreateHarness();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            h.Run(new ManageAccountStatusCommand { UserId = ActorId, Status = "INACTIVE" }));
    }

    [Fact]
    public async Task HoCannotToggleOutOfScopeRole()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(830, Uc106TestData.StudentRoleId, null, null, Campus));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            h.Run(new ManageAccountStatusCommand { UserId = 830, Status = "INACTIVE" }));
    }
}
