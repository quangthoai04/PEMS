using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Enums;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Smoke-runs the actual patch file (not a re-implementation of it) against a real MySQL database,
/// proving it is valid SQL and behaves as documented.
///
/// <para>
/// Each test seeds its OWN instance with its OWN legacy HOST_AND_PARTICIPANTS rows — not the canonical
/// seed data's instance 3110/3005, which is shared, mutable, and (being PENDING with a fixed past
/// <c>scheduled_at</c>) a live target for any OTHER test's <c>DispatchDueAsync()</c> sweep running
/// concurrently against the same disposable database. The PENDING rows this suite creates are
/// scheduled comfortably in the future for exactly that reason: RunPatchAsync's SQL only ever filters
/// on <c>status = 'PENDING'</c>, never on due-ness, so a future schedule is enough to keep this suite's
/// own rows out of every other test's dispatch batch while still being exactly what the patch acts on.
/// </para>
/// </summary>
public sealed class VisitReminderLegacyTargetGroupPatchTests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    /// <summary>
    /// The SAME shared disposable database <see cref="ConnString"/> targets, but reached with a
    /// MySql.Data connection string — GuidFormat is Pomelo's and MySql.Data (which MySqlScript needs)
    /// rejects the key outright, and TestDatabaseTarget.ForDisposable deliberately carries Pomelo's own
    /// keys through untouched (it is built for Pomelo callers), so this is built by hand rather than
    /// reused, the same split EmailTemplateSyncScriptTests uses. Ensures the shared database exists
    /// first (via ConnString) before reading DisposableDatabaseManager.CurrentDatabaseName.
    /// </summary>
    private static string ScriptConnString
    {
        get
        {
            _ = ConnString; // ensure the shared disposable database has been created
            var dbName = TestInfrastructure.DisposableDatabaseManager.CurrentDatabaseName
                ?? throw new InvalidOperationException("Disposable database was not created.");
            return TestInfrastructure.TestDatabaseTarget.ForDisposable(
                "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True",
                dbName);
        }
    }

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static bool? _dbUp;
    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private static string PatchSql()
    {
        var path = Path.Combine(
            TestInfrastructure.CanonicalSqlScript.FindRepositoryRoot(),
            "docs", "database", "scripts", "patches",
            "2026-08-28_visit_reminder_legacy_host_and_participants_split.sql");
        Assert.True(File.Exists(path), $"Patch file not found at {path}");
        return File.ReadAllText(path);
    }

    private static Task RunPatchAsync()
    {
        using var conn = new MySqlConnection(ScriptConnString);
        conn.Open();
        new MySqlScript(conn, PatchSql()).Execute();
        return Task.CompletedTask;
    }

    /// <summary>
    /// A fresh, this-test-only BEFORE_VISIT instance with its own Host, so the seeded reminder rows'
    /// FK is satisfied without borrowing anything another test reads or writes.
    /// </summary>
    private static async Task<ulong> SeedOwnInstanceAsync(ApplicationDbContext db)
    {
        // The approving Staff Leader is READ, never written — the DB trigger requires decided_by /
        // host_assigned_by to reference an actual Staff LEADER of the SAME campus, which a freshly
        // created plain-STAFF host does not satisfy (same reason VisitReminderDispatchIdempotencyTests
        // .SeedAsync reads one instead of creating it).
        var leader = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == "STAFF" && u.SubRole == "LEADER"
                        && u.Status == "ACTIVE" && u.PrimaryCampusId != null)
            .OrderBy(u => u.UserId)
            .Select(u => new { u.UserId, u.RoleId, u.DepartmentId, CampusId = u.PrimaryCampusId!.Value })
            .FirstAsync();
        var leaderCampusId = leader.CampusId;

        var hostUser = new PEMS.Domain.Entities.Users.User
        {
            FullName = "Legacy Patch Host " + Guid.NewGuid().ToString("N")[..8],
            Email = "legacy-patch-host-" + Guid.NewGuid().ToString("N")[..8] + "@partner.example.com",
            RoleId = leader.RoleId,
            SubRole = "STAFF",
            DepartmentId = leader.DepartmentId,
            Status = "ACTIVE",
            PrimaryCampusId = leaderCampusId,
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(hostUser);

        var guestUser = new PEMS.Domain.Entities.Users.User
        {
            FullName = "Legacy Patch Guest " + Guid.NewGuid().ToString("N")[..8],
            Email = "legacy-patch-guest-" + Guid.NewGuid().ToString("N")[..8] + "@partner.example.com",
            RoleId = await db.Roles.Where(r => r.RoleCode == "VISITOR").Select(r => r.RoleId).FirstAsync(),
            Status = "ACTIVE",
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(guestUser);
        await db.SaveChangesAsync();

        var request = new VisitRequest
        {
            RequestCode = "LGP-" + Guid.NewGuid().ToString("N")[..12],
            RegistrantUserId = guestUser.UserId,
            RegistrantFullName = "Nguyễn Văn Khách",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Đối tác",
            RegistrantJobTitle = "Trưởng đoàn",
            RegistrantPhone = "0900000000",
            RegistrantEmail = "legacy-patch-guest@partner.example.com",
            Status = "APPROVED",
            SubmittedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequests.Add(request);
        await db.SaveChangesAsync();

        var instance = new VisitRequestCampus
        {
            VisitRequestId = request.VisitRequestId,
            CampusId = leaderCampusId,
            PlannedStartAt = DateTime.Now.AddDays(5),
            PlannedEndAt = DateTime.Now.AddDays(5).AddHours(2),
            Status = PEMS.Shared.VisitInstanceStatus.BeforeVisit,
            OperationalContactUserId = guestUser.UserId,
            OperationalContactConfirmedAt = DateTime.Now,
            OperationalContactConfirmationSource = "REGISTRANT_SELF_MATCH",
            CurrentHostUserId = hostUser.UserId,
            HostAssignedBy = leader.UserId,
            HostAssignedAt = DateTime.Now,
            DecidedBy = leader.UserId,
            DecidedAt = DateTime.Now,
            DecisionActorRole = "STAFF_LEADER",
            CreatedAt = DateTime.Now,
            FormDetail = new VisitInstanceFormDetail
            {
                DelegationName = "Đoàn patch legacy",
                VisitType = "MEETING",
                Purpose = "Tham quan",
                WorkingContent = "Nội dung",
                OperationalContactFullName = "Đầu mối cơ sở",
                OperationalContactOrganization = "Đối tác",
                OperationalContactJobTitle = "Trưởng phòng Hợp tác",
                OperationalContactPhone = "0900000002",
                OperationalContactEmail = "legacy-patch-op@partner.example.com",
                WorkingLanguage = "EN",
                MediaConsentStatus = "AGREED",
                FormRevision = 1,
                ApprovalRevision = 1,
                CreatedAt = DateTime.Now,
            },
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();

        return instance.VisitInstanceId;
    }

    private static async Task CleanupAsync(ulong instanceId, ulong requestId)
    {
        using var db = NewContext();
        await db.VisitInstanceReminderSettings.Where(r => r.VisitInstanceId == instanceId).ExecuteDeleteAsync();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_instance_form_details WHERE visit_instance_id = {0}", instanceId);
        var hostAndGuestIds = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId)
            .Select(c => c.CurrentHostUserId!.Value)
            .ToListAsync();
        await db.VisitRequestCampuses.Where(c => c.VisitInstanceId == instanceId).ExecuteDeleteAsync();
        await db.VisitRequests.Where(v => v.VisitRequestId == requestId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Email!.StartsWith("legacy-patch-")).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Running_the_patch_splits_a_legacy_PENDING_row_into_HOST_and_PARTICIPANTS()
    {
        RequireDb();
        ulong instanceId = 0, requestId = 0;
        try
        {
            using (var db = NewContext())
            {
                instanceId = await SeedOwnInstanceAsync(db);
                requestId = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instanceId).Select(c => c.VisitRequestId).SingleAsync();

                var future = DateTime.Now.AddDays(4); // future: never a target of another test's dispatch sweep
                db.VisitInstanceReminderSettings.AddRange(
                    new VisitInstanceReminderSetting
                    {
                        VisitInstanceId = instanceId, Channel = VisitReminderChannel.IN_APP,
                        TargetGroup = VisitReminderTargetGroup.HOST_AND_PARTICIPANTS,
                        OffsetMinutes = 60, ScheduledAt = future, Status = VisitReminderStatus.PENDING,
                        CreatedAt = DateTime.Now,
                    },
                    new VisitInstanceReminderSetting
                    {
                        VisitInstanceId = instanceId, Channel = VisitReminderChannel.EMAIL,
                        TargetGroup = VisitReminderTargetGroup.HOST_AND_PARTICIPANTS,
                        OffsetMinutes = 1440, ScheduledAt = future.AddHours(-1), Status = VisitReminderStatus.PENDING,
                        CreatedAt = DateTime.Now,
                    });
                await db.SaveChangesAsync();
            }

            await RunPatchAsync();

            using var verify = NewContext();
            var rows = await verify.VisitInstanceReminderSettings.AsNoTracking()
                .Where(r => r.VisitInstanceId == instanceId).ToListAsync();

            var legacy = rows.Where(r => r.TargetGroup == VisitReminderTargetGroup.HOST_AND_PARTICIPANTS).ToList();
            Assert.Equal(2, legacy.Count);
            Assert.All(legacy, r =>
            {
                Assert.Equal(VisitReminderStatus.CANCELLED, r.Status);
                Assert.StartsWith("LEGACY_TARGET_GROUP_SPLIT:", r.ErrorMessage);
            });

            var canonical = rows.Where(r => r.TargetGroup is VisitReminderTargetGroup.HOST or VisitReminderTargetGroup.PARTICIPANTS).ToList();
            Assert.Equal(4, canonical.Count);
            Assert.All(canonical, r => Assert.Equal(VisitReminderStatus.PENDING, r.Status));

            var inAppHost = canonical.Single(r => r.Channel == VisitReminderChannel.IN_APP && r.TargetGroup == VisitReminderTargetGroup.HOST);
            var legacyInApp = legacy.Single(r => r.Channel == VisitReminderChannel.IN_APP);
            Assert.Equal(legacyInApp.OffsetMinutes, inAppHost.OffsetMinutes);
            Assert.Equal(legacyInApp.ScheduledAt, inAppHost.ScheduledAt);

            var duplicates = rows.GroupBy(r => (r.Channel, r.TargetGroup)).Where(g => g.Count() > 1).ToList();
            Assert.Empty(duplicates);
        }
        finally { await CleanupAsync(instanceId, requestId); }
    }

    [Fact]
    public async Task Running_the_patch_never_touches_a_SENT_legacy_row()
    {
        RequireDb();
        ulong instanceId = 0, requestId = 0;
        try
        {
            using (var db = NewContext())
            {
                instanceId = await SeedOwnInstanceAsync(db);
                requestId = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instanceId).Select(c => c.VisitRequestId).SingleAsync();

                db.VisitInstanceReminderSettings.Add(new VisitInstanceReminderSetting
                {
                    VisitInstanceId = instanceId, Channel = VisitReminderChannel.IN_APP,
                    TargetGroup = VisitReminderTargetGroup.HOST_AND_PARTICIPANTS,
                    OffsetMinutes = 60, ScheduledAt = DateTime.Now.AddDays(-3), Status = VisitReminderStatus.SENT,
                    LastDispatchedAt = DateTime.Now.AddDays(-3), CreatedAt = DateTime.Now,
                });
                await db.SaveChangesAsync();
            }

            await RunPatchAsync();

            using var verify = NewContext();
            var sentLegacy = await verify.VisitInstanceReminderSettings.AsNoTracking()
                .SingleAsync(r => r.VisitInstanceId == instanceId);

            Assert.Equal(VisitReminderStatus.SENT, sentLegacy.Status);
            Assert.Equal(VisitReminderTargetGroup.HOST_AND_PARTICIPANTS, sentLegacy.TargetGroup);
            Assert.Null(sentLegacy.ErrorMessage);
        }
        finally { await CleanupAsync(instanceId, requestId); }
    }

    [Fact]
    public async Task Running_the_patch_twice_is_a_true_no_op_the_second_time()
    {
        RequireDb();
        ulong instanceId = 0, requestId = 0;
        try
        {
            using (var db = NewContext())
            {
                instanceId = await SeedOwnInstanceAsync(db);
                requestId = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instanceId).Select(c => c.VisitRequestId).SingleAsync();

                db.VisitInstanceReminderSettings.Add(new VisitInstanceReminderSetting
                {
                    VisitInstanceId = instanceId, Channel = VisitReminderChannel.IN_APP,
                    TargetGroup = VisitReminderTargetGroup.HOST_AND_PARTICIPANTS,
                    OffsetMinutes = 60, ScheduledAt = DateTime.Now.AddDays(4), Status = VisitReminderStatus.PENDING,
                    CreatedAt = DateTime.Now,
                });
                await db.SaveChangesAsync();
            }

            await RunPatchAsync();
            using var before = NewContext();
            var snapshotBefore = await before.VisitInstanceReminderSettings.AsNoTracking()
                .Where(r => r.VisitInstanceId == instanceId)
                .Select(r => new { r.ReminderSettingId, r.Status, r.OffsetMinutes, r.ScheduledAt, r.ErrorMessage })
                .OrderBy(r => r.ReminderSettingId)
                .ToListAsync();

            await RunPatchAsync();
            using var after = NewContext();
            var snapshotAfter = await after.VisitInstanceReminderSettings.AsNoTracking()
                .Where(r => r.VisitInstanceId == instanceId)
                .Select(r => new { r.ReminderSettingId, r.Status, r.OffsetMinutes, r.ScheduledAt, r.ErrorMessage })
                .OrderBy(r => r.ReminderSettingId)
                .ToListAsync();

            Assert.Equal(snapshotBefore.Count, snapshotAfter.Count);
            Assert.Equal(
                snapshotBefore.Select(s => (s.ReminderSettingId, s.Status, s.OffsetMinutes, s.ScheduledAt, s.ErrorMessage)),
                snapshotAfter.Select(s => (s.ReminderSettingId, s.Status, s.OffsetMinutes, s.ScheduledAt, s.ErrorMessage)));
        }
        finally { await CleanupAsync(instanceId, requestId); }
    }
}
