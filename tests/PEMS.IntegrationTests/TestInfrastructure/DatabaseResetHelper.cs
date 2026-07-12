using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Faqs;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.Application.Common;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Seeds and cleans the minimal <c>pems_test</c> data Integration Tests need: one
/// deterministic user per role under test, an active session per user (so
/// SessionValidationMiddleware lets the request through), and removal of the FAQ rows a
/// test created — WITHOUT touching unrelated seed data already in <c>pems_test</c>
/// (roles/campuses/other users from the fresh-create script are left alone).
///
/// Each FAQ use-case test class must use its own dedicated question prefix (e.g.
/// <see cref="CreateFaqQuestionPrefix"/>, <see cref="UpdateFaqQuestionPrefix"/>) and pass it to
/// <see cref="DeleteTestFaqsAsync"/>. A single shared prefix previously caused a real race
/// condition: xUnit runs different test classes in parallel by default, so
/// CreateFaqApiTests.DisposeAsync deleting "every row with the shared prefix" could wipe out
/// UpdateFaqApiTests' in-flight data (and vice versa) — surfacing as NotFound,
/// DbUpdateConcurrencyException, or a duplicate check that should have conflicted but didn't.
/// Parallelization across test classes is also disabled assembly-wide (see AssemblyInfo.cs) as a
/// second, independent safety layer — this prefix separation is the data-isolation layer.
/// Prefixes must not overlap as substrings of one another.
/// </summary>
public static class DatabaseResetHelper
{
    public const string CreateFaqQuestionPrefix = "[IT-CREATE-FAQ] ";
    public const string UpdateFaqQuestionPrefix = "[IT-UPDATE-FAQ] ";
    public const string ViewListFaqQuestionPrefix = "[IT-VIEW-LIST-FAQ] ";
    public const string ChangeFaqVisibilityQuestionPrefix = "[IT-CHANGE-FAQ-VISIBILITY] ";
    public const string SearchFaqQuestionPrefix = "[IT-SEARCH-FAQ] ";
    public const string AddDepartmentNamePrefix = "[IT-UC101-ADD-DEPARTMENT] ";
    public const string UpdateDepartmentNamePrefix = "[IT-UC102-UPDATE-DEPARTMENT] ";
    public const string SearchFilterDepartmentNamePrefix = "[IT-UC103-SEARCH-FILTER-DEPARTMENT] ";

    private const string TestUserEmailDomain = "@it-uc63.pems.local";
    private const string InactiveCampusTestCode = "IT-UC101-INACTIVE";
    private const string IcProtectionTestCampusCode = "IT-UC102-IC-TEST";

    /// <summary>
    /// Gets or creates a deterministic ACTIVE test user for the given effective role
    /// (<see cref="EffectiveRole.Ho"/>, <c>STAFF</c>, <see cref="EffectiveRole.StaffLeader"/>,
    /// <see cref="EffectiveRole.DepartmentLead"/>, <see cref="EffectiveRole.Department"/>,
    /// <see cref="EffectiveRole.Student"/>, <see cref="EffectiveRole.Admin"/>,
    /// <see cref="EffectiveRole.Visitor"/>) and returns its user id.
    /// </summary>
    public static async Task<ulong> EnsureTestUserAsync(
        ApplicationDbContext db,
        string effectiveRole,
        CancellationToken cancellationToken = default)
    {
        var (roleCode, subRole) = effectiveRole switch
        {
            EffectiveRole.Ho => (RoleCode.Ho, (string?)null),
            EffectiveRole.Admin => (RoleCode.Admin, (string?)null),
            EffectiveRole.Staff => (RoleCode.Staff, SubRole.Staff),
            EffectiveRole.StaffLeader => (RoleCode.Staff, SubRole.Leader),
            EffectiveRole.DepartmentLead => (RoleCode.Department, SubRole.Leader),
            EffectiveRole.Department => (RoleCode.Department, SubRole.Staff),
            EffectiveRole.Student => (RoleCode.Student, (string?)null),
            EffectiveRole.Visitor => (RoleCode.Visitor, (string?)null),
            _ => throw new ArgumentOutOfRangeException(
                nameof(effectiveRole), effectiveRole, "Unsupported effective role for integration tests.")
        };

        var email = $"it-uc63-{effectiveRole.ToLowerInvariant()}{TestUserEmailDomain}";

        var existing = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existing is not null)
            return existing.UserId;

        var role = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleCode == roleCode, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Role '{roleCode}' was not found in pems_test. Import the fresh-create schema first.");

        var isInternalPortal = effectiveRole != EffectiveRole.Visitor;

