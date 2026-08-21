using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Documents.Queries.ViewDocumentDetail;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Documents;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 3D — the visit owner-context block of the document detail screen.
///
/// A document may be owned by a whole visit request or by one campus instance, and the handler resolves
/// each differently. The schedule is the part that matters here: has_mixed_campus_details compares form
/// content and member sets only — never campus_id, never the schedule — so a request whose campuses run on
/// different days is still "not mixed". Reading one arbitrary campus's dates through an unordered
/// FirstOrDefault() therefore reported a real campus's real dates as if they were the request's.
/// </summary>
public sealed class DocumentVisitOwnerContextV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong CampusHn = 1;
    private const ulong CampusHcm = 2;
    private const ulong CampusDn = 3;

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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(ulong id, string roleCode, string? subRole = null, ulong? campusId = null)
        {
            UserId = id; RoleCode = roleCode; SubRole = subRole; PrimaryCampusId = campusId;
        }
        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; }
        public string? SubRole { get; }
        public ulong? PrimaryCampusId { get; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private sealed class SilentNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType, string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static CampusVisitFormDto Campus(string code, DateTime start, string delegationName)
        => new(code, start, start.AddMinutes(120), delegationName, "MEETING", null,
            $"Mục đích {delegationName}", $"Nội dung {delegationName}",
            new List<VisitorDto> { new($"Khách {delegationName}", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "Trưởng phòng Hợp tác", "+8410", "op@example.com"),
            "VI", null, "DECLINED", null, null);

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var actor = new FakeUser(Registrant, RoleCodes.Visitor);
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(), new VisitRequestV2CreateService(db),
            new SilentNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "DOC" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private static async Task<(ulong DocumentId, ulong FileId)> AddDocumentAsync(string ownerType, ulong ownerId)
    {
        using var db = NewContext();
        var file = new UploadedFile
        {
            StorageProvider = "LOCAL",
            ObjectKey = $"it/{Guid.NewGuid():N}.pdf",
            OriginalFilename = "ke-hoach.pdf",
            MimeType = "application/pdf",
            FileSize = 1024,
            UploadedBy = Registrant,
            UploadedAt = Now,
        };
        db.Files.Add(file);
        await db.SaveChangesAsync();

        var document = new Document
        {
            FileId = file.FileId,
            OwnerType = ownerType,
            OwnerId = ownerId,
            Title = "[IT] Kế hoạch tiếp đoàn",
            Status = "DRAFT",
            CreatedAt = Now,
            CreatedBy = Registrant,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return (document.DocumentId, file.FileId);
    }

    private static async Task<(DateTime? Start, DateTime? End, string? Title)> OwnerScheduleAsync(ulong documentId)
    {
        using var db = NewContext();
        var handler = new ViewDocumentDetailQueryHandler(db, new FakeUser(Registrant, RoleCodes.Ho));
        var dto = await handler.Handle(new ViewDocumentDetailQuery { DocumentId = documentId }, CancellationToken.None);
        Assert.NotNull(dto.OwnerContext);

        // OwnerContext is an anonymous type; read it the way a serializer would.
        var t = dto.OwnerContext!.GetType();
        return (
            (DateTime?)t.GetProperty("ExpectedStartDate")!.GetValue(dto.OwnerContext),
            (DateTime?)t.GetProperty("ExpectedEndDate")!.GetValue(dto.OwnerContext),
            (string?)t.GetProperty("VisitTitle")!.GetValue(dto.OwnerContext));
    }

    private static async Task CleanupAsync(ulong requestId, List<(ulong DocumentId, ulong FileId)> documents)
    {
        using var db = NewContext();
        foreach (var (documentId, fileId) in documents)
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM documents WHERE document_id = {0}", documentId);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM files WHERE file_id = {0}", fileId);
        }
        if (requestId == 0) return;
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM email_action_tokens WHERE target_type='VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    [Fact]
    public async Task A_request_level_document_reports_the_span_of_every_campus_not_one_of_them()
    {
        RequireDb();
        ulong requestId = 0;
        var documents = new List<(ulong, ulong)>();
        try
        {
            // Same content everywhere → NOT mixed. The schedules still differ, three days apart, which is
            // exactly the case the mixed flag is not designed to catch.
            var start = Now.AddDays(40).Date.AddHours(9);
            requestId = await CreateAsync(
                Campus("HCM", start.AddDays(2), "Đoàn đồng nhất"),   // submitted first, latest date
                Campus("HN", start, "Đoàn đồng nhất"),               // earliest date
                Campus("DN", start.AddDays(1), "Đoàn đồng nhất"));

            Dictionary<ulong, (DateTime Start, DateTime End)> byCampus;
            using (var db = NewContext())
            {
                var rows = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId)
                    .Select(c => new { c.CampusId, c.PlannedStartAt, c.PlannedEndAt }).ToListAsync();
                byCampus = rows.ToDictionary(r => r.CampusId, r => (r.PlannedStartAt, r.PlannedEndAt));
                Assert.False(await db.VisitRequests.AsNoTracking()
                    .Where(v => v.VisitRequestId == requestId)
                    .Select(v => v.HasMixedCampusDetails).SingleAsync());
            }

            var doc = await AddDocumentAsync("VISIT", requestId);
            documents.Add(doc);

            var (reportedStart, reportedEnd, title) = await OwnerScheduleAsync(doc.DocumentId);

            // The span: earliest start of any campus, latest end of any campus.
            Assert.Equal(byCampus.Values.Min(v => v.Start), reportedStart);
            Assert.Equal(byCampus.Values.Max(v => v.End), reportedEnd);

            // And it is genuinely a span, not one campus's row wearing the request's name.
            Assert.NotEqual(byCampus[CampusHcm].Start, reportedStart);
            Assert.NotEqual(byCampus[CampusDn].Start, reportedStart);
            Assert.NotEqual(byCampus[CampusHn].End, reportedEnd);
            Assert.Equal("Đoàn đồng nhất", title); // uniform content still names itself
        }
        finally { await CleanupAsync(requestId, documents.Select(d => (d.Item1, d.Item2)).ToList()); }
    }

    [Fact]
    public async Task An_instance_level_document_reports_that_instances_own_dates()
    {
        RequireDb();
        ulong requestId = 0;
        var documents = new List<(ulong, ulong)>();
        try
        {
            var start = Now.AddDays(41).Date.AddHours(9);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn HN riêng"),
                Campus("HCM", start.AddDays(5), "Đoàn HCM riêng"));

            Dictionary<ulong, (ulong InstanceId, DateTime Start, DateTime End)> byCampus;
            using (var db = NewContext())
            {
                var rows = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId)
                    .Select(c => new { c.CampusId, c.VisitInstanceId, c.PlannedStartAt, c.PlannedEndAt }).ToListAsync();
                byCampus = rows.ToDictionary(r => r.CampusId, r => (r.VisitInstanceId, r.PlannedStartAt, r.PlannedEndAt));
            }

            // A document owned by the LATER campus must not borrow the earlier one's start.
            var doc = await AddDocumentAsync("VISIT", byCampus[CampusHcm].InstanceId);
            documents.Add(doc);

            var (reportedStart, reportedEnd, _) = await OwnerScheduleAsync(doc.DocumentId);
            Assert.Equal(byCampus[CampusHcm].Start, reportedStart);
            Assert.Equal(byCampus[CampusHcm].End, reportedEnd);
            Assert.NotEqual(byCampus[CampusHn].Start, reportedStart);
        }
        finally { await CleanupAsync(requestId, documents.Select(d => (d.Item1, d.Item2)).ToList()); }
    }

    [Fact]
    public async Task A_mixed_request_level_document_is_labelled_rather_than_named_after_a_campus()
    {
        RequireDb();
        ulong requestId = 0;
        var documents = new List<(ulong, ulong)>();
        try
        {
            var start = Now.AddDays(42).Date.AddHours(9);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn HN"),
                Campus("HCM", start.AddDays(1), "Đoàn HCM"));

            var doc = await AddDocumentAsync("VISIT", requestId);
            documents.Add(doc);

            var (_, _, title) = await OwnerScheduleAsync(doc.DocumentId);
            Assert.Equal("Khác nhau theo cơ sở", title);
            Assert.NotEqual("Đoàn HN", title);
            Assert.NotEqual("Đoàn HCM", title);
        }
        finally { await CleanupAsync(requestId, documents.Select(d => (d.Item1, d.Item2)).ToList()); }
    }
}
