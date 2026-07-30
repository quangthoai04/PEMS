using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.ReplaceStaffLeader;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.ReplaceStaffLeader;

/// <summary>
/// Identity (email) behaviour of <see cref="ReplaceStaffLeaderCommandHandler"/> in mode
/// CREATE_NEW_USER. The handler is invoked directly — i.e. exactly like a client that skips the modal
/// and the FluentValidation pipeline — so it must reject a disallowed domain on its own.
///
/// <para>
/// The point of these tests is that the refusal costs NOTHING: the identity check runs before the
/// transaction is even opened, so a rejected replacement leaves the old leader in his seat, creates
/// no account, revokes no session and sends no mail. The happy-path test is what makes that
/// meaningful — it proves the very same fixture DOES go through once the domain is allowed, so the
/// "nothing changed" assertions are attributable to the email and not to a fixture that could never
/// have succeeded.
/// </para>
/// </summary>
public class ReplaceStaffLeaderIdentityTests
{
    private const ulong Campus = Uc106TestData.CampusId;   // 1
    private const ulong HoRoleId = 2;
    private const ulong ActorId = 800;
    private const ulong IcDepartmentId = 60;
    private const ulong OldLeaderId = 900;
    private const string OldLeaderEmail = "old.leader@fpt.edu.vn";
    private const string Reason = "Điều chuyển nhân sự phụ trách Phòng Hợp tác Quốc tế.";

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new()
        {
            UserId = ActorId,
            Email = "ho.actor@fpt.edu.vn",
            RoleId = HoRoleId,
            RoleCode = RoleCodes.Ho,
            SubRole = null,
            PrimaryCampusId = Campus,
        };
        public FakeDateTimeService Clock { get; } = new();
        public RecordingSessionService Sessions { get; }
        public FakeSystemEmailDispatcher Dispatcher { get; } = new();
        public Mock<ISecurityAuditService> Audit { get; } = new();
        public Mock<IAccountEmailConfirmationService> Confirmations { get; } = new();
        public ReplaceStaffLeaderCommandHandler Handler { get; }

        public Harness()
        {
            Sessions = new RecordingSessionService(Db);
            Confirmations.Setup(c => c.IssuePendingAsync(
                    It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("raw-token");
            Confirmations.Setup(c => c.BuildConfirmUrl(It.IsAny<string>()))
                .Returns("http://localhost:5173/confirm-email?token=raw-token");
            Confirmations.Setup(c => c.ExpiryHours).Returns(24);
            Handler = new ReplaceStaffLeaderCommandHandler(
                Db, Actor, Sessions, Audit.Object, Clock, Dispatcher, Confirmations.Object);
        }

        public Task<ReplaceStaffLeaderResponse> Run(ReplaceStaffLeaderCommand cmd)
            => Handler.Handle(cmd, CancellationToken.None);
    }

    /// <summary>Campus 1 with an ACTIVE IC department whose head is the ACTIVE Staff Leader 900.</summary>
    private static Harness CreateHarness()
    {
        var h = new Harness();

        var campus = Uc106TestData.CreateCampus();
        campus.IcHeadUserId = OldLeaderId;
        h.Db.Campuses.Add(campus);

        h.Db.Roles.AddRange(
            Uc106TestData.CreateRole(HoRoleId, RoleCodes.Ho),
            Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff));

        h.Db.Departments.Add(new Department
        {
            DepartmentId = IcDepartmentId,
            CampusId = Campus,
            Name = "Phòng Hợp tác Quốc tế",
            DepartmentType = "IC",
            Status = EntityStatuses.Active,
            HeadUserId = OldLeaderId,
            CreatedAt = new DateTime(2026, 1, 1),
        });

        var oldLeader = Uc106TestData.CreateUser(
            OldLeaderId, Uc106TestData.StaffRoleId, UserSubRoles.Leader, IcDepartmentId, Campus);
        oldLeader.Email = OldLeaderEmail;
        oldLeader.Status = UserStatuses.Active;
        h.Db.Users.Add(oldLeader);

        h.Db.SaveChanges();
        return h;
    }

    private static ReplaceStaffLeaderCommand Cmd(string email) => new()
    {
        CampusId = Campus,
        Mode = ReplaceStaffLeaderModes.CreateNewUser,
        FullName = "Trần Văn Lãnh Đạo",
        Email = email,
        Reason = Reason,
    };

    // ── The refusal ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("new.leader@fe.edu.vn")]            // dropped from the allowlist
    [InlineData("new.leader@yahoo.com")]
    [InlineData("new.leader@student.fpt.edu.vn")]   // subdomain, not an exact match
    [InlineData("new.leader@gmail.com.vn")]         // look-alike, not an exact match
    [InlineData("new.leader@fpt.edu.vn.evil.com")]
    public async Task DisallowedDomain_IsRejectedWithTheSharedMessage(string email)
    {
        var h = CreateHarness();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Run(Cmd(email)));