        ulong? primaryCampusId = null;
        if (isInternalPortal)
        {
            primaryCampusId = (await db.Campuses.AsNoTracking()
                .Where(c => c.Status == EntityStatuses.Active)
                .Select(c => (ulong?)c.CampusId)
                .FirstOrDefaultAsync(cancellationToken))
                ?? throw new InvalidOperationException(
                    "No active campus found in pems_test to attach an internal test user to.");
        }

        // DB trigger trg_users_validate_bi/bu requires STAFF/DEPARTMENT users to have a
        // department_id whose department_type matches the role: STAFF -> IC, DEPARTMENT ->
        // GENERAL (regardless of sub_role STAFF/LEADER). Look up an existing active department
        // of the right type on the same campus from the seed data — never hard-code a
        // department_id.
        ulong? departmentId = null;
        if (effectiveRole is EffectiveRole.Staff or EffectiveRole.StaffLeader)
        {
            departmentId = (await db.Departments.AsNoTracking()
                .Where(d => d.CampusId == primaryCampusId
                            && d.DepartmentType == "IC"
                            && d.Status == EntityStatuses.Active)
                .Select(d => (ulong?)d.DepartmentId)
                .FirstOrDefaultAsync(cancellationToken))
                ?? throw new InvalidOperationException(
                    "No active IC department found in pems_test for the selected campus to attach a STAFF test user to.");
        }
        else if (effectiveRole is EffectiveRole.DepartmentLead or EffectiveRole.Department)
        {
            departmentId = (await db.Departments.AsNoTracking()
                .Where(d => d.CampusId == primaryCampusId
                            && d.DepartmentType == "GENERAL"
                            && d.Status == EntityStatuses.Active)
                .Select(d => (ulong?)d.DepartmentId)
                .FirstOrDefaultAsync(cancellationToken))
                ?? throw new InvalidOperationException(
                    "No active GENERAL department found in pems_test for the selected campus to attach a DEPARTMENT test user to.");
        }
        // EffectiveRole.Student falls through with departmentId = null: trg_users_validate_bi/bu's
        // ELSE branch (any role other than VISITOR/STAFF/DEPARTMENT) requires sub_role and
        // department_id to both be NULL, and primary_campus_id to be set.

        var user = new User
        {
            FullName = $"[IT-UC63] Test {effectiveRole}",
            Email = email,
            RoleId = role.RoleId,
            SubRole = subRole,
            PrimaryCampusId = primaryCampusId,
            DepartmentId = departmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = VietnamTime.Now()
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.UserId;
    }

    /// <summary>
    /// Creates a fresh active (not revoked, not expired) session for <paramref name="userId"/>
    /// so <c>SessionValidationMiddleware</c> accepts requests carrying its session id claim.
    /// </summary>
    public static async Task<ulong> CreateActiveSessionAsync(
        ApplicationDbContext db,
        ulong userId,
        string effectiveRole,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstAsync(u => u.UserId == userId, cancellationToken);

        var isInternalPortal = effectiveRole != EffectiveRole.Visitor;
        var now = VietnamTime.Now();

        var session = new UserSession
        {
            UserId = userId,
            LoginPortal = isInternalPortal ? LoginPortals.Internal : LoginPortals.Visitor,
            SelectedCampusId = isInternalPortal ? user.PrimaryCampusId : null,
            CreatedAt = now,
            ExpiresAt = now.AddHours(2)
        };

        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session.SessionId;
    }

    /// <summary>
    /// Inserts a FAQ row directly (bypassing the Create FAQ API, per Update FAQ test independence)
    /// for Update FAQ tests to target. <paramref name="question"/> must already carry the
    /// caller's own dedicated prefix (e.g. <see cref="UpdateFaqQuestionPrefix"/>) so
    /// <see cref="DeleteTestFaqsAsync"/> can clean it up without touching other test classes' data.
    /// Leaves <c>UpdatedAt</c>/<c>UpdatedBy</c> null, matching a FAQ that was created but never
    /// updated yet — a clean baseline for audit-refresh assertions.
    /// </summary>
    public static async Task<ulong> CreateTestFaqAsync(
        ApplicationDbContext db,
        string question,
        string answer,
        string faqType,
        string status,
        ulong? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        var faq = new Faq
        {
            FaqType = faqType,
            Question = question,
            Answer = answer,
            DisplayOrder = 0,
            Status = status,
            CreatedAt = VietnamTime.Now(),
            CreatedBy = createdBy,
            UpdatedAt = null,
            UpdatedBy = null
        };

        db.Faqs.Add(faq);
        await db.SaveChangesAsync(cancellationToken);
        return faq.FaqId;
    }

