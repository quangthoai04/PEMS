using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.UpdateAccountRole;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
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
        public FakeSystemEmailDispatcher Dispatcher { get; } = new();
        public RecordingUserMutationLockService Locks { get; } = new();

        /// <summary>
        /// Issues real confirmation rows, so a pending account's email edit can be asserted the way it
        /// actually behaves — the old token superseded, exactly one live token, bound to the new address.
        /// </summary>
        public RecordingConfirmationService Confirmations { get; }
        public UpdateAccountRoleCommandHandler Handler { get; }

        public Harness()
        {
            Sessions = new RecordingSessionService(Db);
            Confirmations = new RecordingConfirmationService(Db, Clock);
            Handler = new UpdateAccountRoleCommandHandler(
                Db, Actor, Sessions, Clock, Dispatcher, Locks,
                new PendingAccountEmailChangeService(Db, Confirmations), Confirmations);
        }

        /// <summary>Asserts the request neither sent an email nor revoked a session (spec §21).</summary>
        public void AssertNoSideEffects()
        {
            Assert.Empty(Sessions.RevokeAllCalls);
            // Asserted against the dispatcher rather than IEmailService: the handler no longer composes
            // its own message, so "no email" now means "no template was dispatched".
            Assert.Empty(Dispatcher.Sent);
            Assert.Empty(Db.AuditLogs);
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
    public async Task LeavingDepartment_IsBlocked_WhenTargetIsStillHead()
    {
        // The old behaviour silently cleared head_user_id, leaving the department leaderless the
        // moment a Staff Leader re-roled its head. The department must be handed over through
        // /departments/reassign-lead first (spec §8.6/§12.4).
        var h = CreateHarness();
        var dept = Uc106TestData.CreateGeneralDepartment(GeneralDeptId);
        dept.HeadUserId = TargetId;
        h.Db.Departments.Add(dept);
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.DepartmentRoleId, UserSubRoles.Leader, GeneralDeptId));
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160005",
        }));

        Assert.Equal(AccountErrorCodes.AccountRoleChangeBlockedByActiveResponsibilities, ex.ErrorCode);

        var oldDept = await h.Db.Departments.SingleAsync(d => d.DepartmentId == GeneralDeptId);
        Assert.Equal(TargetId, oldDept.HeadUserId);
        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal(Uc106TestData.DepartmentRoleId, user.RoleId);
        h.AssertNoSideEffects();
    }

    [Fact]
    public async Task LeavingDepartment_Succeeds_AfterHeadWasReassigned()
    {
        var h = CreateHarness();
        var dept = Uc106TestData.CreateGeneralDepartment(GeneralDeptId);
        dept.HeadUserId = 777;                                   // handover already happened
        h.Db.Departments.Add(dept);
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.DepartmentRoleId, UserSubRoles.Leader, GeneralDeptId));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160005",
        });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal(Uc106TestData.StudentRoleId, user.RoleId);
        // The new head is untouched: this flow never rewrites head_user_id on the way out.
        var oldDept = await h.Db.Departments.SingleAsync(d => d.DepartmentId == GeneralDeptId);
        Assert.Equal(777UL, oldDept.HeadUserId);
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

    // A DEPARTMENT/STAFF or STAFF/LEADER target is now out of a Staff Leader's managed set entirely
    // (spec §3.3), so the request is refused by the scope gate before identity is even considered.
    // The outcome these three tests were written to protect is unchanged — 403, nothing written —
    // but the reason is now the precise one, carried by a stable error code.

    [Fact]
    public async Task DepartmentStaff_IdentityChange_Rejected()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateDepartmentStaff(TargetId, GeneralDeptId));
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160011",
            FullName = "Tên Mới Không Được Phép",
        }));
        Assert.Equal(AccountErrorCodes.AccountRoleTargetNotManageable, ex.ErrorCode);

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal($"User {TargetId}", user.FullName);
    }

    [Fact]
    public async Task StaffLeaderTarget_IdentityChange_Rejected()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Leader, IcDeptId));
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            Email = "hacked@fpt.edu.vn",
        }));
        Assert.Equal(AccountErrorCodes.AccountRoleTargetNotManageable, ex.ErrorCode);

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal($"user{TargetId}@test.local", user.Email);
    }

    [Fact]
    public async Task Identity_Eligibility_UsesOriginalRole_NotNewRoleCode()
    {
        // A DEPARTMENT/STAFF target asked to become a STUDENT: the *new* role would be
        // identity-editable, but eligibility is derived from the ORIGINAL role — and that original
        // role is not manageable here at all, so nothing about the account may change.
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateDepartmentStaff(TargetId, GeneralDeptId));
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160012",
            Email = "shouldnotchange@fpt.edu.vn",
        }));
        Assert.Equal(AccountErrorCodes.AccountRoleTargetNotManageable, ex.ErrorCode);

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal($"user{TargetId}@test.local", user.Email);
        Assert.Equal(Uc106TestData.DepartmentRoleId, user.RoleId);
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

    // ── Active-responsibility blockers (spec §8 / §19 / §21) ───────────────────
    //
    // Every test here asserts BOTH halves of the contract: the request is refused with the blocker
    // error code, AND the database is exactly as it was — no role change, no audit, no session
    // revoke, no email. A blocked role change that still mutated something would be the worst
    // outcome of all, because the account would lose permissions it is still using.

    private const ulong VisitId = 5001;

    private static Harness HarnessWithStaffTarget(out ulong targetId)
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId));
        h.Db.SaveChanges();
        targetId = TargetId;
        return h;
    }

    private static async Task<ConflictException> AssertBlockedAsync(Harness h, UpdateAccountRoleCommand cmd)
    {
        var before = await h.Db.Users.AsNoTracking().SingleAsync(u => u.UserId == cmd.UserId);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Run(cmd));
        Assert.Equal(AccountErrorCodes.AccountRoleChangeBlockedByActiveResponsibilities, ex.ErrorCode);

        var after = await h.Db.Users.AsNoTracking().SingleAsync(u => u.UserId == cmd.UserId);
        Assert.Equal(before.RoleId, after.RoleId);
        Assert.Equal(before.SubRole, after.SubRole);
        Assert.Equal(before.DepartmentId, after.DepartmentId);
        Assert.Equal(before.PrimaryCampusId, after.PrimaryCampusId);
        Assert.Equal(before.StudentCode, after.StudentCode);
        Assert.Equal(before.FullName, after.FullName);
        Assert.Equal(before.Email, after.Email);
        h.AssertNoSideEffects();
        return ex;
    }

    private static UpdateAccountRoleCommand ToStudent(ulong userId = TargetId) => new()
    {
        UserId = userId,
        NewRoleCode = RoleCodes.Student,
        StudentCode = "HE900001",
    };

    [Theory]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval)]
    [InlineData(VisitInstanceStatuses.Assigned)]
    [InlineData(VisitInstanceStatuses.BeforeVisit)]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.AfterVisit)]
    public async Task ActiveHost_BlocksRoleChange(string visitStatus)
    {
        var h = HarnessWithStaffTarget(out var target);
        var instance = Uc106TestData.CreateVisitInstance(VisitId, visitStatus);
        instance.CurrentHostUserId = target;
        h.Db.VisitRequestCampuses.Add(instance);
        h.Db.SaveChanges();

        var ex = await AssertBlockedAsync(h, ToStudent());
        Assert.Contains("Host", ex.Message);
    }

    [Theory]
    [InlineData(VisitInstanceStatuses.Closed)]
    [InlineData(VisitInstanceStatuses.Cancelled)]
    [InlineData(VisitInstanceStatuses.Rejected)]
    public async Task TerminalVisit_DoesNotBlockRoleChange(string visitStatus)
    {
        var h = HarnessWithStaffTarget(out var target);
        var instance = Uc106TestData.CreateVisitInstance(VisitId, visitStatus);
        instance.CurrentHostUserId = target;
        instance.CoordinatorUserId = target;
        h.Db.VisitRequestCampuses.Add(instance);
        h.Db.VisitParticipants.Add(Uc106TestData.CreateParticipant(
            1, VisitId, target, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted));
        h.Db.SaveChanges();

        await h.Run(ToStudent());

        var user = await h.Db.Users.SingleAsync(u => u.UserId == target);
        Assert.Equal(Uc106TestData.StudentRoleId, user.RoleId);
    }

    [Fact]
    public async Task ActiveCoordinator_BlocksRoleChange()
    {
        var h = HarnessWithStaffTarget(out var target);
        var instance = Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.BeforeVisit);
        instance.CoordinatorUserId = target;
        h.Db.VisitRequestCampuses.Add(instance);
        h.Db.SaveChanges();

        await AssertBlockedAsync(h, ToStudent());
    }

    [Fact]
    public async Task ActiveHost_BlocksPromotionToDepartmentLeader_Too()
    {
        var h = HarnessWithStaffTarget(out var target);
        h.Db.Departments.Add(Uc106TestData.CreateGeneralDepartment(GeneralDeptId));
        var instance = Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned);
        instance.CurrentHostUserId = target;
        h.Db.VisitRequestCampuses.Add(instance);
        h.Db.SaveChanges();

        await AssertBlockedAsync(h, new UpdateAccountRoleCommand
        {
            UserId = target,
            NewRoleCode = RoleCodes.Department,
            DepartmentId = GeneralDeptId,
        });

        // The destination department must NOT have been claimed by the refused change.
        var dept = await h.Db.Departments.SingleAsync(d => d.DepartmentId == GeneralDeptId);
        Assert.Null(dept.HeadUserId);
    }

    [Theory]
    [InlineData(ParticipantStatuses.Invited, AccountRoleChangeDependencyRule.PendingParticipantInvitationsBlockerType)]
    [InlineData(ParticipantStatuses.Accepted, AccountRoleChangeDependencyRule.ActiveVisitParticipationsBlockerType)]
    [InlineData(ParticipantStatuses.Assigned, AccountRoleChangeDependencyRule.ActiveVisitParticipationsBlockerType)]
    public async Task LiveParticipantRow_BlocksRoleChange(string participantStatus, string expectedType)
    {
        var h = HarnessWithStaffTarget(out var target);
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned));
        h.Db.VisitParticipants.Add(Uc106TestData.CreateParticipant(
            1, VisitId, target, ParticipantRoles.IcSupport, participantStatus));
        h.Db.SaveChanges();

        var ex = await AssertBlockedAsync(h, ToStudent());
        Assert.Contains(expectedType, BlockerTypesOf(ex));
    }

    [Theory]
    [InlineData(ParticipantStatuses.Declined)]
    [InlineData(ParticipantStatuses.Removed)]
    public async Task ResolvedParticipantRow_DoesNotBlockRoleChange(string participantStatus)
    {
        var h = HarnessWithStaffTarget(out var target);
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned));
        h.Db.VisitParticipants.Add(Uc106TestData.CreateParticipant(
            1, VisitId, target, ParticipantRoles.IcSupport, participantStatus));
        h.Db.SaveChanges();

        await h.Run(ToStudent());

        var user = await h.Db.Users.SingleAsync(u => u.UserId == target);
        Assert.Equal(Uc106TestData.StudentRoleId, user.RoleId);
    }

    // ── Department Leader who has already handed the visit to one of their staff ──
    // AssignDepartmentStaff leaves the leader's own DEPT_SUPPORT row at ASSIGNED (the participant
    // enum has no DELEGATED value), so the row cannot say on its own whether the leader handed the
    // visit over or is attending it personally. The staff row does.

    private const ulong SubstituteStaffId = 777;

    private static Harness HarnessWithDelegatingLeader(out ulong targetId, string substituteStatus)
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(
            TargetId, Uc106TestData.DepartmentRoleId, UserSubRoles.Leader, GeneralDeptId));
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned));

        var leaderRow = Uc106TestData.CreateParticipant(
            1, VisitId, TargetId, ParticipantRoles.DeptSupport, ParticipantStatuses.Assigned);
        var staffRow = Uc106TestData.CreateParticipant(
            2, VisitId, SubstituteStaffId, ParticipantRoles.DeptSupport, substituteStatus);
        staffRow.AssignedBy = TargetId;                     // the hand-down this leader performed
        h.Db.VisitParticipants.AddRange(leaderRow, staffRow);
        h.Db.SaveChanges();

        targetId = TargetId;
        return h;
    }

    [Theory]
    [InlineData(ParticipantStatuses.Assigned)]              // staff has not answered yet
    [InlineData(ParticipantStatuses.Accepted)]              // staff has taken it on
    public async Task DelegatedVisit_DoesNotBlockTheLeadersRoleChange(string substituteStatus)
    {
        var h = HarnessWithDelegatingLeader(out var target, substituteStatus);

        await h.Run(ToStudent());

        var user = await h.Db.Users.SingleAsync(u => u.UserId == target);
        Assert.Equal(Uc106TestData.StudentRoleId, user.RoleId);
    }

    [Theory]
    [InlineData(ParticipantStatuses.Declined)]              // staff refused — duty bounces back
    [InlineData(ParticipantStatuses.Removed)]
    public async Task DelegationThatFellThrough_BlocksTheLeaderAgain(string substituteStatus)
    {
        var h = HarnessWithDelegatingLeader(out _, substituteStatus);

        var ex = await AssertBlockedAsync(h, ToStudent());
        Assert.Contains(
            AccountRoleChangeDependencyRule.ActiveVisitParticipationsBlockerType, BlockerTypesOf(ex));
    }

    [Fact]
    public async Task DelegationByAnotherLeader_DoesNotExcuseThisOne()
    {
        // The staff row must have been assigned BY the target. A visit somebody else delegated is
        // still the target's own responsibility.
        var h = HarnessWithDelegatingLeader(out _, ParticipantStatuses.Accepted);
        var staffRow = h.Db.VisitParticipants.Single(p => p.UserId == SubstituteStaffId);
        staffRow.AssignedBy = 888;
        h.Db.SaveChanges();

        var ex = await AssertBlockedAsync(h, ToStudent());
        Assert.Contains(
            AccountRoleChangeDependencyRule.ActiveVisitParticipationsBlockerType, BlockerTypesOf(ex));
    }

    [Fact]
    public async Task CanonicalHostParticipantRow_IsNotCountedTwice()
    {
        // A Host normally exists as BOTH current_host_user_id and an IC_HOST participant row.
        // That is ONE responsibility and must produce ONE blocker (spec §8.4).
        var h = HarnessWithStaffTarget(out var target);
        var instance = Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned);
        instance.CurrentHostUserId = target;
        h.Db.VisitRequestCampuses.Add(instance);
        var hostRow = Uc106TestData.CreateParticipant(
            1, VisitId, target, ParticipantRoles.IcHost, ParticipantStatuses.Assigned);
        hostRow.IsHost = true;
        h.Db.VisitParticipants.Add(hostRow);
        h.Db.SaveChanges();

        var ex = await AssertBlockedAsync(h, ToStudent());

        var types = BlockerTypesOf(ex);
        Assert.Equal(new[] { AccountRoleChangeDependencyRule.ActiveHostAssignmentsBlockerType }, types);
    }

    [Fact]
    public async Task OrphanIcHostParticipantRow_StillBlocks()
    {
        // IC_HOST row whose instance no longer points at the target: a data anomaly. Fail closed —
        // do not treat it as the canonical host row and silently drop it (spec §8.4).
        var h = HarnessWithStaffTarget(out var target);
        var instance = Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned);
        instance.CurrentHostUserId = 888;                          // someone else is the host now
        h.Db.VisitRequestCampuses.Add(instance);
        h.Db.VisitParticipants.Add(Uc106TestData.CreateParticipant(
            1, VisitId, target, ParticipantRoles.IcHost, ParticipantStatuses.Assigned));
        h.Db.SaveChanges();

        var ex = await AssertBlockedAsync(h, ToStudent());
        Assert.Contains(
            AccountRoleChangeDependencyRule.ActiveVisitParticipationsBlockerType, BlockerTypesOf(ex));
    }

    [Theory]
    [InlineData("REQUESTED")]
    [InlineData("CHANGE_PROPOSED")]
    [InlineData("ASSIGNED")]
    [InlineData("ACCEPTED")]
    [InlineData("IN_PROGRESS")]
    public async Task ActiveLogisticsAssignee_BlocksRoleChange(string logisticsStatus)
    {
        var h = HarnessWithStaffTarget(out var target);
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned));
        var item = Uc106TestData.CreateOpenLogisticsItem(1, VisitId, GeneralDeptId, logisticsStatus);
        item.AssignedToUserId = target;
        h.Db.VisitLogisticsItems.Add(item);
        h.Db.SaveChanges();

        await AssertBlockedAsync(h, ToStudent());
    }

    [Theory]
    [InlineData("DONE")]
    [InlineData("REJECTED")]
    [InlineData("DECLINED")]
    [InlineData("CANCELLED")]
    public async Task TerminalLogistics_DoesNotBlockRoleChange(string logisticsStatus)
    {
        var h = HarnessWithStaffTarget(out var target);
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned));
        var item = Uc106TestData.CreateOpenLogisticsItem(1, VisitId, GeneralDeptId, logisticsStatus);
        item.AssignedToUserId = target;
        h.Db.VisitLogisticsItems.Add(item);
        h.Db.SaveChanges();

        await h.Run(ToStudent());

        var user = await h.Db.Users.SingleAsync(u => u.UserId == target);
        Assert.Equal(Uc106TestData.StudentRoleId, user.RoleId);
    }

    [Fact]
    public async Task LogisticsReceivedByWithNoAssignee_BlocksRoleChange()
    {
        var h = HarnessWithStaffTarget(out var target);
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned));
        var item = Uc106TestData.CreateOpenLogisticsItem(1, VisitId, GeneralDeptId, "REQUESTED");
        item.ReceivedBy = target;
        item.AssignedToUserId = null;
        h.Db.VisitLogisticsItems.Add(item);
        h.Db.SaveChanges();

        await AssertBlockedAsync(h, ToStudent());
    }

    [Fact]
    public async Task LogisticsReceivedByButAssignedToSomeoneElse_DoesNotBlock()
    {
        // received_by is then handling HISTORY, not a duty the account still owes (spec §8.5).
        var h = HarnessWithStaffTarget(out var target);
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned));
        var item = Uc106TestData.CreateOpenLogisticsItem(1, VisitId, GeneralDeptId, "IN_PROGRESS");
        item.ReceivedBy = target;
        item.AssignedToUserId = 888;
        h.Db.VisitLogisticsItems.Add(item);
        h.Db.SaveChanges();

        await h.Run(ToStudent());

        var user = await h.Db.Users.SingleAsync(u => u.UserId == target);
        Assert.Equal(Uc106TestData.StudentRoleId, user.RoleId);
    }

    [Fact]
    public async Task LogisticsWhereTargetIsBothReceiverAndAssignee_CountsOnce()
    {
        var h = HarnessWithStaffTarget(out var target);
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned));
        var item = Uc106TestData.CreateOpenLogisticsItem(1, VisitId, GeneralDeptId, "REQUESTED");
        item.ReceivedBy = target;
        item.AssignedToUserId = target;
        h.Db.VisitLogisticsItems.Add(item);
        h.Db.SaveChanges();

        var ex = await AssertBlockedAsync(h, ToStudent());
        var blocker = Assert.Single(BlockersOf(ex));
        Assert.Equal(AccountRoleChangeDependencyRule.ActiveLogisticsResponsibilitiesBlockerType, blocker.Type);
        Assert.Equal(1, blocker.Count);
    }

    [Fact]
    public async Task LogisticsRequestedByAlone_DoesNotBlock()
    {
        var h = HarnessWithStaffTarget(out var target);
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned));
        var item = Uc106TestData.CreateOpenLogisticsItem(1, VisitId, GeneralDeptId, "REQUESTED");
        item.RequestedBy = target;
        item.AssignedBy = target;
        h.Db.VisitLogisticsItems.Add(item);
        h.Db.SaveChanges();

        await h.Run(ToStudent());

        var user = await h.Db.Users.SingleAsync(u => u.UserId == target);
        Assert.Equal(Uc106TestData.StudentRoleId, user.RoleId);
    }

    [Fact]
    public async Task MultipleBlockers_AreAllReportedTogether()
    {
        // The user must see everything left to resolve in ONE response, not discover the
        // responsibilities one failed attempt at a time (spec §18.6).
        var h = HarnessWithStaffTarget(out var target);
        var hosted = Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.Assigned);
        hosted.CurrentHostUserId = target;
        var other = Uc106TestData.CreateVisitInstance(VisitId + 1, VisitInstanceStatuses.BeforeVisit);
        h.Db.VisitRequestCampuses.AddRange(hosted, other);
        h.Db.VisitParticipants.Add(Uc106TestData.CreateParticipant(
            1, VisitId + 1, target, ParticipantRoles.IcSupport, ParticipantStatuses.Invited));
        var item = Uc106TestData.CreateOpenLogisticsItem(1, VisitId + 1, GeneralDeptId, "ASSIGNED");
        item.AssignedToUserId = target;
        h.Db.VisitLogisticsItems.Add(item);
        h.Db.SaveChanges();

        var ex = await AssertBlockedAsync(h, ToStudent());

        var types = BlockerTypesOf(ex);
        Assert.Contains(AccountRoleChangeDependencyRule.ActiveHostAssignmentsBlockerType, types);
        Assert.Contains(AccountRoleChangeDependencyRule.PendingParticipantInvitationsBlockerType, types);
        Assert.Contains(AccountRoleChangeDependencyRule.ActiveLogisticsResponsibilitiesBlockerType, types);

        // Two distinct visits are affected — not three, even though three blockers reference them.
        Assert.Equal(2, AffectedVisitCountOf(ex));
    }

    [Fact]
    public async Task StudentWithPendingInvitation_CannotBecomeStaff()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Student(TargetId, "HE160099"));
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.BeforeVisit));
        h.Db.VisitParticipants.Add(Uc106TestData.CreateParticipant(
            1, VisitId, TargetId, ParticipantRoles.Student, ParticipantStatuses.Invited));
        h.Db.SaveChanges();

        await AssertBlockedAsync(h, new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
        });
    }

    [Fact]
    public async Task DepartmentLeaderWithHeadReassigned_ButStillLogistics_IsStillBlocked()
    {
        // Replacing the head clears ONLY the department blocker; the rest are re-checked (spec §15).
        var h = CreateHarness();
        var dept = Uc106TestData.CreateGeneralDepartment(GeneralDeptId);
        dept.HeadUserId = 777;
        h.Db.Departments.Add(dept);
        h.Db.Users.Add(Uc106TestData.CreateUser(
            TargetId, Uc106TestData.DepartmentRoleId, UserSubRoles.Leader, GeneralDeptId));
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.DuringVisit));
        var item = Uc106TestData.CreateOpenLogisticsItem(1, VisitId, GeneralDeptId, "IN_PROGRESS");
        item.AssignedToUserId = TargetId;
        h.Db.VisitLogisticsItems.Add(item);
        h.Db.SaveChanges();

        var ex = await AssertBlockedAsync(h, ToStudent());

        var types = BlockerTypesOf(ex);
        Assert.Contains(AccountRoleChangeDependencyRule.ActiveLogisticsResponsibilitiesBlockerType, types);
        Assert.DoesNotContain(AccountRoleChangeDependencyRule.DepartmentHeadAssignmentBlockerType, types);
    }

    // ── Department-head handover inside the role change (spec §8.6/§15) ───────
    // Handing the seat over through /departments/reassign-lead first cannot work: that command
    // demotes the outgoing head to DEPARTMENT/STAFF, which a Staff Leader may not manage (§3.3), so
    // the role change it was meant to unblock becomes unreachable. The successor therefore travels
    // with the role change and both commit together.

    private const ulong SuccessorId = 555;

    private static Harness HarnessWithHeadedDepartment(out ulong targetId)
    {
        var h = CreateHarness();
        var dept = Uc106TestData.CreateGeneralDepartment(GeneralDeptId);
        dept.HeadUserId = TargetId;
        h.Db.Departments.Add(dept);
        h.Db.Users.Add(Uc106TestData.CreateUser(
            TargetId, Uc106TestData.DepartmentRoleId, UserSubRoles.Leader, GeneralDeptId));
        h.Db.Users.Add(Uc106TestData.CreateDepartmentStaff(SuccessorId, GeneralDeptId));
        h.Db.SaveChanges();
        targetId = TargetId;
        return h;
    }

    private static UpdateAccountRoleCommand ToStudentWithSuccessor(ulong? successorId = SuccessorId)
    {
        var cmd = ToStudent();
        cmd.ReplacementDepartmentHeadUserId = successorId;
        return cmd;
    }

    [Fact]
    public async Task DepartmentHeadHandover_MovesTheSeatAndChangesTheRoleTogether()
    {
        var h = HarnessWithHeadedDepartment(out var target);

        await h.Run(ToStudentWithSuccessor());

        var dept = await h.Db.Departments.SingleAsync(d => d.DepartmentId == GeneralDeptId);
        Assert.Equal(SuccessorId, dept.HeadUserId);                       // never headless, never two heads

        var successor = await h.Db.Users.SingleAsync(u => u.UserId == SuccessorId);
        Assert.Equal(UserSubRoles.Leader, successor.SubRole);

        var user = await h.Db.Users.SingleAsync(u => u.UserId == target);
        Assert.Equal(Uc106TestData.StudentRoleId, user.RoleId);           // and the target moved on
    }

    [Fact]
    public async Task DepartmentHead_WithoutASuccessor_IsStillBlocked()
    {
        var h = HarnessWithHeadedDepartment(out _);

        var ex = await AssertBlockedAsync(h, ToStudent());

        Assert.Contains(AccountRoleChangeDependencyRule.DepartmentHeadAssignmentBlockerType, BlockerTypesOf(ex));
        var dept = await h.Db.Departments.SingleAsync(d => d.DepartmentId == GeneralDeptId);
        Assert.Equal(TargetId, dept.HeadUserId);
    }

    [Fact]
    public async Task DepartmentHeadHandover_KeepingTheSameDepartment_IsRejected()
    {
        // Staying DEPARTMENT/LEADER of the same department vacates nothing.
        var h = HarnessWithHeadedDepartment(out var target);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = target,
            NewRoleCode = RoleCodes.Department,
            DepartmentId = GeneralDeptId,
            FullName = "Tên Mới",                                          // an identity edit alongside
            ReplacementDepartmentHeadUserId = SuccessorId,
        }));

        Assert.Equal(AccountErrorCodes.InvalidDepartmentHeadReplacement, ex.ErrorCode);
    }

    [Fact]
    public async Task SuccessorSentForAnAccountThatHeadsNothing_IsRejected()
    {
        var h = HarnessWithStaffTarget(out var target);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Run(ToStudentWithSuccessor()));

        Assert.Equal(AccountErrorCodes.InvalidDepartmentHeadReplacement, ex.ErrorCode);
        var user = await h.Db.Users.SingleAsync(u => u.UserId == target);
        Assert.Equal(Uc106TestData.StaffRoleId, user.RoleId);
    }

    [Fact]
    public async Task SuccessorFromAnotherDepartment_IsRejected()
    {
        var h = HarnessWithHeadedDepartment(out _);
        var outsider = Uc106TestData.CreateDepartmentStaff(666, GeneralDeptId + 1);
        h.Db.Users.Add(outsider);
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Run(ToStudentWithSuccessor(666)));

        Assert.Equal(AccountErrorCodes.InvalidDepartmentHeadReplacement, ex.ErrorCode);
    }

    [Fact]
    public async Task InactiveSuccessor_IsRejected()
    {
        var h = HarnessWithHeadedDepartment(out _);
        var successor = h.Db.Users.Single(u => u.UserId == SuccessorId);
        successor.Status = UserStatuses.Inactive;
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Run(ToStudentWithSuccessor()));

        Assert.Equal(AccountErrorCodes.InvalidDepartmentHeadReplacement, ex.ErrorCode);
    }

    [Fact]
    public async Task SuccessorWhoIsTheTargetThemself_IsRejected()
    {
        var h = HarnessWithHeadedDepartment(out var target);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Run(ToStudentWithSuccessor(target)));

        Assert.Equal(AccountErrorCodes.InvalidDepartmentHeadReplacement, ex.ErrorCode);
    }

    [Fact]
    public async Task HandoverDoesNotExcuseTheOtherBlockers()
    {
        // Naming a successor clears exactly one blocker. A target who is still hosting a visit is
        // refused, and on a real database the transaction takes the handover down with it — proven
        // in AccountRoleChangeConcurrencyTests, since EF InMemory has no transactions to roll back.
        var h = HarnessWithHeadedDepartment(out var target);
        var instance = Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.DuringVisit);
        instance.CurrentHostUserId = target;
        h.Db.VisitRequestCampuses.Add(instance);
        h.Db.SaveChanges();

        var ex = await AssertBlockedAsync(h, ToStudentWithSuccessor());

        var types = BlockerTypesOf(ex);
        Assert.Contains(AccountRoleChangeDependencyRule.ActiveHostAssignmentsBlockerType, types);
        Assert.DoesNotContain(AccountRoleChangeDependencyRule.DepartmentHeadAssignmentBlockerType, types);
    }

    // ── Identity-only / MSSV-only / no-op are never blocked (spec §6 / §22) ────

    [Fact]
    public async Task IdentityOnlyChange_IsNotBlockedByActiveResponsibilities()
    {
        var h = HarnessWithStaffTarget(out var target);
        var instance = Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.DuringVisit);
        instance.CurrentHostUserId = target;                 // busiest possible account
        h.Db.VisitRequestCampuses.Add(instance);
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = target,
            NewRoleCode = RoleCodes.Staff,                   // same shape
            FullName = "Tên Đã Sửa",
        });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == target);
        Assert.Equal("Tên Đã Sửa", user.FullName);
        Assert.Equal(Uc106TestData.StaffRoleId, user.RoleId);
    }

    [Fact]
    public async Task StudentCodeOnlyChange_IsNotBlockedByActiveResponsibilities()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Student(TargetId, "OLD001"));
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(VisitId, VisitInstanceStatuses.BeforeVisit));
        h.Db.VisitParticipants.Add(Uc106TestData.CreateParticipant(
            1, VisitId, TargetId, ParticipantRoles.Student, ParticipantStatuses.Accepted));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "NEW001",
        });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal("NEW001", user.StudentCode);
    }

    [Fact]
    public async Task DepartmentLeaderIdentityOnly_DoesNotTouchDepartmentRow()
    {
        var h = CreateHarness();
        var dept = Uc106TestData.CreateGeneralDepartment(GeneralDeptId);
        dept.HeadUserId = TargetId;
        h.Db.Departments.Add(dept);
        h.Db.Users.Add(Uc106TestData.CreateUser(
            TargetId, Uc106TestData.DepartmentRoleId, UserSubRoles.Leader, GeneralDeptId));
        h.Db.SaveChanges();
        var originalUpdatedAt = dept.UpdatedAt;
        var originalUpdatedBy = dept.UpdatedBy;

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Department,
            DepartmentId = GeneralDeptId,
            Email = "leader.moi@fpt.edu.vn",
        });

        var after = await h.Db.Departments.SingleAsync(d => d.DepartmentId == GeneralDeptId);
        Assert.Equal(originalUpdatedAt, after.UpdatedAt);
        Assert.Equal(originalUpdatedBy, after.UpdatedBy);
    }

    [Fact]
    public async Task PureNoOp_HasNoSideEffects()
    {
        var h = CreateHarness();
        var target = Student(TargetId, "HE160777");
        h.Db.Users.Add(target);
        h.Db.SaveChanges();
        var originalUpdatedAt = target.UpdatedAt;

        var response = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160777",
            FullName = target.FullName,
            Email = target.Email,
        });

        Assert.Equal(RoleCodes.Student, response.RoleCode);
        Assert.Equal(0, response.RevokedSessions);
        var after = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal(originalUpdatedAt, after.UpdatedAt);
        h.AssertNoSideEffects();
    }

    // ── Managed-target scope (spec §3 / §20) ──────────────────────────────────

    [Fact]
    public async Task StaffLeader_CannotRoleChangeAVisitor()
    {
        // Visitor → internal needs its own flow (operational-contact ownership per campus, DB triggers).
        // Refused in the backend, not merely hidden in the UI (spec §3.4).
        var h = CreateHarness();
        h.Db.Roles.Add(Uc106TestData.CreateRole(9, RoleCodes.Visitor));
        var visitor = Uc106TestData.CreateUser(TargetId, 9, null, null);
        visitor.PrimaryCampusId = null;
        h.Db.Users.Add(visitor);
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => h.Run(ToStudent()));
        Assert.Equal(AccountErrorCodes.AccountRoleTargetNotManageable, ex.ErrorCode);
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task StaffLeader_CannotRoleChangeADepartmentStaff()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateDepartmentStaff(TargetId, GeneralDeptId));
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => h.Run(ToStudent()));
        Assert.Equal(AccountErrorCodes.AccountRoleTargetNotManageable, ex.ErrorCode);
    }

    [Fact]
    public async Task StaffLeader_CannotRoleChangeAnotherStaffLeader()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Uc106TestData.CreateUser(
            TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Leader, IcDeptId));
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => h.Run(ToStudent()));
        Assert.Equal(AccountErrorCodes.AccountRoleTargetNotManageable, ex.ErrorCode);
    }

    [Fact]
    public async Task StaffLeader_CannotPromoteTargetToVisitor()
    {
        var h = HarnessWithStaffTarget(out var target);

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = target,
            NewRoleCode = RoleCodes.Visitor,
        }));
        Assert.Equal(AccountErrorCodes.AccountRoleTargetNotManageable, ex.ErrorCode);
    }

    // ── Locking protocol (spec §13) ───────────────────────────────────────────

    [Fact]
    public async Task RoleChange_LocksTargetUser_ThenTheDepartmentsInvolved()
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

        Assert.Equal(new[] { TargetId }, Assert.Single(h.Locks.LockedUserBatches));
        // Both the department being left and the one being joined, so neither can be
        // concurrently re-headed while this change is in flight.
        var lockedDepartments = Assert.Single(h.Locks.LockedDepartmentBatches);
        Assert.Contains(IcDeptId, lockedDepartments);
        Assert.Contains(GeneralDeptId, lockedDepartments);
    }

    [Fact]
    public async Task PureNoOp_DoesNotLockDepartments()
    {
        var h = CreateHarness();
        h.Db.Users.Add(Student(TargetId, "HE160888"));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160888",
        });

        Assert.Empty(h.Locks.LockedDepartmentBatches);
    }

    // ── Email change: pending accounts (STAFF_LEADER_PENDING_EMAIL spec §11/§13/§15/§16/§17) ──
    //
    // The property under test throughout: a PENDING_EMAIL_CONFIRMATION account has never proven it
    // owns an address, so moving that address must re-issue the ACTIVATION link — not mail a "your
    // login email changed" notice, which carries no token and would strand the account forever.

    /// <summary>A pending STAFF/STAFF target — the shape a Staff Leader may re-role and rename.</summary>
    private static User PendingStaff(ulong id, string email = "old.holder@fpt.edu.vn")
    {
        var u = Uc106TestData.CreateUser(id, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId);
        u.Email = email;
        u.Status = UserStatuses.PendingEmailConfirmation;
        u.EmailVerifiedAt = new DateTime(2026, 1, 2);
        return u;
    }

    [Fact]
    public async Task PendingAccount_EmailChange_MailsTheActivationLink_NotAChangeNotice()
    {
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId));
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            Email = "New.Holder@fpt.edu.vn",
        });

        var confirmation = h.Dispatcher.Single(SystemEmailTemplates.AccountEmailConfirmation);
        Assert.Equal("new.holder@fpt.edu.vn", confirmation.To.Email);       // normalized
        // The activation button is the whole point — a mail without it cannot activate anything.
        Assert.Contains(
            h.Confirmations.IssuedTokens.Last(),
            confirmation.TrustedBlocks![EmailTrustedBlocks.ActionBlock]);

        // The notice templates belong to accounts that HAVE confirmed an address. Neither may appear.
        Assert.Empty(h.Dispatcher.All(SystemEmailTemplates.AccountEmailChangedNewNotice));
        Assert.Empty(h.Dispatcher.All(SystemEmailTemplates.AccountEmailChangedOldNotice));

        Assert.True(res.EmailChanged);
        Assert.True(res.RequiresEmailConfirmation);
        Assert.Equal("SENT", res.EmailNotificationStatus);
    }

    [Fact]
    public async Task PendingAccount_EmailChange_SendsTheOldAddressNothingButANeutralNotice()
    {
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId, "typo@fpt.edu.vn"));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160777",
            FullName = "Nguyen Van A",
            Email = "real.holder@fpt.edu.vn",
        });

        var notice = h.Dispatcher.Single(SystemEmailTemplates.AccountPendingEmailChangedOldNotice);
        Assert.Equal("typo@fpt.edu.vn", notice.To.Email);
        // The mistyped address may belong to a stranger: it learns that something changed, and nothing
        // else. No name (not even on the envelope), no new address, no role, no token.
        Assert.Empty(notice.Variables);
        Assert.True(string.IsNullOrEmpty(notice.To.DisplayName));
        Assert.Null(notice.TrustedBlocks);
    }

    [Fact]
    public async Task PendingAccount_RoleAndEmailInOneRequest_ConfirmationStatesTheFinalRole()
    {
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId));
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160123",
            FullName = "Tran Thi B",
            Email = "student.b@fpt.edu.vn",
        });

        // Not the snapshot the account was created with: the holder is being activated INTO the new
        // role, so that is the one the mail must name.
        var confirmation = h.Dispatcher.Single(SystemEmailTemplates.AccountEmailConfirmation);
        Assert.Equal("Tran Thi B", confirmation.Variables["fullName"]);
        Assert.Equal(
            AccountRoleDisplayNames.Resolve(RoleCodes.Student, null),
            confirmation.Variables["roleName"]);
    }

    [Fact]
    public async Task PendingAccount_RoleAndEmailCommitTogether_AndTheOldTokenDies()
    {
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId));
        h.Db.SaveChanges();
        // A live link already mailed to the old address.
        await h.Confirmations.IssuePendingAsync(TargetId, "old.holder@fpt.edu.vn", isResend: false, CancellationToken.None);
        await h.Db.SaveChangesAsync();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160124",
            Email = "moved@fpt.edu.vn",
        });

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        // One request, one transaction: neither half can land without the other.
        Assert.Equal("moved@fpt.edu.vn", user.Email);
        Assert.Equal(Uc106TestData.StudentRoleId, user.RoleId);
        Assert.Equal("HE160124", user.StudentCode);
        // Still pending — nothing here activates an account.
        Assert.Equal(UserStatuses.PendingEmailConfirmation, user.Status);
        Assert.Null(user.EmailVerifiedAt);

        var tokens = await h.Db.AccountEmailConfirmations.Where(c => c.UserId == TargetId).ToListAsync();
        var live = tokens.Where(c => c.Status == AccountEmailConfirmationStatuses.Pending).ToList();
        Assert.Single(live);                                      // exactly one link can confirm
        Assert.Equal("moved@fpt.edu.vn", live[0].TargetEmail);    // and it points at the new address
        Assert.Contains(tokens, c => c.Status == AccountEmailConfirmationStatuses.Superseded);
    }

    // ── The role notice does not depend on the account's status (PENDING_ROLE_CHANGE_EMAIL spec) ──
    //
    // A pending account used to be skipped, on the theory that its activation mail already names the
    // final role. But that mail only exists when the ADDRESS moved too — so re-roling a pending
    // account changed the permissions it will wake up with and told nobody. Status decides which
    // confirmation flow runs; it never decides whether the holder learns their role moved.

    [Fact]
    public async Task PendingAccount_RoleChangeOnly_IsStillToldTheirRoleMoved()
    {
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId, "pending.holder@fpt.edu.vn"));
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160126",
        });

        var mail = h.Dispatcher.Single(SystemEmailTemplates.AccountRoleChanged);
        // The address it currently holds — the only one it has.
        Assert.Equal("pending.holder@fpt.edu.vn", mail.To.Email);
        Assert.Equal(
            AccountRoleDisplayNames.Resolve(RoleCodes.Staff, UserSubRoles.Staff),
            mail.Variables["oldRoleName"]);
        Assert.Equal(
            AccountRoleDisplayNames.Resolve(RoleCodes.Student, null),
            mail.Variables["newRoleName"]);
        // Campus is read from the RESOLVED shape, not the pre-change snapshot.
        Assert.Equal($"Campus {Campus}", mail.Variables["campusName"]);
        Assert.Equal(TargetId, mail.RelatedId);
        Assert.Equal("User", mail.RelatedType);
        Assert.Equal(h.Actor.UserId, mail.SentBy);

        Assert.True(res.RoleChanged);
        Assert.False(res.EmailChanged);
        Assert.Equal("SENT", res.RoleChangeEmailNotificationStatus);
        // Nothing about the address moved, so neither address-related mail was due.
        Assert.Equal("NOT_REQUIRED", res.EmailNotificationStatus);
        Assert.Equal("NOT_REQUIRED", res.ConfirmationEmailNotificationStatus);
        Assert.True(res.RequiresEmailConfirmation);          // still pending, and says so
    }

    [Fact]
    public async Task PendingAccount_RoleChangeOnly_LeavesTheConfirmationTokenExactlyAsItWas()
    {
        // The token proves the holder owns the ADDRESS. A role change does not touch that ownership, so
        // re-issuing here would kill a live activation link — and burn a resend the operator still needs.
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId));
        h.Db.SaveChanges();
        await h.Confirmations.IssuePendingAsync(
            TargetId, "old.holder@fpt.edu.vn", isResend: true, CancellationToken.None);
        await h.Db.SaveChangesAsync();
        var issuedBefore = h.Confirmations.IssuedTokens.Count;

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160127",
        });

        Assert.Equal(issuedBefore, h.Confirmations.IssuedTokens.Count);      // no new token
        var token = Assert.Single(await h.Db.AccountEmailConfirmations
            .Where(c => c.UserId == TargetId).ToListAsync());
        Assert.Equal(AccountEmailConfirmationStatuses.Pending, token.Status);   // the live link survives
        Assert.Equal("old.holder@fpt.edu.vn", token.TargetEmail);
        Assert.Equal(1, token.ResendCount);                                 // not bumped

        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, user.Status);    // never auto-activated
        Assert.Empty(h.Dispatcher.All(SystemEmailTemplates.AccountEmailConfirmation));
    }

    [Fact]
    public async Task PendingAccount_RoleAndEmail_SendsBothMailsToTheNewAddress()
    {
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId, "typo@fpt.edu.vn"));
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160125",
            Email = "Again@fpt.edu.vn",
        });

        // Two facts, two messages — the activation link and the role notice — both aimed at the address
        // the holder will actually be reading.
        Assert.Equal("again@fpt.edu.vn",
            h.Dispatcher.Single(SystemEmailTemplates.AccountEmailConfirmation).To.Email);
        var roleMail = h.Dispatcher.Single(SystemEmailTemplates.AccountRoleChanged);
        Assert.Equal("again@fpt.edu.vn", roleMail.To.Email);
        // The old address gets nothing but the neutral notice: no role, no name, no token.
        Assert.Equal("typo@fpt.edu.vn",
            h.Dispatcher.Single(SystemEmailTemplates.AccountPendingEmailChangedOldNotice).To.Email);

        Assert.True(res.RoleChanged);
        Assert.True(res.EmailChanged);
        Assert.True(res.RequiresEmailConfirmation);
        Assert.Equal("SENT", res.ConfirmationEmailNotificationStatus);
        Assert.Equal("SENT", res.RoleChangeEmailNotificationStatus);
    }

    [Fact]
    public async Task PendingAccount_AFailedConfirmationStillLetsTheRoleNoticeGoOut()
    {
        // The two mails are independent in both directions. Suppressing the role notice because the
        // activation link failed would lose the one message that CAN still be delivered.
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId));
        h.Db.SaveChanges();
        h.Dispatcher.OutcomeFor = req =>
            req.TemplateCode == SystemEmailTemplates.AccountEmailConfirmation
                ? EmailDeliveryResult.Failed("SMTP", "không gửi được")
                : EmailDeliveryResult.Sent();

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160128",
            Email = "reachable@fpt.edu.vn",
        });

        Assert.Single(h.Dispatcher.All(SystemEmailTemplates.AccountRoleChanged));
        Assert.Equal("FAILED", res.ConfirmationEmailNotificationStatus);
        Assert.Equal("SENT", res.RoleChangeEmailNotificationStatus);
        // And the change itself stands: an undeliverable mail is not a reason to undo a commit.
        Assert.Equal("reachable@fpt.edu.vn",
            (await h.Db.Users.SingleAsync(u => u.UserId == TargetId)).Email);
    }

    [Theory]
    [InlineData(EmailDeliveryStatus.Skipped, "SKIPPED")]
    [InlineData(EmailDeliveryStatus.Failed, "FAILED")]
    public async Task RoleNoticeDelivery_IsReportedTruthfully_AndNeverRollsTheRoleBack(
        EmailDeliveryStatus delivery, string expected)
    {
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId));
        h.Db.SaveChanges();
        h.Dispatcher.Outcome = new EmailDeliveryResult(delivery);

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160129",
        });

        Assert.Equal(expected, res.RoleChangeEmailNotificationStatus);
        // Reporting a failed mail as SENT would leave the operator believing the holder had been told.
        Assert.NotEqual("SENT", res.RoleChangeEmailNotificationStatus);
        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Equal(Uc106TestData.StudentRoleId, user.RoleId);
        Assert.Equal("HE160129", user.StudentCode);
    }

    [Fact]
    public async Task RoleNoticeThatThrows_IsReportedAsFailed_NotPropagated()
    {
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId));
        h.Db.SaveChanges();
        h.Dispatcher.ThrowOnSend = new InvalidOperationException("dispatcher down");

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160130",
        });

        // A 500 here would tell the caller the role change failed, when it is committed and correct.
        Assert.Equal("FAILED", res.RoleChangeEmailNotificationStatus);
        Assert.Equal(Uc106TestData.StudentRoleId,
            (await h.Db.Users.SingleAsync(u => u.UserId == TargetId)).RoleId);
    }

    [Fact]
    public async Task PendingAccount_NameOnlyEdit_SendsNoRoleNotice()
    {
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId));
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,              // same shape
            FullName = "Sửa Lại Tên",
        });

        // ACCOUNT_ROLE_CHANGED says only "your role went from X to Y". Nothing here moved a role.
        Assert.Empty(h.Dispatcher.Sent);
        Assert.False(res.RoleChanged);
        Assert.Equal("NOT_REQUIRED", res.RoleChangeEmailNotificationStatus);
        Assert.Equal("Sửa Lại Tên", (await h.Db.Users.SingleAsync(u => u.UserId == TargetId)).FullName);
    }

    [Fact]
    public async Task PendingAccount_StudentCodeOnlyEdit_SendsNoRoleNotice()
    {
        var h = CreateHarness();
        var pending = Student(TargetId, "HE160001");
        pending.Status = UserStatuses.PendingEmailConfirmation;
        h.Db.Users.Add(pending);
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,            // STUDENT → STUDENT
            StudentCode = "HE160002",
        });

        Assert.Empty(h.Dispatcher.Sent);
        Assert.False(res.RoleChanged);
        Assert.Equal("NOT_REQUIRED", res.RoleChangeEmailNotificationStatus);
        Assert.Equal("HE160002", (await h.Db.Users.SingleAsync(u => u.UserId == TargetId)).StudentCode);
    }

    [Fact]
    public async Task InactiveAccount_RoleChange_IsToldToo()
    {
        // The status the account happens to be parked in never decides this.
        var h = CreateHarness();
        var user = Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId);
        user.Status = UserStatuses.Inactive;
        user.Email = "inactive.holder@fpt.edu.vn";
        h.Db.Users.Add(user);
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160131",
        });

        Assert.Equal("inactive.holder@fpt.edu.vn",
            h.Dispatcher.Single(SystemEmailTemplates.AccountRoleChanged).To.Email);
        Assert.True(res.RoleChanged);
        Assert.Equal("SENT", res.RoleChangeEmailNotificationStatus);
        Assert.False(res.RequiresEmailConfirmation);
    }

    [Theory]
    [InlineData(EmailDeliveryStatus.Skipped, "SKIPPED")]
    [InlineData(EmailDeliveryStatus.Failed, "FAILED")]
    public async Task PendingAccount_ReportsTheConfirmationDeliveryTruthfully(
        EmailDeliveryStatus delivery, string expected)
    {
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId));
        h.Db.SaveChanges();
        h.Dispatcher.Outcome = new EmailDeliveryResult(delivery);

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            Email = "undeliverable@fpt.edu.vn",
        });

        // Announcing success on a mail nobody received would send the caller away waiting for a
        // confirmation that cannot arrive. The account change itself is committed regardless.
        Assert.Equal(expected, res.EmailNotificationStatus);
        Assert.Equal("undeliverable@fpt.edu.vn", (await h.Db.Users.SingleAsync(u => u.UserId == TargetId)).Email);
    }

    [Fact]
    public async Task PendingAccount_AFailedOldNoticeDoesNotDowngradeTheConfirmation()
    {
        var h = CreateHarness();
        h.Db.Users.Add(PendingStaff(TargetId));
        h.Db.SaveChanges();
        // Only the notice to the previous address fails.
        h.Dispatcher.OutcomeFor = req =>
            req.TemplateCode == SystemEmailTemplates.AccountPendingEmailChangedOldNotice
                ? EmailDeliveryResult.Failed("SMTP", "không gửi được")
                : EmailDeliveryResult.Sent();

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            Email = "reachable@fpt.edu.vn",
        });

        // The mail that decides whether this account can ever be activated went out. A best-effort
        // notice to an address that may be a stranger's must not be able to report otherwise.
        Assert.Equal("SENT", res.EmailNotificationStatus);
    }

    [Fact]
    public async Task PendingAccount_RejectsAnEmailAlreadyUsedElsewhere()
    {
        var h = CreateHarness();
        var other = PendingStaff(200, "taken@fpt.edu.vn");
        other.Status = UserStatuses.Active;
        h.Db.Users.Add(other);
        h.Db.Users.Add(PendingStaff(TargetId));
        h.Db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            Email = "taken@fpt.edu.vn",
        }));

        // Refused means unchanged: no token issued, no mail, nothing written.
        Assert.Empty(h.Confirmations.IssuedTokens);
        Assert.Empty(h.Dispatcher.Sent);
        Assert.Equal("old.holder@fpt.edu.vn", (await h.Db.Users.SingleAsync(u => u.UserId == TargetId)).Email);
    }

    // ── Email change: accounts that HAVE confirmed an address (spec §18/§19/§20) ──

    [Fact]
    public async Task ActiveAccount_EmailChange_SendsBothNotices_AndNoActivationLink()
    {
        var h = CreateHarness();
        var user = Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId);
        user.Email = "before@fpt.edu.vn";
        h.Db.Users.Add(user);
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            Email = "after@fpt.edu.vn",
        });

        Assert.Equal("before@fpt.edu.vn",
            h.Dispatcher.Single(SystemEmailTemplates.AccountEmailChangedOldNotice).To.Email);
        var newNotice = h.Dispatcher.Single(SystemEmailTemplates.AccountEmailChangedNewNotice);
        Assert.Equal("after@fpt.edu.vn", newNotice.To.Email);
        // Enough of the old address to recognise it, not enough to be a disclosure.
        Assert.Equal(AccountEmailVariables.MaskEmail("before@fpt.edu.vn"), newNotice.Variables["oldEmailMasked"]);

        // This account HAS proven an address; it is re-verifying, not activating.
        Assert.Empty(h.Dispatcher.All(SystemEmailTemplates.AccountEmailConfirmation));
        Assert.True(res.EmailChanged);
        Assert.False(res.RequiresEmailConfirmation);
        Assert.Equal("SENT", res.EmailNotificationStatus);
    }

    [Fact]
    public async Task ActiveAccount_EmailChange_ClearsVerificationAndRevokesSessions()
    {
        var h = CreateHarness();
        var user = Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId);
        user.Email = "before@fpt.edu.vn";
        user.EmailVerifiedAt = new DateTime(2026, 1, 5);
        h.Db.Users.Add(user);
        h.Db.UserAuthProviders.AddRange(
            new UserAuthProvider
            {
                UserId = TargetId, ProviderType = ProviderTypes.LocalPassword,
                ProviderEmail = "before@fpt.edu.vn", IsEnabled = true, LinkedAt = new DateTime(2026, 1, 1),
            },
            new UserAuthProvider
            {
                UserId = TargetId, ProviderType = ProviderTypes.GoogleSso, ProviderSubject = "google-sub-1",
                ProviderEmail = "before@fpt.edu.vn", IsEnabled = true, LinkedAt = new DateTime(2026, 1, 1),
            });
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            Email = "after@fpt.edu.vn",
        });

        var saved = await h.Db.Users.SingleAsync(u => u.UserId == TargetId);
        Assert.Null(saved.EmailVerifiedAt);                       // must prove the new address
        Assert.NotEmpty(h.Sessions.RevokeAllCalls);               // the old identity stops signing in

        var providers = await h.Db.UserAuthProviders.Where(p => p.UserId == TargetId).ToListAsync();
        // The external binding was proven against the OLD address, so it goes; the local one follows.
        Assert.DoesNotContain(providers, p => p.ProviderType == ProviderTypes.GoogleSso);
        Assert.Equal("after@fpt.edu.vn",
            providers.Single(p => p.ProviderType == ProviderTypes.LocalPassword).ProviderEmail);
    }

    [Fact]
    public async Task ActiveAccount_RoleAndEmailChange_SendsTheRoleNoticeAsWell()
    {
        var h = CreateHarness();
        var user = Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId);
        user.Email = "before@fpt.edu.vn";
        h.Db.Users.Add(user);
        h.Db.SaveChanges();

        await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Student,
            StudentCode = "HE160222",
            Email = "after@fpt.edu.vn",
        });

        // Three separate facts, three separate messages — no combined template is invented for this.
        Assert.Single(h.Dispatcher.All(SystemEmailTemplates.AccountEmailChangedOldNotice));
        Assert.Single(h.Dispatcher.All(SystemEmailTemplates.AccountEmailChangedNewNotice));
        Assert.Single(h.Dispatcher.All(SystemEmailTemplates.AccountRoleChanged));
    }

    [Fact]
    public async Task ActiveAccount_OneNoticeFailing_IsReportedAsPartial()
    {
        var h = CreateHarness();
        var user = Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId);
        user.Email = "before@fpt.edu.vn";
        h.Db.Users.Add(user);
        h.Db.SaveChanges();
        h.Dispatcher.OutcomeFor = req =>
            req.TemplateCode == SystemEmailTemplates.AccountEmailChangedOldNotice
                ? EmailDeliveryResult.Failed("SMTP", "không gửi được")
                : EmailDeliveryResult.Sent();

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            Email = "after@fpt.edu.vn",
        });

        Assert.Equal("PARTIAL", res.EmailNotificationStatus);
        // Partial delivery is a reporting fact, not a reason to undo a committed change.
        Assert.Equal("after@fpt.edu.vn", (await h.Db.Users.SingleAsync(u => u.UserId == TargetId)).Email);
    }

    [Fact]
    public async Task NameOnlyEdit_SendsNothingAtAll()
    {
        var h = CreateHarness();
        var user = Uc106TestData.CreateUser(TargetId, Uc106TestData.StaffRoleId, UserSubRoles.Staff, IcDeptId);
        user.Email = "stable@fpt.edu.vn";
        h.Db.Users.Add(user);
        h.Db.SaveChanges();

        var res = await h.Run(new UpdateAccountRoleCommand
        {
            UserId = TargetId,
            NewRoleCode = RoleCodes.Staff,
            FullName = "Correcting A Typo",
        });

        // Neither the address nor the role moved. ACCOUNT_ROLE_CHANGED says only "your role went from
        // X to Y" — sending it here would tell the holder something untrue.
        Assert.Empty(h.Dispatcher.Sent);
        Assert.False(res.EmailChanged);
        Assert.Equal("NOT_REQUIRED", res.EmailNotificationStatus);
        Assert.Equal("Correcting A Typo", (await h.Db.Users.SingleAsync(u => u.UserId == TargetId)).FullName);
    }

    // ── helpers for reading the structured 409 payload ────────────────────────

    private static IReadOnlyList<AccountRoleChangeBlocker> BlockersOf(ConflictException ex)
    {
        var data = ex.Data ?? throw new Xunit.Sdk.XunitException("Blocker conflict carried no data payload.");
        var property = data.GetType().GetProperty("blockers")
            ?? throw new Xunit.Sdk.XunitException("Blocker payload has no 'blockers' property.");
        return (IReadOnlyList<AccountRoleChangeBlocker>)property.GetValue(data)!;
    }

    private static string[] BlockerTypesOf(ConflictException ex)
        => BlockersOf(ex).Select(b => b.Type).ToArray();

    private static int AffectedVisitCountOf(ConflictException ex)
    {
        var data = ex.Data!;
        return (int)data.GetType().GetProperty("affectedVisitCount")!.GetValue(data)!;
    }
}
