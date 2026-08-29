using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Delegations.Reminders;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Enums;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// DB-NQ-005 (docs/CanhIter3FixBug/GopYCQuyen/PEMS_Database_Optimization_Safe_Implementation_Plan.md
/// §16): <c>VisitReminderDispatchService.DispatchOneAsync</c> re-queried the reminder's campus instance
/// and campus name once PER REMINDER, even though one tick's whole batch only ever touches a handful
/// of distinct instances/campuses. <c>DispatchDueAsync</c> now batches those two read-only lookups
/// once for the tick into <c>instancesById</c>/<c>campusNamesById</c> dictionaries; the per-row
/// <c>ClaimAsync</c> and the strict Claim -&gt; Send order per reminder (the audit's explicit "PHẢI giữ
/// per-row" requirement) are untouched.
///
/// <para>
/// A raw round-trip-count comparison across two separate <c>DispatchDueAsync()</c> calls would be
/// fragile here: the job sweeps the newest 50 due reminders GLOBALLY from a database this whole test
/// suite shares, so unrelated due rows from other tests could change the count independently of this
/// fix. What genuinely matters — and what a dictionary-based batching bug would actually break — is
/// whether two DIFFERENT instances' reminders, claimed and dispatched in the SAME tick, each still get
/// their OWN instance's delegation/campus name rather than the other one's. That is what this proves.
/// </para>
/// </summary>
public sealed class VisitReminderDispatchBatchingTests
{
    private static string ConnString => DisposableDatabaseManager.GetDisposableConnectionString(
        EmailEvidenceHarness.BaseConnectionString);