    /// <summary>
    /// Removes every FAQ row whose question starts with <paramref name="prefix"/>. Callers must
    /// pass their own dedicated prefix (e.g. <see cref="CreateFaqQuestionPrefix"/>,
    /// <see cref="UpdateFaqQuestionPrefix"/>) — never a prefix shared with another test class.
    /// </summary>
    public static async Task DeleteTestFaqsAsync(ApplicationDbContext db, string prefix, CancellationToken cancellationToken = default)
    {
        var testFaqs = await db.Faqs
            .Where(f => f.Question.StartsWith(prefix))
            .ToListAsync(cancellationToken);

        if (testFaqs.Count == 0)
            return;

        db.Faqs.RemoveRange(testFaqs);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Inserts a Department row directly for Add/Update/Search Department tests to seed a
    /// pre-existing department. <paramref name="name"/> must already carry the caller's own
    /// dedicated prefix so <see cref="DeleteTestDepartmentsAsync"/> can clean it up without
    /// touching other test classes' data or real seed departments. Leaves <c>HeadUserId</c> null
    /// unless <paramref name="headUserId"/> is supplied (needed for UC-103's "keyword matches head
    /// full name" behavior) — never attaches a user by default, so cleanup never hits the
    /// department_id FK (RESTRICT).
    /// </summary>
    public static async Task<ulong> CreateTestDepartmentAsync(
        ApplicationDbContext db,
        string name,
        ulong campusId,
        string departmentType,
        string status,
        ulong? createdBy = null,
        ulong? headUserId = null,
        CancellationToken cancellationToken = default)
    {
        var department = new Department
        {
            CampusId = campusId,
            Name = name,
            DepartmentType = departmentType,
            HeadUserId = headUserId,
            Status = status,
            CreatedAt = VietnamTime.Now(),
            CreatedBy = createdBy,
            UpdatedAt = null,
            UpdatedBy = null
        };

        db.Departments.Add(department);
        await db.SaveChangesAsync(cancellationToken);
        return department.DepartmentId;
    }

    /// <summary>
    /// Creates a minimal, uniquely-named STUDENT-role test user (no sub_role/department_id
    /// required by the trigger) purely to serve as a Department's <c>head_user_id</c> so UC-103's
    /// "keyword matches name + head full name" behavior can be tested. Unlike
    /// <see cref="EnsureTestUserAsync"/>, this is NOT idempotent/reused — each call creates a fresh
    /// row, since callers need a specific, unique FullName to search for. Not cleaned up
    /// afterwards, matching the project's existing convention of never deleting test Users.
    /// </summary>
    public static async Task<ulong> CreateHeadUserCandidateAsync(
        ApplicationDbContext db,
        ulong campusId,
        string fullName,
        CancellationToken cancellationToken = default)
    {
        var role = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleCode == RoleCode.Student, cancellationToken)
            ?? throw new InvalidOperationException("Role 'STUDENT' was not found in pems_test. Import the fresh-create schema first.");

        var user = new User
        {
            FullName = fullName,
            Email = $"it-uc103-head-{Guid.NewGuid():N}{TestUserEmailDomain}",
            RoleId = role.RoleId,
            SubRole = null,
            PrimaryCampusId = campusId,
            DepartmentId = null,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.UserId;
    }

    /// <summary>
    /// Removes every Department row whose name starts with <paramref name="prefix"/>. Callers
    /// must pass their own dedicated prefix (e.g. <see cref="AddDepartmentNamePrefix"/>) — never a
    /// prefix shared with another test class. Safe only because test departments created via
    /// <see cref="CreateTestDepartmentAsync"/> or the Add New Department API never get a user
    /// attached (fk_users_department is ON DELETE RESTRICT).
    /// </summary>
    public static async Task DeleteTestDepartmentsAsync(ApplicationDbContext db, string prefix, CancellationToken cancellationToken = default)
    {
        var testDepartments = await db.Departments
            .Where(d => d.Name.StartsWith(prefix))
            .ToListAsync(cancellationToken);

        if (testDepartments.Count == 0)
            return;

        db.Departments.RemoveRange(testDepartments);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets or creates a dedicated, isolated INACTIVE test campus (its own campus_code, never a
    /// real seed campus) with an active IC department and an ACTIVE Staff Leader user whose
    /// <c>primary_campus_id</c> points to that inactive campus — used only to test
    /// AddNewDepartmentCommandHandler's "caller's own campus is INACTIVE" rule (UC-101 manual test
    /// #7). Idempotent: reused across test runs the same way <see cref="EnsureTestUserAsync"/>
    /// reuses its users, so nothing here needs cleanup.
    /// </summary>
    public static async Task<ulong> EnsureInactiveCampusStaffLeaderAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var email = $"it-uc101-inactive-campus-staffleader{TestUserEmailDomain}";

        var existingUser = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existingUser is not null)
            return existingUser.UserId;

        var campus = await db.Campuses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CampusCode == InactiveCampusTestCode, cancellationToken);

        ulong campusId;
        if (campus is not null)
        {
            campusId = campus.CampusId;
        }
        else
        {
            var newCampus = new Campus
            {
                CampusCode = InactiveCampusTestCode,
                Name = "[IT-UC101] Inactive Test Campus",
                Status = EntityStatuses.Inactive,
                CreatedAt = VietnamTime.Now()
            };
            db.Campuses.Add(newCampus);
            await db.SaveChangesAsync(cancellationToken);
            campusId = newCampus.CampusId;
        }

        var icDepartment = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == campusId && d.DepartmentType == "IC" && d.Status == EntityStatuses.Active)
            .Select(d => (ulong?)d.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (icDepartment is null)
        {
            var newIcDepartment = new Department
            {
                CampusId = campusId,
                Name = "[IT-UC101] Inactive Campus IC Department",
                DepartmentType = "IC",
                HeadUserId = null,
                Status = EntityStatuses.Active,
                CreatedAt = VietnamTime.Now()
            };
            db.Departments.Add(newIcDepartment);
            await db.SaveChangesAsync(cancellationToken);
            icDepartment = newIcDepartment.DepartmentId;
        }

        var role = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleCode == RoleCode.Staff, cancellationToken)
            ?? throw new InvalidOperationException("Role 'STAFF' was not found in pems_test. Import the fresh-create schema first.");

