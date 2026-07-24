using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Regression cover for the cleanup path that intermittently failed a whole test class.
///
/// <c>DeleteTestDepartmentsAsync</c> used to delete departments assuming none of them could have a user
/// attached. That assumption held only by luck: <c>EnsureTestUserAsync</c> picked a department with an
/// unordered <c>FirstOrDefault</c> over "active department of this type on this campus", which sometimes
/// returned a department a test class had just created under its own prefix. The class then deleted it in
/// DisposeAsync, fk_users_department (ON DELETE RESTRICT) refused, and all 22 of its tests failed —
/// reproducibly only some of the time, because it depended on the order MySQL happened to return rows in.
///
/// Two properties are pinned here: cleanup survives an attached user, and it stays inside its own prefix.
/// </summary>
public sealed class DepartmentCleanupIsolationTests : IAsyncLifetime
{
    private const string GroupAPrefix = "[IT-CLEANUP-ISOLATION-A] ";
    private const string GroupBPrefix = "[IT-CLEANUP-ISOLATION-B] ";

    private readonly PemsWebApplicationFactory _factory = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await DatabaseResetHelper.DeleteTestDepartmentsAsync(db, GroupAPrefix);
            await DatabaseResetHelper.DeleteTestDepartmentsAsync(db, GroupBPrefix);
        }

        await _factory.DisposeAsync();
    }

    private ApplicationDbContext NewDb(out IServiceScope scope)
    {
        scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    private static async Task<ulong> AnyActiveCampusAsync(ApplicationDbContext db)
        => await db.Campuses.Where(c => c.Status == EntityStatuses.Active)
            .OrderBy(c => c.CampusId).Select(c => c.CampusId).FirstAsync();

    /// <summary>
    /// The exact shape that used to break: a user pointing at a department the cleanup is about to delete.
    /// Cleanup must complete, the department must be gone, and the user must survive — re-pointed at a
    /// seed department, never deleted and never left dangling.
    /// </summary>
    [Fact]
    public async Task Cleanup_detaches_an_attached_user_instead_of_failing_the_foreign_key()
    {
        using var scope = NewDb(out var s1);
        var db = scope;

        var campusId = await AnyActiveCampusAsync(db);
        var departmentId = await DatabaseResetHelper.CreateTestDepartmentAsync(
            db, $"{GroupAPrefix}attached {Guid.NewGuid():N}", campusId, "GENERAL", EntityStatuses.Active);

        // Bind a real test user to it, reproducing what the unordered lookup used to do by accident.
        var userId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Department);
        var user = await db.Users.FirstAsync(u => u.UserId == userId);
        var originalDepartmentId = user.DepartmentId;
        user.DepartmentId = departmentId;
        await db.SaveChangesAsync();

        await DatabaseResetHelper.DeleteTestDepartmentsAsync(db, GroupAPrefix);

        Assert.False(await db.Departments.AnyAsync(d => d.DepartmentId == departmentId));

        var survivor = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        Assert.NotNull(survivor);
        Assert.NotNull(survivor!.DepartmentId);
        Assert.NotEqual(departmentId, survivor.DepartmentId!.Value);

        // Re-pointed at a real seed department of the same campus and type — the DB trigger requires the
        // type to match the role, so landing anywhere else would be a different kind of breakage.
        var replacement = await db.Departments.AsNoTracking()
            .FirstAsync(d => d.DepartmentId == survivor.DepartmentId!.Value);
        Assert.Equal(campusId, replacement.CampusId);
        Assert.Equal("GENERAL", replacement.DepartmentType);
        Assert.DoesNotContain(DatabaseResetHelper.TestDataNamePrefix, replacement.Name, StringComparison.Ordinal);

        if (originalDepartmentId is not null)
        {
            var reattach = await db.Users.FirstAsync(u => u.UserId == userId);
            reattach.DepartmentId = originalDepartmentId;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Cleaning one prefix must not touch another class's departments.</summary>
    [Fact]
    public async Task Cleanup_of_one_prefix_leaves_another_groups_data_intact()
    {
        using var scope = NewDb(out var s2);
        var db = scope;

        var campusId = await AnyActiveCampusAsync(db);
        var groupA = await DatabaseResetHelper.CreateTestDepartmentAsync(
            db, $"{GroupAPrefix}a {Guid.NewGuid():N}", campusId, "GENERAL", EntityStatuses.Active);
        var groupB = await DatabaseResetHelper.CreateTestDepartmentAsync(
            db, $"{GroupBPrefix}b {Guid.NewGuid():N}", campusId, "GENERAL", EntityStatuses.Active);

        await DatabaseResetHelper.DeleteTestDepartmentsAsync(db, GroupAPrefix);

        Assert.False(await db.Departments.AnyAsync(d => d.DepartmentId == groupA));
        Assert.True(await db.Departments.AnyAsync(d => d.DepartmentId == groupB));
    }

    /// <summary>
    /// The source of the original collision: a shared test user must never be bound to a department some
    /// class is going to delete. Seeding ACTIVE departments of the same campus and type under a test
    /// prefix must not change which department the helper chooses.
    ///
    /// Only GENERAL decoys are seeded — <c>trg_departments_*</c> allows a campus just one ACTIVE IC
    /// department, so an IC test department cannot coexist with the seed one and the IC branch of the
    /// lookup has nothing to collide with. GENERAL is exactly where the real failure happened.
    /// </summary>
    [Fact]
    public async Task Test_users_are_never_attached_to_a_test_created_department()
    {
        using var scope = NewDb(out var s3);
        var db = scope;

        var campusId = await AnyActiveCampusAsync(db);
        await DatabaseResetHelper.CreateTestDepartmentAsync(
            db, $"{GroupAPrefix}decoy-1 {Guid.NewGuid():N}", campusId, "GENERAL", EntityStatuses.Active);
        await DatabaseResetHelper.CreateTestDepartmentAsync(
            db, $"{GroupAPrefix}decoy-2 {Guid.NewGuid():N}", campusId, "GENERAL", EntityStatuses.Active);

        foreach (var role in new[] { EffectiveRole.Department, EffectiveRole.DepartmentLead, EffectiveRole.Staff })
        {
            var userId = await DatabaseResetHelper.EnsureTestUserAsync(db, role);
            var department = await db.Users.AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => u.Department!.Name)
                .FirstAsync();

            Assert.DoesNotContain(DatabaseResetHelper.TestDataNamePrefix, department, StringComparison.Ordinal);
        }

        await DatabaseResetHelper.DeleteTestDepartmentsAsync(db, GroupAPrefix);
    }
}
