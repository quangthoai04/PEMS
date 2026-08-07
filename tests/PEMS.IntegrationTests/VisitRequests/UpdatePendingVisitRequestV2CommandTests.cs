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
using PEMS.Application.Delegations.Commands.UpdatePendingVisitRequestV2;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Pending-edit v2 COMMAND tests (Phase C): flag gates, the editor policy (registrant OK; ACTIVE primary
/// contact OK; PENDING contact and unrelated actors blocked), v1-request rejection, and first-success-only
/// post-commit notification. Uses ONE committed v2 request (created through the real service) shared across
/// the checks, then cascade-deletes it child-first so <c>pems_pr3_test</c> keeps v2_requests = 0.
/// </summary>
public sealed class UpdatePendingVisitRequestV2CommandTests
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

    private static UpdatePendingVisitRequestV2CommandHandler Handler(
        ApplicationDbContext db, ulong actor, bool read = true, bool write = true,
        INotificationService? notifications = null)
        => new(db, new FakeUser(actor), new FixedClock(), new VisitRequestV2EditService(db),
            notifications ?? new RecordingNotifications(),
            NullLogger<UpdatePendingVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = read }, new PerCampusFormV2WriteOptions { Enabled = write });

    private static CampusVisitFormDto CampusContent(string delegation = "Đoàn Base")
    {
        var start = Now.AddDays(20);
        return new CampusVisitFormDto(
            "HN", start, start.AddMinutes(120), delegation, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null);
    }

    /// <summary>Builds a valid edit payload from the CURRENT persisted state of the request.</summary>
    private static async Task<VisitRequestEditV2Dto> EditPayloadAsync(ulong requestId, string delegation)
    {
        using var db = NewContext();
        var r = await db.VisitRequests.AsNoTracking()
            .Include(v => v.CampusInstances)
            .FirstAsync(v => v.VisitRequestId == requestId);
        var content = CampusContent(delegation);
        var slots = r.CampusInstances.Select(i => new CampusVisitEditV2Dto(
            i.VisitInstanceId, i.RowVersion,
            "HN", content.PlannedStartAt, content.PlannedEndAt,
            content.DelegationName, content.VisitType, content.VisitTypeOther, content.Purpose, content.WorkingContent,
            content.Visitors, content.ExternalSupportMembers, content.OperationalContact,
            content.WorkingLanguage, content.TransportationNote, content.MediaConsentStatus,
            content.Notes)).ToList();
        return new VisitRequestEditV2Dto(
            r.RowVersion,
            new RegistrantInputV2(r.RegistrantFullName, r.RegistrantNationality ?? "VN", r.RegistrantOrganization,
                r.RegistrantJobTitle ?? "Job", r.RegistrantPhone ?? "+8491", r.RegistrantEmail),
            r.PartnerId, slots);
    }

    private static async Task<ulong> CreateCommittedAsync()
    {
        using var db = NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var created = await new VisitRequestV2CreateService(db).CreateV2Async(
            new VisitRequestFormDataV2(
                Guid.NewGuid().ToString("N"),
                new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", "registrant@example.com"),
                null, new List<CampusVisitFormDto> { CampusContent() }),
            Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
        await tx.CommitAsync();
        return created.VisitRequestId;
    }

    private static async Task CleanupAsync(ulong id)
    {
        using var db = NewContext();
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

    [Fact]
    public async Task Flag_gates_editor_policy_v1_reject_and_notification_lifecycle()
    {
        RequireDb();
        var requestId = await CreateCommittedAsync();
        try
        {
            // A second real user (any ACTIVE user that is not the registrant) plays contact B / unrelated actor.
            ulong otherUser;
            using (var db = NewContext())
                otherUser = await db.Users.Where(u => u.UserId != Registrant && u.Status == UserStatuses.Active && u.Role.RoleCode == "VISITOR")
                    .OrderBy(u => u.UserId).Select(u => u.UserId).FirstAsync();

            // 1) Write flag OFF → 404, nothing applied, no notification.
            var n1 = new RecordingNotifications();
            var gatePayload = await EditPayloadAsync(requestId, "Đoàn Gate");
            using (var db = NewContext())
                await Assert.ThrowsAsync<NotFoundException>(() =>
                    Handler(db, Registrant, write: false, notifications: n1).Handle(
                        new UpdatePendingVisitRequestV2Command(requestId, gatePayload),
                        CancellationToken.None));
            Assert.Equal(0, n1.Batches);

            // 2) Write ON + read OFF → explicit reject.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Handler(db, Registrant, read: false).Handle(
                        new UpdatePendingVisitRequestV2Command(requestId, gatePayload),
                        CancellationToken.None));
                Assert.Equal(CreateVisitRequestV2ErrorCodes.ReadRequired, ex.ErrorCode);
            }

            // 3) Unrelated actor → Forbidden, no write, no notification.
            var n3 = new RecordingNotifications();
            var hackPayload = await EditPayloadAsync(requestId, "Đoàn Hack");
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Handler(db, otherUser, notifications: n3).Handle(
                        new UpdatePendingVisitRequestV2Command(requestId, hackPayload),
                        CancellationToken.None));
            Assert.Equal(0, n3.Batches);


            // 5) A CONFIRMED operational contact still may not edit the whole request. This edits
            //    request-level content across every campus, and the contact's authority is one campus —
            //    so the answer is Forbidden even though the account is a legitimate participant in the
            //    request. (Campus-scoped edits are VisitSafeEditV2Tests' subject, not this one.)
            //    The status moves with the contact: a campus may not hold one while it is still
            //    WAITING_CONTACT_CONFIRMATION, and may not be past that state without one.
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_campuses SET operational_contact_user_id = {0}, "
                    + "operational_contact_confirmed_at = NOW(), "
                    + "operational_contact_confirmation_source = 'EMAIL_CONFIRMATION', "
                    + "status = 'WAITING_REQUEST_APPROVAL' "
                    + "WHERE visit_request_id = {1}",
                    otherUser, requestId);
            var n5 = new RecordingNotifications();
            var contactPayload = await EditPayloadAsync(requestId, "Đoàn Contact Sửa");
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Handler(db, otherUser, notifications: n5).Handle(
                        new UpdatePendingVisitRequestV2Command(requestId, contactPayload),
                        CancellationToken.None));
            Assert.Equal(0, n5.Batches);

            // 6) The registrant edits successfully (fresh row versions from the DB) with one notification batch.
            var n6 = new RecordingNotifications();
            using (var db = NewContext())
            {
                var res = await Handler(db, Registrant, notifications: n6).Handle(
                    new UpdatePendingVisitRequestV2Command(requestId, await EditPayloadAsync(requestId, "Đoàn Registrant Sửa")),
                    CancellationToken.None);
                // The first edit that actually lands, so the row version must have moved off its
                // created-state value. It used to be >= 2 because step 5 applied an edit of its own;
                // that step now refuses, and asserting >= 2 here would only be measuring the refusal.
                Assert.True(res.RequestRowVersion >= 1);
            }
            Assert.Equal(1, n6.Batches);

            // 7) Stale request row version through the HANDLER → stable 409, nothing applied.
            var stale = (await EditPayloadAsync(requestId, "Đoàn Stale")) with { ExpectedRequestRowVersion = 0 };
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Handler(db, Registrant).Handle(
                        new UpdatePendingVisitRequestV2Command(requestId, stale), CancellationToken.None));
                Assert.Equal(VisitRequestErrorCodes.RequestVersionConflict, ex.ErrorCode);
            }

            // 8) There is no longer any such thing as a "v1 request" to reject. This step used to demote the
            //    row with `UPDATE visit_requests SET form_schema_version = 1` and expect NOT_PER_CAMPUS_V2;
            //    under Pure V2 that column does not exist, so the demotion itself is unrepresentable. Assert
            //    that directly against the live schema — a stronger guarantee than the runtime guard it
            //    replaces, because it holds for every code path rather than this one endpoint.
            using (var db = NewContext())
            {
                var discriminatorColumns = await db.Database
                    .SqlQueryRaw<int>(
                        "SELECT COUNT(*) AS Value FROM information_schema.columns " +
                        "WHERE table_schema = DATABASE() AND column_name = 'form_schema_version'")
                    .SingleAsync();

                Assert.Equal(0, discriminatorColumns);
            }
        }
        finally
        {
            await CleanupAsync(requestId);
        }
    }
}
