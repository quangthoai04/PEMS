using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequestV2;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Resubmit-v2 COMMAND tests: the flag gates, editor policy and the all-rejected pre-check all fire BEFORE any
/// write, so one committed PENDING request (child-first cleanup) covers them without ever mutating state. The
/// full apply semantics live in <see cref="ResubmitRejectedVisitRequestV2ServiceTests"/>; failed handler calls
/// must dispatch NO notification.
/// </summary>
public sealed class ResubmitRejectedVisitRequestV2CommandTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");
    private const ulong Registrant = 8;
    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;

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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master to run these tests.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(ulong userId) => UserId = userId;
        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode => RoleCodes.Visitor;
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private sealed class RecordingNotifications : INotificationService
    {
        public int Batches { get; private set; }
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct)
        {
            Batches++;
            return Task.CompletedTask;
        }
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType,
            string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    private static ResubmitRejectedVisitRequestV2CommandHandler Handler(
        ApplicationDbContext db, ulong actor, bool read = true, bool write = true,
        INotificationService? notifications = null)
        => new(db, new FakeUser(actor), new FixedClock(), new VisitRequestV2EditService(db),
            notifications ?? new RecordingNotifications(),
            NullLogger<ResubmitRejectedVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = read }, new PerCampusFormV2WriteOptions { Enabled = write });

    private static CampusVisitFormDto CampusContent()
    {
        var start = Now.AddDays(20);
        return new CampusVisitFormDto(
            "HN", start, start.AddMinutes(120), "Đoàn Resubmit", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            // The registrant's own address, so the campus self-matches at submit and the request opens the
            // confirmation gate immediately — this suite is about the resubmit gates, not that one.
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "EN", null, "DECLINED", null, null);
    }

    private static async Task<VisitRequestEditV2Dto> PayloadAsync(ulong requestId)
    {
        using var db = NewContext();
        var r = await db.VisitRequests.AsNoTracking()
            .Include(v => v.CampusInstances)
            .FirstAsync(v => v.VisitRequestId == requestId);
        var content = CampusContent();
        var slots = r.CampusInstances.Select(i => new CampusVisitEditV2Dto(
            i.VisitInstanceId, i.RowVersion,
            "HN", content.PlannedStartAt, content.PlannedEndAt,
            content.DelegationName, content.VisitType, content.VisitTypeOther, content.Purpose, content.WorkingContent,
            content.Visitors, content.ExternalSupportMembers, content.OperationalContact,
            content.WorkingLanguage, content.TransportationNote, content.MediaConsentStatus,
            content.MediaConsentNote)).ToList();
        return new VisitRequestEditV2Dto(
            r.RowVersion,
            new RegistrantInputV2(r.RegistrantFullName, r.RegistrantNationality ?? "VN", r.RegistrantOrganization,
                r.RegistrantJobTitle ?? "Job", r.RegistrantPhone ?? "+8491", r.RegistrantEmail),
            r.PartnerId, slots);
    }

    [Fact]
    public async Task Gates_editor_policy_and_not_resubmittable_all_fire_before_any_write()
    {
        RequireDb();
        // One committed PENDING v2 request — every path below rejects before writing anything.
        ulong requestId;
        using (var db = NewContext())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var created = await new VisitRequestV2CreateService(db).CreateV2Async(
                new VisitRequestFormDataV2(
                    Guid.NewGuid().ToString("N"),
                    new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
                    null, new List<CampusVisitFormDto> { CampusContent() }),
                Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
            await tx.CommitAsync();
            requestId = created.VisitRequestId;
        }
        try
        {
            var payload = await PayloadAsync(requestId);
            var notifications = new RecordingNotifications();

            // Write OFF → 404.
            using (var db = NewContext())
                await Assert.ThrowsAsync<NotFoundException>(() =>
                    Handler(db, Registrant, write: false, notifications: notifications).Handle(
                        new ResubmitRejectedVisitRequestV2Command(requestId, payload), CancellationToken.None));

            // Write ON + read OFF → explicit reject.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Handler(db, Registrant, read: false, notifications: notifications).Handle(
                        new ResubmitRejectedVisitRequestV2Command(requestId, payload), CancellationToken.None));
                Assert.Equal(CreateVisitRequestV2ErrorCodes.ReadRequired, ex.ErrorCode);
            }

            // Unrelated actor → Forbidden (checked before the resubmittable gate).
            ulong otherUser;
            using (var db = NewContext())
                otherUser = await db.Users.Where(u => u.UserId != Registrant && u.Status == UserStatuses.Active)
                    .OrderBy(u => u.UserId).Select(u => u.UserId).FirstAsync();
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Handler(db, otherUser, notifications: notifications).Handle(
                        new ResubmitRejectedVisitRequestV2Command(requestId, payload), CancellationToken.None));

            // Registrant on a NOT-fully-rejected request → VISIT_REQUEST_NOT_RESUBMITTABLE.
            // ThrowsAny, not Throws: the refusal is a VisitMutationRefusedException, a
            // BusinessRuleException that also carries the campus and the deadline. The code is what
            // this test is about, and the code is unchanged.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAnyAsync<BusinessRuleException>(() =>
                    Handler(db, Registrant, notifications: notifications).Handle(
                        new ResubmitRejectedVisitRequestV2Command(requestId, payload), CancellationToken.None));
                Assert.Equal(VisitRequestErrorCodes.VisitRequestNotResubmittable, ex.ErrorCode);
            }

            // No failed path ever notified; the request is untouched.
            Assert.Equal(0, notifications.Batches);
            using (var db = NewContext())
            {
                var head = await db.VisitRequests.AsNoTracking().FirstAsync(v => v.VisitRequestId == requestId);
                Assert.Equal(VisitRequestStatuses.PendingApproval, head.Status);
                Assert.Equal(0u, head.ResubmissionCount);
                Assert.Equal(0, head.RowVersion);
            }
        }
        finally
        {
            using var db = NewContext();
            var id = requestId;
            async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
            await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
            await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
            await Del("DELETE FROM audit_log_changes WHERE audit_log_id IN (SELECT audit_log_id FROM audit_logs WHERE visit_request_id = {0})");
            await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
        }
    }
}