        var user = new User
        {
            FullName = "[IT-UC101] Test StaffLeader (Inactive Campus)",
            Email = email,
            RoleId = role.RoleId,
            SubRole = SubRole.Leader,
            PrimaryCampusId = campusId,
            DepartmentId = icDepartment,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = VietnamTime.Now()
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.UserId;
    }

    /// <summary>
    /// Gets or creates a dedicated, isolated ACTIVE test campus (its own campus_code, never a real
    /// seed campus) with its own IC department and an ACTIVE Staff Leader user whose
    /// <c>primary_campus_id</c>/<c>department_id</c> point there — used only to test
    /// UpdateDepartmentCommandHandler's "cannot rename the IC department" rule (UC-102) WITHOUT
    /// ever targeting a real seed campus's IC department. That matters because if a future
    /// regression ever let this update through, the IC department's name would gain the
    /// <see cref="UpdateDepartmentNamePrefix"/> and <see cref="DeleteTestDepartmentsAsync"/> would
    /// delete it on cleanup — catastrophic if it were a real campus's IC department (FK violations
    /// from real users referencing it, or silent loss of real seed data). Using a campus nothing
    /// else references confines that worst case to disposable test data.
    /// Idempotent: reused across test runs the same way <see cref="EnsureTestUserAsync"/> reuses
    /// its users, so nothing here needs cleanup.
    /// </summary>
    public static async Task<(ulong UserId, ulong CampusId, ulong IcDepartmentId)> EnsureIcProtectionTestContextAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var email = $"it-uc102-ic-protection-staffleader{TestUserEmailDomain}";

        var campus = await db.Campuses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CampusCode == IcProtectionTestCampusCode, cancellationToken);

        ulong campusId;
        if (campus is not null)
        {
            campusId = campus.CampusId;
        }
        else
        {
            var newCampus = new Campus
            {
                CampusCode = IcProtectionTestCampusCode,
                Name = "[IT-UC102] IC Protection Test Campus",
                Status = EntityStatuses.Active,
                CreatedAt = VietnamTime.Now()
            };
            db.Campuses.Add(newCampus);
            await db.SaveChangesAsync(cancellationToken);
            campusId = newCampus.CampusId;
        }

        var icDepartmentId = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == campusId && d.DepartmentType == "IC" && d.Status == EntityStatuses.Active)
            .Select(d => (ulong?)d.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (icDepartmentId is null)
        {
            var newIcDepartment = new Department
            {
                CampusId = campusId,
                Name = "[IT-UC102] IC Protection Test IC Department",
                DepartmentType = "IC",
                HeadUserId = null,
                Status = EntityStatuses.Active,
                CreatedAt = VietnamTime.Now()
            };
            db.Departments.Add(newIcDepartment);
            await db.SaveChangesAsync(cancellationToken);
            icDepartmentId = newIcDepartment.DepartmentId;
        }

        var existingUser = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existingUser is not null)
            return (existingUser.UserId, campusId, icDepartmentId.Value);

        var role = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleCode == RoleCode.Staff, cancellationToken)
            ?? throw new InvalidOperationException("Role 'STAFF' was not found in pems_test. Import the fresh-create schema first.");

        var user = new User
        {
            FullName = "[IT-UC102] Test StaffLeader (IC Protection)",
            Email = email,
            RoleId = role.RoleId,
            SubRole = SubRole.Leader,
            PrimaryCampusId = campusId,
            DepartmentId = icDepartmentId,
            Status = UserStatuses.Active,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = VietnamTime.Now()
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return (user.UserId, campusId, icDepartmentId.Value);
    }
}
