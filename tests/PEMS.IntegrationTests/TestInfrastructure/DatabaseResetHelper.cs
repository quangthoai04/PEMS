using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Faqs;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;

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

    private const string TestUserEmailDomain = "@it-uc63.pems.local";

    /// <summary>
    /// Gets or creates a deterministic ACTIVE test user for the given effective role
    /// (<see cref="EffectiveRole.Ho"/>, <c>STAFF</c>, <see cref="EffectiveRole.StaffLeader"/>,
    /// <see cref="EffectiveRole.Admin"/>, <see cref="EffectiveRole.Visitor"/>) and returns its
    /// user id.
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
            EffectiveRole.Visitor => (RoleCode.Visitor, (string?)null),
            _ => throw new ArgumentOutOfRangeException(
                nameof(effectiveRole), effectiveRole, "Unsupported effective role for Create FAQ integration tests.")
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

        // DB trigger requires STAFF/DEPARTMENT users to have a department_id, regardless of
        // sub_role (STAFF or LEADER). STAFF/STAFF_LEADER belong to an IC department (same
        // convention used across the app, e.g. AccountProvisioningRules.cs:
        // "d.CampusId == actorCampusId && d.DepartmentType == "IC""), so look up an existing
        // active IC department on the same campus from the seed data — never hard-code a
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
            CreatedAt = DateTime.UtcNow
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
        var now = DateTime.UtcNow;

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
            CreatedAt = DateTime.UtcNow,
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
}
