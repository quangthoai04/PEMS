using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.CreateAccount;
using PEMS.Application.Accounts.Commands.ReplaceStaffLeader;
using PEMS.Application.Accounts.Commands.UpdateAccountRole;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts;

/// <summary>
/// The ADMIN boundary on the account-management mutations — ADMIN_ACCOUNT_MANAGEMENT spec
/// §11/§12/§15/§47.
///
/// <para>
/// Hiding a button is not a permission. These tests call the handlers the way a client with a valid
/// ADMIN token and a hand-written request would: no modal, no route guard, no FluentValidation
/// pipeline. Each refusal is asserted together with the absence of its side effects — no user row, no
/// audit entry, no confirmation token, no mail, no notification — because "403 but it wrote anyway"
/// is the failure this boundary exists to prevent. Every case is paired with the equivalent HO or
/// Staff Leader call succeeding on the SAME fixture, so a refusal can never be credited to a fixture
/// that could not have gone through in the first place.
/// </para>
/// </summary>
public class AdminAccountBoundaryTests
{
    private const ulong Campus = Uc106TestData.CampusId;   // 1
    private const ulong AdminRoleId = 1;
    private const ulong HoRoleId = 2;
    private const ulong IcDeptId = 50;
    private const ulong AdminActorId = 700;
    private const ulong HoActorId = 800;

    // ── CreateAccount ───────────────────────────────────────────────────────────────────────────

    private sealed class CreateHarness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();
        public FakeDateTimeService Clock { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public FakeSystemEmailDispatcher Dispatcher { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public Mock<IAccountEmailConfirmationService> Confirmations { get; } = new();
        public CreateAccountCommandHandler Handler { get; }

        public CreateHarness()
        {
            Confirmations.Setup(c => c.IssuePendingAsync(
                    It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("raw-token");
            Confirmations.Setup(c => c.BuildConfirmUrl(It.IsAny<string>()))
                .Returns("http://localhost:5173/confirm-email?token=raw-token");
            Confirmations.Setup(c => c.ExpiryHours).Returns(24);
            Notifications
                .Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Handler = new CreateAccountCommandHandler(
                Db, Actor, Hasher.Object, Clock, new AuthOptions(), Dispatcher,
                Notifications.Object, Confirmations.Object);
        }

        public Task<CreateAccountResponse> Run(CreateAccountCommand cmd)
            => Handler.Handle(cmd, CancellationToken.None);
    }

    private static CreateHarness CreateAccountHarness()
    {
        var h = new CreateHarness();
        h.Db.Campuses.Add(Uc106TestData.CreateCampus());
        h.Db.Roles.AddRange(
            Uc106TestData.CreateRole(AdminRoleId, RoleCodes.Admin),
            Uc106TestData.CreateRole(HoRoleId, RoleCodes.Ho),
            Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff),
            Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
        h.Db.SaveChanges();
        return h;
    }

    private static void ActAsAdmin(FakeCurrentUserService actor)
    {
        actor.UserId = AdminActorId;
        actor.RoleId = AdminRoleId;
        actor.RoleCode = RoleCodes.Admin;
        actor.SubRole = null;
        actor.PrimaryCampusId = Campus;
    }

    [Fact]
    public async Task AdminCannotCreateAnAccount_AndNothingIsProvisioned()
    {
        var h = CreateAccountHarness();
        ActAsAdmin(h.Actor);

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => h.Run(new CreateAccountCommand
        {
            RoleCode = RoleCodes.Student,
            FullName = "Nguyễn Văn A",
            Email = "new.student@fpt.edu.vn",
            StudentCode = "SE123456",
        }));

        Assert.Equal(AccountErrorCodes.AdminAccountCreationNotAllowed, ex.ErrorCode);
        Assert.Equal(403, ex.StatusCode);

        // The guard runs before ANY work: no account, no confirmation token, no mail, no notification,
        // no audit row claiming a create that never happened.
        Assert.Empty(await h.Db.Users.AsNoTracking().ToListAsync());
        Assert.Empty(h.Db.AuditLogs.Local);
        Assert.Empty(h.Dispatcher.Sent);
        h.Confirmations.Verify(c => c.IssuePendingAsync(
            It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        h.Notifications.Verify(n => n.CreateAsync(
            It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ARoleThatIsNeitherHoNorStaffLeaderCannotCreateAnAccount()
    {
        var h = CreateAccountHarness();
        h.Actor.UserId = 950;
        h.Actor.RoleCode = RoleCodes.Department;
        h.Actor.SubRole = UserSubRoles.Leader;

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(new CreateAccountCommand
        {
            RoleCode = RoleCodes.Student,
            FullName = "Nguyễn Văn B",
            Email = "another.student@fpt.edu.vn",
            StudentCode = "SE654321",
        }));

        Assert.Empty(await h.Db.Users.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task StaffLeaderCreateStillWorks()
    {
        // The same fixture, the intended caller: the refusals above are about WHO asked, not about a
        // fixture that could never provision anything.
        var h = CreateAccountHarness();   // default actor = Staff Leader 900, campus 1

        var res = await h.Run(new CreateAccountCommand
        {
            RoleCode = RoleCodes.Student,
            FullName = "Nguyễn Văn C",
            Email = "ok.student@fpt.edu.vn",
            StudentCode = "SE777777",
        });

        Assert.Equal("ok.student@fpt.edu.vn", res.Email);
        Assert.Single(await h.Db.Users.AsNoTracking().ToListAsync());
    }

    // ── UpdateAccountRole ───────────────────────────────────────────────────────────────────────

    private sealed class RoleHarness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();
        public FakeDateTimeService Clock { get; } = new();
        public RecordingSessionService Sessions { get; }
        public FakeSystemEmailDispatcher Dispatcher { get; } = new();
        public RecordingUserMutationLockService Locks { get; } = new();
        public RecordingConfirmationService Confirmations { get; }
        public UpdateAccountRoleCommandHandler Handler { get; }

        public RoleHarness()
        {
            Sessions = new RecordingSessionService(Db);
            Confirmations = new RecordingConfirmationService(Db, Clock);
            Handler = new UpdateAccountRoleCommandHandler(
                Db, Actor, Sessions, Clock, Dispatcher, Locks,
                new PendingAccountEmailChangeService(Db, Confirmations), Confirmations);
        }

        public Task<UpdateAccountRoleResponse> Run(UpdateAccountRoleCommand cmd)
            => Handler.Handle(cmd, CancellationToken.None);
    }

    private static RoleHarness CreateRoleHarness(ulong targetId)
    {
        var h = new RoleHarness();
        h.Db.Campuses.Add(Uc106TestData.CreateCampus());
        h.Db.Roles.AddRange(
            Uc106TestData.CreateRole(AdminRoleId, RoleCodes.Admin),
            Uc106TestData.CreateRole(HoRoleId, RoleCodes.Ho),
            Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff),
            Uc106TestData.CreateRole(Uc106TestData.DepartmentRoleId, RoleCodes.Department),
            Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
        h.Db.Departments.Add(new Department
        {
            DepartmentId = IcDeptId,
            CampusId = Campus,
            Name = "Phòng Hợp tác Quốc tế",
            DepartmentType = "IC",
            Status = EntityStatuses.Active,
            CreatedAt = new DateTime(2026, 1, 1),
        });
        h.Db.Users.Add(Uc106TestData.CreateUser(targetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId, Campus));
        h.Db.SaveChanges();
        return h;
    }

    [Fact]
    public async Task AdminCannotChangeAnAccountRole_AndTakesNoLock()
    {
        const ulong targetId = 100;
        var h = CreateRoleHarness(targetId);
        ActAsAdmin(h.Actor);

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = targetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "SE010101",
        }));

        Assert.Equal(AccountErrorCodes.AdminAccountEditNotAllowed, ex.ErrorCode);
        Assert.Equal(403, ex.StatusCode);

        // Refused before the transaction and the row lock: the target is untouched and nothing was
        // even read under a lock on its behalf.
        var target = await h.Db.Users.AsNoTracking().SingleAsync(u => u.UserId == targetId);
        Assert.Equal(Uc106TestData.StaffRoleId, target.RoleId);
        Assert.Equal(UserSubRoles.Staff, target.SubRole);
        Assert.Empty(h.Locks.LockedUserBatches);
        Assert.Empty(h.Db.AuditLogs.Local);
        Assert.Empty(h.Sessions.RevokeAllCalls);
        Assert.Empty(h.Dispatcher.Sent);
    }

    [Fact]
    public async Task AdminCannotRenameOrRepointAnAccountEmailThroughTheRoleEndpoint()
    {
        const ulong targetId = 101;
        var h = CreateRoleHarness(targetId);
        ActAsAdmin(h.Actor);

        await Assert.ThrowsAsync<AuthBusinessException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = targetId,
            NewRoleCode = RoleCodes.Staff,
            SubRole = UserSubRoles.Staff,
            DepartmentId = IcDeptId,
            FullName = "Tên Mới",
            Email = "moved@fpt.edu.vn",
        }));

        var target = await h.Db.Users.AsNoTracking().SingleAsync(u => u.UserId == targetId);
        Assert.Equal($"User {targetId}", target.FullName);
        Assert.Equal($"user{targetId}@test.local", target.Email);
    }

    [Fact]
    public async Task StaffLeaderRoleChangeStillWorks()
    {
        const ulong targetId = 102;
        var h = CreateRoleHarness(targetId);   // default actor = Staff Leader 900, campus 1

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = targetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "SE020202",
        });

