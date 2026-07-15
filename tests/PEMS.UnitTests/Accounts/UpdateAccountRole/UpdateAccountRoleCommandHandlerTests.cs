using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.UpdateAccountRole;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.UpdateAccountRole;

/// <summary>
/// Unit tests for <see cref="UpdateAccountRoleCommandHandler"/> — the Staff Leader "Chỉnh sửa vai
/// trò" flow (UC-100-SL). Covers the STAFF/DEPARTMENT/STUDENT role rules, student_code (MSSV)
/// handling, department-head synchronisation and the caller guards. Runs on EF InMemory (no MySQL,
/// no HTTP): the caller is the default Staff Leader (id 900, campus 1) from the shared harness.
/// </summary>
public class UpdateAccountRoleCommandHandlerTests
{
    private const ulong Campus = Uc106TestData.CampusId;      // 1
    private const ulong IcDeptId = 50;
    private const ulong GeneralDeptId = 60;
    private const ulong TargetId = 100;

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();     // Staff Leader, id 900, campus 1
        public FakeDateTimeService Clock { get; } = new();
        public RecordingSessionService Sessions { get; }
        public Mock<IEmailService> Email { get; } = new();
        public UpdateAccountRoleCommandHandler Handler { get; }

        public Harness()
        {
            Sessions = new RecordingSessionService(Db);
            Email.Setup(e => e.SendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Handler = new UpdateAccountRoleCommandHandler(Db, Actor, Sessions, Clock, Email.Object);
        }

        public Task<UpdateAccountRoleResponse> Run(UpdateAccountRoleCommand cmd)
            => Handler.Handle(cmd, CancellationToken.None);
    }

    private static Department IcDept(bool active = true) => new()
    {
        DepartmentId = IcDeptId,
        CampusId = Campus,
        Name = "Phòng Hợp tác Quốc tế",
        DepartmentType = "IC",
        Status = active ? EntityStatuses.Active : EntityStatuses.Inactive,
        CreatedAt = new DateTime(2026, 1, 1),
    };

    /// <summary>Campus + STAFF/DEPARTMENT/STUDENT roles (+ optionally the IC department).</summary>
    private static Harness CreateHarness(bool withIc = true)
    {
        var h = new Harness();
        h.Db.Campuses.Add(Uc106TestData.CreateCampus());
        h.Db.Roles.AddRange(
            Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff),
            Uc106TestData.CreateRole(Uc106TestData.DepartmentRoleId, RoleCodes.Department),
            Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
        if (withIc) h.Db.Departments.Add(IcDept());
        h.Db.SaveChanges();
        return h;
    }

    private static User Student(ulong id, string? code, ulong campus = Campus)
    {
        var u = Uc106TestData.CreateUser(id, Uc106TestData.StudentRoleId, null, null, campus);
        u.StudentCode = code;
        return u;
    }

