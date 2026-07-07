using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
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
/// Test FAQ rows must always be created with <see cref="FaqQuestionPrefix"/> so
/// <see cref="DeleteTestFaqsAsync"/> can find and remove exactly (and only) what a test
/// created.
/// </summary>
public static class DatabaseResetHelper
{
    public const string FaqQuestionPrefix = "[IT-UC63] ";

    private const string TestUserEmailDomain = "@it-uc63.pems.local";

    /// <summary>
    /// Gets or creates a deterministic ACTIVE test user for the given effective role
    /// (<see cref="EffectiveRole.Ho"/>, <c>STAFF</c>, <see cref="EffectiveRole.Admin"/>,
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

        // DB trigger requires STAFF/DEPARTMENT users to have a department_id. STAFF belongs to
        // an IC department (same convention used across the app, e.g.
        // AccountProvisioningRules.cs: "d.CampusId == actorCampusId && d.DepartmentType == "IC""),
        // so look up an existing active IC department on the same campus from the seed data —
        // never hard-code a department_id.
        ulong? departmentId = null;
        if (effectiveRole == EffectiveRole.Staff)
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

    /// <summary>Removes every FAQ row whose question starts with <see cref="FaqQuestionPrefix"/>.</summary>
    public static async Task DeleteTestFaqsAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        var testFaqs = await db.Faqs
            .Where(f => f.Question.StartsWith(FaqQuestionPrefix))
            .ToListAsync(cancellationToken);

        if (testFaqs.Count == 0)
            return;

        db.Faqs.RemoveRange(testFaqs);
        await db.SaveChangesAsync(cancellationToken);
    }
}