        Assert.Equal(RoleCodes.Student, res.RoleCode);
        Assert.NotEmpty(h.Locks.LockedUserBatches);
    }

    // ── ReplaceStaffLeader ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminCannotReplaceAStaffLeader()
    {
        var db = TestApplicationDbContext.Create();
        var actor = new FakeCurrentUserService();
        ActAsAdmin(actor);
        var clock = new FakeDateTimeService();
        var sessions = new RecordingSessionService(db);
        var dispatcher = new FakeSystemEmailDispatcher();
        var audit = new Mock<ISecurityAuditService>();
        var confirmations = new Mock<IAccountEmailConfirmationService>();

        var handler = new ReplaceStaffLeaderCommandHandler(
            db, actor, sessions, audit.Object, clock, dispatcher, confirmations.Object);

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() =>
            handler.Handle(new ReplaceStaffLeaderCommand
            {
                CampusId = Campus,
                Mode = ReplaceStaffLeaderModes.CreateNewUser,
                FullName = "Người Kế Nhiệm",
                Email = "successor@fpt.edu.vn",
                Reason = "Điều chuyển nhân sự phụ trách Phòng Hợp tác Quốc tế.",
            }, CancellationToken.None));

        Assert.Equal(AccountErrorCodes.AdminAccountEditNotAllowed, ex.ErrorCode);
        Assert.Equal(403, ex.StatusCode);
        Assert.Empty(await db.Users.AsNoTracking().ToListAsync());
        Assert.Empty(dispatcher.Sent);
        Assert.Empty(sessions.RevokeAllCalls);
    }
}