    // ── STAFF ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Staff_ResolvesActiveIc_SetsSubRoleStaff_AndClearsStudentCode()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Student(TargetId, "HE160001"));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand { UserId = TargetId, NewRoleCode = RoleCodes.Staff });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal(Uc106TestData.StaffRoleId, user.RoleId);
        Assert.Equal(UserSubRoles.Staff, user.SubRole);
        Assert.Equal(IcDeptId, user.DepartmentId);
        Assert.Equal(Campus, user.PrimaryCampusId);
        Assert.Null(user.StudentCode);
    }

    [Fact]
    public async Task Staff_WithNoActiveIcDepartment_Throws()
    {
        var h = CreateHarness(withIc: false);
        h.Db.Users.Add(Student(TargetId, null));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ValidationException>(() =>
            h.Run(new UpdateAccountRoleCommand { UserId = TargetId, NewRoleCode = RoleCodes.Staff }));
    }

    // ── STUDENT ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Student_TrimsAndSavesCode_ClearsSubRoleAndDepartment()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "  HE160002  ",
        });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal(Uc106TestData.StudentRoleId, user.RoleId);
        Assert.Null(user.SubRole);
        Assert.Null(user.DepartmentId);
        Assert.Equal("HE160002", user.StudentCode);
    }

    [Fact]
    public async Task Student_EmptyCode_Throws()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ValidationException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "   ",
        }));
    }

    [Fact]
    public async Task Student_CodeOver30Chars_Throws()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ValidationException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = new string('X', 31),
        }));
    }

    [Fact]
    public async Task Student_DuplicateCodeOnAnotherUser_Throws()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Student(200, "HE160003"));                                   // owns the code
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160003",
        }));
    }

    [Fact]
    public async Task Student_SameCodeOnSameTarget_IsNotDuplicate()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Student(TargetId, "HE160004"));    // already a STUDENT with this code
        h.Db.SaveChanges();

        // Re-submitting the same MSSV for the same account must not trip the uniqueness check.
        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160004",
        });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal("HE160004", user.StudentCode);
    }

    // ── DEPARTMENT ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Department_AssignsHeadUser_AndSetsLeaderShape()
    {
        var h = CreateHarness();
        h.Db.Departments.Add(Uc106TestData.CreateGeneralDepartment(GeneralDeptId));
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Department,
            DepartmentId = GeneralDeptId,
        });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        var dept = await h.Db.Departments.SingleAsync(d => d.DepartmentId == GeneralDeptId);
        Assert.Equal(Uc106TestData.DepartmentRoleId, user.RoleId);
        Assert.Equal(UserSubRoles.Leader, user.SubRole);
        Assert.Equal(GeneralDeptId, user.DepartmentId);
        Assert.Equal(TargetId, dept.HeadUserId);
    }

    [Fact]
    public async Task Department_WithoutDepartmentId_Throws()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ValidationException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Department,
            DepartmentId = null,
        }));
    }

    [Fact]
    public async Task Department_WithOtherHead_Throws()
    {
        var h = CreateHarness();
        var dept = Uc106TestData.CreateGeneralDepartment(GeneralDeptId);
        dept.HeadUserId = 999;                                  // some other leader
        h.Db.Departments.Add(dept);
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Department,
            DepartmentId = GeneralDeptId,
        }));
    }

    [Fact]
    public async Task LeavingDepartment_ClearsOldHead_WhenTargetWasHead()
    {
        var h = CreateHarness();
        var dept = Uc106TestData.CreateGeneralDepartment(GeneralDeptId);
        dept.HeadUserId = TargetId;
        h.Db.Departments.Add(dept);
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.DepartmentRoleId, UserSubRoles.Leader, GeneralDeptId));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160005",
        });

        var oldDept = await h.Db.Departments.SingleAsync(d => d.DepartmentId == GeneralDeptId);
        Assert.Null(oldDept.HeadUserId);
    }

    // ── Caller guards ─────────────────────────────────────────────────────────

    [Fact]
    public async Task StaffLeader_CannotChangeOwnRole()
    {
        var h = CreateHarness();
        // Target id == actor id (900).
        h.Db.Users.Add(Uc106TestData.CreateUser(h.Actor.UserId!.Value, Uc106TestData.StaffRoleId, UserSubRoles.Leader, IcDeptId));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = h.Actor.UserId!.Value,
            NewRoleCode = RoleCodes.Staff,
        }));
    }

    [Fact]
    public async Task StaffLeader_CannotEditTargetInAnotherCampus()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, departmentId: null, campusId: 2));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE1",
        }));
    }

    [Fact]
    public async Task StaffLeader_CannotEditLockedTarget()
    {
        var h = CreateHarness();
        var locked = Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId);
        locked.Status = UserStatuses.Locked;
        h.Db.Users.Add(locked);
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
        }));
    }

    // ── Identity fields (Họ tên / Email) — spec §4.2 / §4.15 ────────────────────

    [Fact]
    public async Task StaffStaff_EditsFullNameAndEmail()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            FullName = "  Nguyễn Văn An  ",
            Email = "  An.Nguyen@FPT.edu.vn  ",
        });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal("Nguyễn Văn An", user.FullName);
        Assert.Equal("an.nguyen@fpt.edu.vn", user.Email);
    }

    [Fact]
    public async Task DepartmentLeader_EditsEmail()
    {
        var h = CreateHarness();
        var dept = Uc106TestData.CreateGeneralDepartment(GeneralDeptId);
        dept.HeadUserId = TargetId;
        h.Db.Departments.Add(dept);
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.DepartmentRoleId, UserSubRoles.Leader, GeneralDeptId));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Department,
            DepartmentId = GeneralDeptId,
            Email = "newleader@fpt.edu.vn",
        });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal("newleader@fpt.edu.vn", user.Email);
    }

    [Fact]
    public async Task Student_EditsFullNameAndEmail()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Student(TargetId, "HE160010"));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160010",
            FullName = "Trần Văn C",
            Email = "tranvanc@fpt.edu.vn",
        });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal("Trần Văn C", user.FullName);
        Assert.Equal("tranvanc@fpt.edu.vn", user.Email);
    }

    [Fact]
    public async Task DepartmentStaff_IdentityChange_Rejected()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateDepartmentStaff(TargetId, GeneralDeptId));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160011",
            FullName = "Tên Mới Không Được Phép",
        }));
    }

    [Fact]
    public async Task StaffLeaderTarget_IdentityChange_Rejected()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Leader, IcDeptId));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            Email = "hacked@fpt.edu.vn",
        }));
    }

    [Fact]
    public async Task Identity_Eligibility_UsesOriginalRole_NotNewRoleCode()
    {
        // A DEPARTMENT/STAFF target promoted to STUDENT still may NOT have its identity edited,
        // even though the *new* role (STUDENT) would be eligible.
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateDepartmentStaff(TargetId, GeneralDeptId));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160012",
            Email = "shouldnotchange@fpt.edu.vn",
        }));
    }

    [Fact]
    public async Task Email_DuplicateOnAnotherUser_Rejected()
    {
        var h = CreateHarness();
        var other = Uc106TestData.CreateUser(200, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId);
        other.Email = "taken@fpt.edu.vn";
        h.Db.Users.Add(other);
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            Email = "taken@fpt.edu.vn",
        }));
    }

    [Fact]
    public async Task Email_SameAsTargetsCurrent_IsNotDuplicate()
    {
        var h = CreateHarness();
        var target = Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId);
        target.Email = "keep@fpt.edu.vn";
        h.Db.Users.Add(target);
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            Email = "KEEP@fpt.edu.vn",   // same address, different casing → normalized, no conflict
            FullName = "Đổi Tên Thôi",
        });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal("keep@fpt.edu.vn", user.Email);
        Assert.Equal("Đổi Tên Thôi", user.FullName);
    }

    [Fact]
    public async Task LegacyRequest_WithoutIdentity_KeepsFullNameAndEmail()
    {
        var h = CreateHarness();
        var target = Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId);
        h.Db.Users.Add(target);
        h.Db.SaveChanges();
        var originalName = target.FullName;
        var originalEmail = target.Email;

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160013",
        });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal(originalName, user.FullName);
        Assert.Equal(originalEmail, user.Email);
    }

    [Fact]
    public async Task Audit_ContainsIdentityOldAndNew()
    {
        var h = CreateHarness();
        var target = Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId);
        target.FullName = "OldName";
        target.Email = "old@fpt.edu.vn";
        h.Db.Users.Add(target);
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            FullName = "NewName",
            Email = "new@fpt.edu.vn",
        });

        var audit = await h.Db.AuditLogs.Include(a => a.Changes)
            .SingleAsync(a => a.Action == "UPDATE_ACCOUNT_ROLE" && a.EntityId == TargetId);
        var change = Assert.Single(audit.Changes);
        Assert.Contains("OldName", change.OldValueText);
        Assert.Contains("old@fpt.edu.vn", change.OldValueText);
        Assert.Contains("NewName", change.NewValueText);
        Assert.Contains("new@fpt.edu.vn", change.NewValueText);
    }

    // ── Audit + sessions ──────────────────────────────────────────────────────

    [Fact]
    public async Task Success_WritesAuditWithStudentCode_AndRevokesSessions()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Student(TargetId, "OLDCODE"));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "NEWCODE",
        });

        var audit = await h.Db.AuditLogs.Include(a => a.Changes)
            .SingleAsync(a => a.Action == "UPDATE_ACCOUNT_ROLE" && a.EntityId == TargetId);
        var change = Assert.Single(audit.Changes);
        Assert.Contains("OLDCODE", change.OldValueText);
        Assert.Contains("NEWCODE", change.NewValueText);

        var revoke = Assert.Single(h.Sessions.RevokeAllCalls);
        Assert.Equal(TargetId, revoke.UserId);
    }
}
