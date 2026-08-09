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
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Commands.ConfirmFaceTags;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Documents;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 5A — face-tag confirmation is anti-IDOR by instance.
///
/// A face detected on a photo of one campus instance may only be tagged to a guest that belongs to THAT
/// exact instance. The handler re-derives the guest set from visit_instance_guest_members for the scan's
/// own instance and trusts nothing the client sends, so a guest from a sibling campus of the same request,
/// or from an entirely different request, must be refused — never silently tagged. Confirmation is also
/// one-shot (row-version + status guarded), so a replay cannot double-write tags.
///
/// The Vision provider is not involved here: this command is pure persistence over an already-SUCCEEDED
/// scan, so the scan and its detections are seeded directly and no external API is called.
/// </summary>
public sealed class ConfirmFaceTagsScopeV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
    private const ulong LeaderHcm = 9;
    private const ulong HostHn = 101;      // IC Staff HN — host on the HN instance, so has media scope
    private const ulong HostHcm = 103;     // IC Staff HCM — host on the HCM instance
    private const ulong StudentUploader = 152; // ACTIVE Student — a DB trigger requires the photo uploader
                                               // to be an accepted Student, so photos are seeded as theirs
    private const ulong Stranger = 202;    // a user with no relation to the request
    private const ulong CampusHn = 1;
    private const ulong CampusHcm = 2;
    private const ulong ApiConfigId = 1;   // seeded GOOGLE_DRIVE_STORAGE config (FK target)

    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;
    private static long _seq = DateTime.Now.Ticks;
    private static ulong NextId() => (ulong)System.Threading.Interlocked.Increment(ref _seq);

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
        { UserId = id; RoleCode = roleCode; SubRole = subRole; PrimaryCampusId = campusId; }
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
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong u, string t, string? m, string n, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest r, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static CampusVisitFormDto Campus(string code, DateTime start, string delegationName, string guestName)
        => new(code, start, start.AddMinutes(120), delegationName, "MEETING", null,
            $"Mục đích {delegationName}", $"Nội dung {delegationName}",
            new List<VisitorDto> { new(guestName, "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            // The contact is the REGISTRANT'S own address, so the campus self-matches at submit: confirmed
            // with no invitation, and the request is past the confirmation gate from the start. This suite
            // does not test that gate, and a campus behind it can be neither decided nor moved forward.
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "VI", null, "AGREED", null, null);

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
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "FT" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId, ulong leaderId, ulong campusId, ulong hostId)
    {
        using var db = NewContext();
        var actor = new FakeUser(leaderId, RoleCodes.Staff, UserSubRoles.Leader, campusId);
        // Approving states the revision it was decided on. The command requires it, so a fixture
        // that left it out would be exercising a call shape no caller can make any more.
        var rowVersion = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.RowVersion).SingleAsync();
        await new ApproveCampusInstanceCommandHandler(
                db, actor, new FixedClock(), 
                new CampusApprovalExecutor(
                    db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db), new SilentNotifications(),
                    new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()),
                    NullLogger<CampusApprovalExecutor>.Instance))
            .Handle(new ApproveCampusInstanceCommand(requestId, instanceId, hostId, null, rowVersion), CancellationToken.None);
    }

    private static async Task<Dictionary<ulong, ulong>> InstanceIdsAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .ToDictionaryAsync(c => c.CampusId, c => c.VisitInstanceId);
    }

    private static async Task<ulong> GuestIdAsync(ulong instanceId)
    {
        using var db = NewContext();
        return await db.VisitInstanceGuestMembers.AsNoTracking()
            .Where(l => l.VisitInstanceId == instanceId)
            .Select(l => l.GuestMemberId)
            .FirstAsync();
    }

    /// <summary>Adds the Student uploader as an ACCEPTED participant so the visit_photos trigger allows
    /// their upload (the uploader must be an active Student who accepted this instance).</summary>
    private static async Task AddStudentUploaderAsync(ulong instanceId)
    {
        using var db = NewContext();
        db.VisitParticipants.Add(new VisitParticipant
        {
            VisitInstanceId = instanceId,
            UserId = StudentUploader,
            ParticipantRole = ParticipantRoles.Student,
            IsHost = false,
            Status = ParticipantStatuses.Accepted,
            InvitedBy = LeaderHn,
            AssignedBy = LeaderHn,
            AssignedAt = Now,
            RespondedAt = Now,
            CreatedAt = Now,
            CreatedBy = LeaderHn,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Seeds a file + folder + photo + a SUCCEEDED scan with one detection on the instance.
    /// The photo uploader must be an accepted Student (DB trigger), so that participation is added first.</summary>
    private static async Task<(ulong ScanId, ulong DetectionId, uint RowVersion, ulong FileId)> SeedScanAsync(
        ulong requestId, ulong instanceId)
    {
        await AddStudentUploaderAsync(instanceId);
        using var db = NewContext();
        // A DB trigger requires the photo's file to be a Google Drive image with VISIT_REQUEST_PHOTO
        // purpose, so the seeded file mirrors what the real upload flow produces.
        var file = new UploadedFile
        {
            StorageProvider = "GOOGLE_DRIVE", ObjectKey = $"it/{Guid.NewGuid():N}.jpg",
            ExternalFileId = $"gd-{Guid.NewGuid():N}",
            OriginalFilename = "anh.jpg", MimeType = "image/jpeg", FileSize = 2048,
            FilePurpose = "VISIT_REQUEST_PHOTO",
            UploadedBy = StudentUploader, UploadedAt = Now,
        };
        db.Files.Add(file);
        await db.SaveChangesAsync();

        var folder = new VisitPhotoFolder
        {
            VisitRequestId = requestId, StorageProvider = "GOOGLE_DRIVE",
            ExternalFolderId = $"drv-{NextId()}", FolderName = $"VR-{requestId}",
            Status = "ACTIVE", CreatedAt = Now, CreatedBy = StudentUploader,
        };
        db.VisitPhotoFolders.Add(folder);
        await db.SaveChangesAsync();

        var photo = new VisitPhoto
        {
            VisitRequestId = requestId, VisitInstanceId = instanceId,
            VisitPhotoFolderId = folder.VisitPhotoFolderId, FileId = file.FileId,
            Status = "ACTIVE", UploadedBy = StudentUploader, UploadedAt = Now,
        };
        db.VisitPhotos.Add(photo);
        await db.SaveChangesAsync();

        var scan = new VisitPhotoFaceScan
        {
            VisitPhotoId = photo.VisitPhotoId, VisitRequestId = requestId, VisitInstanceId = instanceId,
            FileId = file.FileId, ApiConfigId = ApiConfigId, Status = VisitPhotoFaceScan.StatusSucceeded,
            ProviderName = "GOOGLE_CLOUD_VISION", FeatureType = "FACE_DETECTION",
            DetectedFaceCount = 1, RequestedAt = Now, RequestedBy = HostHn, CompletedAt = Now,
            RowVersion = 0, CreatedAt = Now,
        };
        db.VisitPhotoFaceScans.Add(scan);
        await db.SaveChangesAsync();

        var detection = new VisitPhotoFaceDetection
        {
            FaceScanId = scan.FaceScanId, VisitRequestId = requestId, VisitInstanceId = instanceId,
            FileId = file.FileId, FaceIndex = 1,
            BoundingBoxX = 0.1m, BoundingBoxY = 0.1m, BoundingBoxWidth = 0.2m, BoundingBoxHeight = 0.2m,
            DetectionConfidence = 0.99m, ReviewStatus = VisitPhotoFaceDetection.ReviewStatusDetected,
            CreatedAt = Now,
        };
        db.VisitPhotoFaceDetections.Add(detection);
        await db.SaveChangesAsync();

        return (scan.FaceScanId, detection.FaceDetectionId, scan.RowVersion, file.FileId);
    }

    private static ConfirmFaceTagsCommandHandler Handler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new FixedClock());

    private static ConfirmFaceTagsCommand TagCmd(ulong scanId, uint rowVersion, ulong detectionId, ulong? guestId)
        => new()
        {
            FaceScanId = scanId,
            RowVersion = rowVersion,
            Faces = new List<ConfirmFaceTagItem>
            {
                new() { FaceDetectionId = detectionId, GuestMemberId = guestId, Ignored = guestId is null },
            },
        };

    private static async Task CleanupAsync(params ulong[] requestIds)
    {
        using var db = NewContext();
        foreach (var id in requestIds)
        {
            if (id == 0) continue;
            async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
            // Detections FK to photo_face_tags (face_tag_id), so the child rows go first.
            await Del("DELETE FROM visit_photo_face_detections WHERE visit_request_id = {0}");
            await Del("DELETE FROM photo_face_tags WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_photo_face_scans WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_photos WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_photo_folders WHERE visit_request_id = {0}");
            await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
            await Del("DELETE FROM email_action_tokens WHERE target_type='VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
            await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
            await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
            await Del("DELETE FROM audit_logs WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
            await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
            await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
            await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
            await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
            await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
        }
    }

    [Fact]
    public async Task Tagging_a_guest_of_this_instance_succeeds_and_a_replay_is_refused()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(70);
            requestId = await CreateAsync(Campus("HN", start, $"Đoàn HN", "Khách HN"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            var hnGuest = await GuestIdAsync(instances[CampusHn]);
            var (scanId, detectionId, rowVersion, _) = await SeedScanAsync(requestId, instances[CampusHn]);

            using (var db = NewContext())
            {
                var dto = await Handler(db, HostHn).Handle(
                    TagCmd(scanId, rowVersion, detectionId, hnGuest), CancellationToken.None);
                Assert.Equal(VisitPhotoFaceScan.StatusConfirmed, dto.Status);
            }

            using (var db = NewContext())
            {
                var detection = await db.VisitPhotoFaceDetections.AsNoTracking().SingleAsync(d => d.FaceDetectionId == detectionId);
                Assert.Equal(VisitPhotoFaceDetection.ReviewStatusConfirmed, detection.ReviewStatus);
                Assert.Equal(hnGuest, detection.GuestMemberId);
                var tag = Assert.Single(await db.PhotoFaceTags.AsNoTracking().Where(t => t.VisitRequestId == requestId).ToListAsync());
                Assert.Equal(hnGuest, tag.GuestMemberId);
            }

            // Replay: the scan is CONFIRMED and its row version moved, so a second confirm is a 409, not a
            // second tag. Assert both the guard and that no duplicate tag was written.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Handler(db, HostHn).Handle(TagCmd(scanId, rowVersion, detectionId, hnGuest), CancellationToken.None));
                Assert.Contains(ex.ErrorCode, new[] { FaceScanErrorCodes.ScanAlreadyConfirmed, FaceScanErrorCodes.RowVersionMismatch });
            }
            using (var db = NewContext())
                Assert.Single(await db.PhotoFaceTags.AsNoTracking().Where(t => t.VisitRequestId == requestId).ToListAsync());
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_sibling_campus_guest_or_a_foreign_request_guest_cannot_be_tagged()
    {
        RequireDb();
        ulong requestId = 0;
        ulong foreignRequestId = 0;
        try
        {
            var start = Now.AddDays(71);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn HN", "Khách HN"),
                Campus("HCM", start.AddDays(1), "Đoàn HCM", "Khách HCM"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, HostHcm);
            var hcmGuest = await GuestIdAsync(instances[CampusHcm]); // sibling campus of the SAME request

            foreignRequestId = await CreateAsync(Campus("HN", start.AddDays(2), "Đoàn khác", "Khách đoàn khác"));
            var foreignInstances = await InstanceIdsAsync(foreignRequestId);
            await ApproveAsync(foreignRequestId, foreignInstances[CampusHn], LeaderHn, CampusHn, HostHn);
            var foreignGuest = await GuestIdAsync(foreignInstances[CampusHn]);

            // The scan is on the HN instance of `requestId`.
            var (scanId, detectionId, rowVersion, _) = await SeedScanAsync(requestId, instances[CampusHn]);

            // Neither the sibling-campus guest nor the foreign-request guest may be tagged here.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Handler(db, HostHn).Handle(TagCmd(scanId, rowVersion, detectionId, hcmGuest), CancellationToken.None));
                Assert.Equal(FaceScanErrorCodes.GuestNotInInstance, ex.ErrorCode);
            }
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Handler(db, HostHn).Handle(TagCmd(scanId, rowVersion, detectionId, foreignGuest), CancellationToken.None));
                Assert.Equal(FaceScanErrorCodes.GuestNotInInstance, ex.ErrorCode);
            }

            // Nothing was written, the scan is still confirmable, and no tag leaked onto either request.
            using (var db = NewContext())
            {
                var scan = await db.VisitPhotoFaceScans.AsNoTracking().SingleAsync(s => s.FaceScanId == scanId);
                Assert.Equal(VisitPhotoFaceScan.StatusSucceeded, scan.Status);
                Assert.Empty(await db.PhotoFaceTags.AsNoTracking()
                    .Where(t => t.VisitRequestId == requestId || t.VisitRequestId == foreignRequestId).ToListAsync());
            }
        }
        finally { await CleanupAsync(requestId, foreignRequestId); }
    }

    [Fact]
    public async Task A_user_with_no_relation_to_the_instance_cannot_confirm()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(72), "Đoàn HN", "Khách HN"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            var hnGuest = await GuestIdAsync(instances[CampusHn]);
            var (scanId, detectionId, rowVersion, _) = await SeedScanAsync(requestId, instances[CampusHn]);

            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Handler(db, Stranger).Handle(TagCmd(scanId, rowVersion, detectionId, hnGuest), CancellationToken.None));

            using (var db = NewContext())
            {
                var scan = await db.VisitPhotoFaceScans.AsNoTracking().SingleAsync(s => s.FaceScanId == scanId);
                Assert.Equal(VisitPhotoFaceScan.StatusSucceeded, scan.Status); // untouched
                Assert.Empty(await db.PhotoFaceTags.AsNoTracking().Where(t => t.VisitRequestId == requestId).ToListAsync());
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