    private static bool? _dbUp;

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private sealed class RecordingNotifications : INotificationService
    {
        public List<CreateNotificationRequest> Created { get; } = new();
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct)
        { Created.AddRange(requests); return Task.CompletedTask; }
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType, string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct)
        { Created.Add(request); return Task.CompletedTask; }
    }

    private sealed class FixedClock : PEMS.Application.Common.Interfaces.IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => DateTime.Now;
    }

    /// <summary>One IN_APP-only campus instance + due HOST reminder, on an existing ACTIVE Staff
    /// Leader's campus (read-only) — same shape as VisitReminderDispatchIdempotencyTests.SeedAsync,
    /// but IN_APP so no SMTP/renderer path is involved.</summary>
    private static async Task<(ulong InstanceId, ulong HostUserId, List<ulong> CreatedUserIds, ulong RequestId,
            string DelegationName, string CampusName)>
        SeedOneAsync(string tag)
    {
        using var db = NewContext();

        var leader = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == "STAFF" && u.SubRole == "LEADER"
                        && u.Status == "ACTIVE" && u.PrimaryCampusId != null)
            .OrderBy(u => u.UserId)
            .Select(u => new { u.UserId, u.RoleId, u.DepartmentId, CampusId = u.PrimaryCampusId!.Value })
            .FirstAsync();

        var campusName = await db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == leader.CampusId).Select(c => c.Name).FirstAsync();

        var hostUser = new PEMS.Domain.Entities.Users.User
        {
            FullName = $"[IT-NQ005] Host {tag}",
            Email = $"nq005-host-{tag}@pems.test",
            RoleId = leader.RoleId,
            SubRole = "STAFF",
            DepartmentId = leader.DepartmentId,
            Status = "ACTIVE",
            PrimaryCampusId = leader.CampusId,
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(hostUser);
        await db.SaveChangesAsync();

        var guestUser = new PEMS.Domain.Entities.Users.User
        {
            FullName = $"[IT-NQ005] Guest {tag}",
            Email = $"nq005-guest-{tag}@pems.test",
            RoleId = await db.Roles.Where(r => r.RoleCode == "VISITOR").Select(r => r.RoleId).FirstAsync(),
            Status = "ACTIVE",
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(guestUser);
        await db.SaveChangesAsync();

        var request = new VisitRequest
        {
            RequestCode = "NQ005-" + tag,
            RegistrantUserId = guestUser.UserId,
            RegistrantFullName = "Registrant",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Org",
            RegistrantJobTitle = "Job",
            RegistrantPhone = "0900000000",
            RegistrantEmail = guestUser.Email,
            Status = "APPROVED",
            SubmittedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequests.Add(request);
        await db.SaveChangesAsync();

        var delegationName = $"[IT-NQ005] Đoàn {tag}";
        var instance = new VisitRequestCampus
        {
            VisitRequestId = request.VisitRequestId,
            CampusId = leader.CampusId,
            PlannedStartAt = DateTime.Now.AddDays(2),
            PlannedEndAt = DateTime.Now.AddDays(2).AddHours(2),
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
                DelegationName = delegationName,
                VisitType = "MEETING",
                Purpose = "Tham quan",
                WorkingContent = "Nội dung",
                OperationalContactFullName = "Đầu mối",
                OperationalContactOrganization = "Org",
                OperationalContactJobTitle = "Trưởng phòng",
                OperationalContactPhone = "0900000002",
                OperationalContactEmail = "nq005-op@pems.test",
                WorkingLanguage = "EN",
                MediaConsentStatus = "AGREED",
                FormRevision = 1,
                ApprovalRevision = 1,
                CreatedAt = DateTime.Now,
            },
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();

        db.VisitInstanceReminderSettings.Add(new VisitInstanceReminderSetting
        {
            VisitInstanceId = instance.VisitInstanceId,
            Channel = VisitReminderChannel.IN_APP,
            TargetGroup = VisitReminderTargetGroup.HOST,
            OffsetMinutes = 1440,
            ScheduledAt = DateTime.Now.AddMinutes(-5),
            Status = VisitReminderStatus.PENDING,
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        return (instance.VisitInstanceId, hostUser.UserId,
            new List<ulong> { hostUser.UserId, guestUser.UserId }, request.VisitRequestId,
            delegationName, campusName);
    }

    private static async Task CleanupAsync(ulong requestId, ulong instanceId, IEnumerable<ulong> userIds)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_instance_reminder_settings WHERE visit_instance_id = {0}", instanceId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_instance_form_details WHERE visit_instance_id = {0}", instanceId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_request_campuses WHERE visit_instance_id = {0}", instanceId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM audit_logs WHERE visit_request_id = {0}", requestId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_requests WHERE visit_request_id = {0}", requestId);
        foreach (var userId in userIds)
            await db.Database.ExecuteSqlRawAsync("DELETE FROM users WHERE user_id = {0}", userId);
    }

    [Fact]
    public async Task Two_different_instances_dispatched_in_the_same_tick_never_swap_delegation_or_campus_names()
    {
        RequireDb();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var a = await SeedOneAsync(tag + "A");
        var b = await SeedOneAsync(tag + "B");
        try
        {
            var notifications = new RecordingNotifications();
            using var db = NewContext();
            var service = new VisitReminderDispatchService(
                db, dispatcher: null!, clock: new FixedClock(), urls: null!, notifications: notifications);
            // IN_APP never reaches the email dispatcher/URL builder, so null stand-ins for those two
            // constructor dependencies are never actually invoked on this path.

            await service.DispatchDueAsync();

            var messageA = notifications.Created.SingleOrDefault(r => r.RecipientUserId == a.HostUserId);
            var messageB = notifications.Created.SingleOrDefault(r => r.RecipientUserId == b.HostUserId);

            Assert.NotNull(messageA);
            Assert.NotNull(messageB);

            // Each Host's notification names THEIR OWN delegation/campus - the exact thing a dictionary
            // keyed wrong (or a stale/shared row) would silently swap between the two instances.
            Assert.Contains(a.DelegationName, messageA!.Message);
            Assert.Contains(a.CampusName, messageA.Message);
            Assert.DoesNotContain(b.DelegationName, messageA.Message);

            Assert.Contains(b.DelegationName, messageB!.Message);
            Assert.Contains(b.CampusName, messageB.Message);
            Assert.DoesNotContain(a.DelegationName, messageB.Message);

            using var verify = NewContext();
            var reminderA = await verify.VisitInstanceReminderSettings.AsNoTracking()
                .SingleAsync(r => r.VisitInstanceId == a.InstanceId);
            var reminderB = await verify.VisitInstanceReminderSettings.AsNoTracking()
                .SingleAsync(r => r.VisitInstanceId == b.InstanceId);
            Assert.Equal(VisitReminderStatus.SENT, reminderA.Status);
            Assert.Equal(VisitReminderStatus.SENT, reminderB.Status);
        }
        finally
        {
            await CleanupAsync(a.RequestId, a.InstanceId, a.CreatedUserIds);
            await CleanupAsync(b.RequestId, b.InstanceId, b.CreatedUserIds);
        }
    }
}