        Assert.Equal(AccountIdentityRules.EmailDomainNotAllowedMessage, ex.Message);
    }

    /// <summary>
    /// The whole swap is all-or-nothing. A refused email must not leave a half-done replacement
    /// behind: no demotion, no new account, no repointed head, no revoked session, no mail.
    /// </summary>
    [Fact]
    public async Task DisallowedDomain_LeavesTheCampusExactlyAsItWas()
    {
        var h = CreateHarness();

        await Assert.ThrowsAsync<ValidationException>(() => h.Run(Cmd("new.leader@fe.edu.vn")));

        var oldLeader = await h.Db.Users.SingleAsync(u => u.UserId == OldLeaderId);
        Assert.Equal(UserSubRoles.Leader, oldLeader.SubRole);      // still the leader
        Assert.Equal(UserStatuses.Active, oldLeader.Status);

        // No second account was created, and both head references still point at him.
        Assert.Single(await h.Db.Users.ToListAsync());
        Assert.Equal(OldLeaderId, (await h.Db.Campuses.SingleAsync()).IcHeadUserId);
        Assert.Equal(OldLeaderId, (await h.Db.Departments.SingleAsync()).HeadUserId);

        Assert.Empty(h.Sessions.RevokeAllCalls);
        Assert.Empty(h.Dispatcher.Sent);
        Assert.False(await h.Db.AuditLogs.AnyAsync());
        h.Confirmations.Verify(
            c => c.IssuePendingAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── The control: the same fixture succeeds on an allowed domain ───────────

    [Theory]
    [InlineData("new.leader@gmail.com")]
    [InlineData("new.leader@fpt.edu.vn")]
    public async Task AllowedDomain_CompletesTheReplacement(string email)
    {
        var h = CreateHarness();

        var res = await h.Run(Cmd(email));

        var newLeader = await h.Db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(newLeader.UserId, res.NewLeaderUserId);
        Assert.Equal(UserSubRoles.Leader, newLeader.SubRole);
        // A brand-new leader holds the seat but no authority until it confirms its address.
        Assert.Equal(UserStatuses.PendingEmailConfirmation, newLeader.Status);

        var oldLeader = await h.Db.Users.SingleAsync(u => u.UserId == OldLeaderId);
        Assert.Equal(UserSubRoles.Staff, oldLeader.SubRole);       // demoted
        Assert.Equal(newLeader.UserId, (await h.Db.Campuses.SingleAsync()).IcHeadUserId);
        Assert.Equal(newLeader.UserId, (await h.Db.Departments.SingleAsync()).HeadUserId);

        Assert.Contains(h.Sessions.RevokeAllCalls, c => c.UserId == OldLeaderId);
    }

    /// <summary>Casing/whitespace are normalized before the domain is compared, and before storage.</summary>
    [Fact]
    public async Task AllowedDomain_IsNormalizedBeforeItIsStored()
    {
        var h = CreateHarness();

        var res = await h.Run(Cmd("  NEW.Leader@GMAIL.COM  "));

        Assert.Equal("new.leader@gmail.com", res.NewLeaderEmail);
        Assert.True(await h.Db.Users.AnyAsync(u => u.Email == "new.leader@gmail.com"));
    }

    // ── EXISTING_USER never looks at the hidden create-new inputs ─────────────

    /// <summary>
    /// The create-new email field is hidden in EXISTING_USER mode, so whatever stale value the form
    /// happens to be holding must not block promoting an existing IC Staff.
    /// </summary>
    [Fact]
    public async Task ExistingUserMode_IgnoresTheHiddenEmailField()
    {
        var h = CreateHarness();
        var candidate = Uc106TestData.CreateUser(
            901, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDepartmentId, Campus);
        candidate.Email = "ic.staff@fpt.edu.vn";
        candidate.Status = UserStatuses.Active;
        h.Db.Users.Add(candidate);
        h.Db.SaveChanges();

        var res = await h.Run(new ReplaceStaffLeaderCommand
        {
            CampusId = Campus,
            Mode = ReplaceStaffLeaderModes.ExistingUser,
            NewLeaderUserId = 901,
            FullName = "Ai Đó",
            Email = "left.over@fe.edu.vn",   // never read in this mode
            Reason = Reason,
        });

        Assert.Equal(901UL, res.NewLeaderUserId);
        Assert.Equal(UserSubRoles.Leader, (await h.Db.Users.SingleAsync(u => u.UserId == 901)).SubRole);
    }
}
