using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Emails.Common;
using PEMS.Application.Files.Common;
using PEMS.Application.Files.Queries.GetFileContent;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.FileStorage;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// G5 security closure — who may download a stored file.
///
/// <para>
/// <c>GET /api/files/{id}/download</c> checked only that the caller was signed in, then streamed the
/// bytes. Because email attachments, unsent drafts, visit photos and partner documents all live in one
/// <c>files</c> table keyed by a small sequential integer, that made the numeric id a master key — and it
/// walked straight around the recipient rules Giai đoạn 5 had just built. These tests run the real
/// handler against a real database with real files on disk, because the interesting question is not
/// whether a policy object returns false; it is whether the bytes come back.
/// </para>
/// </summary>
public sealed class FileDownloadAuthorizationTests : IDisposable
{
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "pems-g5fix-" + Guid.NewGuid().ToString("N"));

    private const ulong Base = 991_500;
    private const ulong CampusId = Base + 1;
    private const ulong IcDeptId = Base + 2;

    private const ulong SenderA = Base + 10;
    private const ulong RecipientB = Base + 11;
    private const ulong CopiedC = Base + 12;
    private const ulong BlindD = Base + 13;
    private const ulong BlindE = Base + 14;
    private const ulong HostF = Base + 15;
    private const ulong OutsiderG = Base + 16;
    private const ulong HoH = Base + 17;
    private const ulong StaffLeaderI = Base + 18;

    /// <summary>
    /// The guest who submitted the request, and — self-matched — this campus's operational contact.
    /// Never authenticated as an actor here: the suite is about who may download an attachment, and a
    /// campus past WAITING_CONTACT_CONFIRMATION is simply not allowed to have a NULL contact.
    /// </summary>
    private const ulong RegistrantJ = Base + 19;

    private const ulong VisitRequestId = Base + 30;
    private const ulong VisitInstanceId = Base + 31;
    private const ulong ParticipantId = Base + 32;

    private const string MailPrefix = "g5fix-";
    private const string MailDomain = "@partner.example.com";
    private static string Mail(ulong userId) => $"{MailPrefix}{userId}{MailDomain}";

    public void Dispose()
    {
        try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true); }
        catch (IOException) { /* a temp dir left behind must never fail a test run */ }
    }

    // ── Rig ─────────────────────────────────────────────────────────────────

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId { get; init; }
        public string? Email { get; init; }
        public ulong? RoleId => null;
        public string? RoleCode { get; init; }
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId { get; init; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class NoHttpClients : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    /// <summary>
    /// Never reached: every file in this suite is LOCAL. Throwing rather than returning empty makes that
    /// an assertion — a test that accidentally routed through Drive would fail loudly instead of passing
    /// on bytes nobody stored.
    /// </summary>
    private sealed class NoDrive : IGoogleDriveStorageService
    {
        public Task<GoogleDriveUploadResult> UploadAvatarAsync(
            byte[] content, string driveFileName, string contentType, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<GoogleDriveUploadResult> UploadFileAsync(
            byte[] content, string driveFileName, string contentType, string? folderId = null,
            CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<Stream> DownloadAsync(string externalFileId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<GoogleDriveDownloadResult> DownloadRangeAsync(
            string externalFileId, long? from, long? to, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task DeleteAsync(string externalFileId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<GoogleDriveFolderResult> EnsureChildFolderAsync(
            string folderName, string parentFolderId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private static ICurrentUserService Viewer(
        ulong id, string roleCode = "STAFF", string? subRole = null, ulong? campusId = CampusId)
        => new FakeCurrentUser
        {
            UserId = id, Email = Mail(id), RoleCode = roleCode, SubRole = subRole, PrimaryCampusId = campusId,
        };

    private static ICurrentUserService Ho => Viewer(HoH, RoleCodes.Ho);
    private static ICurrentUserService StaffLeader => Viewer(StaffLeaderI, RoleCodes.Staff, UserSubRoles.Leader);

    private LocalFileStorageService Storage() => new(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:LocalRoot"] = _storageRoot })
            .Build(),
        new NoHttpClients(),
        new NoServices(),
        NullLogger<LocalFileStorageService>.Instance);

    private GetFileContentQueryHandler Download(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, Storage(), new NoDrive(),
            new FileAccessAuthorizationService(db, user, new SentEmailObjectScope(db, user)));

    /// <summary>
    /// The authorization decision alone, for files whose bytes live somewhere an offline test cannot
    /// reach (Google Drive). Uses the same service the handler calls.
    /// </summary>
    private static Task<bool> Allowed(
        ApplicationDbContext db, ICurrentUserService user, PEMS.Domain.Entities.Documents.UploadedFile file)
        => new FileAccessAuthorizationService(db, user, new SentEmailObjectScope(db, user))
            .CanDownloadAsync(file, CancellationToken.None);

    /// <summary>True when the bytes actually came back for this viewer.</summary>
    private async Task<bool> CanDownload(ApplicationDbContext db, ICurrentUserService user, ulong fileId)
    {
        try
        {
            var result = await Download(db, user)
                .Handle(new GetFileContentQuery(fileId), CancellationToken.None);
            return result.Content.Length > 0;
        }
        catch (ForbiddenException) { return false; }
    }

    // ── Seed ────────────────────────────────────────────────────────────────

    /// <summary>Writes a real file on disk plus its <c>files</c> row, and returns the id.</summary>
    private async Task<ulong> SeedFileAsync(
        ApplicationDbContext db, ulong uploader, string purpose, string name = "tai-lieu.pdf")
    {
        var objectKey = $"g5fix/{Guid.NewGuid():N}.pdf";
        var fullPath = Path.Combine(_storageRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 });

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO files (storage_provider, object_key, original_filename, mime_type, file_size, "
            + "uploaded_by, uploaded_at, file_purpose) "
            + "VALUES ('LOCAL', {0}, {1}, 'application/pdf', 6, {2}, NOW(), {3})",
            objectKey, name, uploader, purpose);

        return await db.Files.AsNoTracking()
            .Where(f => f.ObjectKey == objectKey).Select(f => f.FileId).FirstAsync();
    }

    private static async Task SeedWorldAsync(ApplicationDbContext db)
    {
        await CleanupRowsAsync(db);

        var roles = await db.Database.SqlQueryRaw<RoleRow>(
            "SELECT role_id AS RoleId, role_code AS RoleCode FROM roles").ToListAsync();
        ulong Role(string code) => roles.First(r => r.RoleCode == code).RoleId;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, status) VALUES ({0}, 'G5F', {1}, 'ACTIVE')",
            CampusId, "PEMS G5-fix Campus");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'IC', 'ACTIVE')",
            IcDeptId, CampusId, "PEMS G5-fix IC");

        static string Str(string? v) => v is null ? "NULL" : $"'{v}'";

        // A VISITOR must carry neither a campus nor a department — trg_users_validate_bi refuses both —
        // so the registrant seeded below asks for no campus at all.
        async Task User(ulong id, string name, string roleCode, string? subRole, bool inIc, bool onCampus = true)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, "
                + $"department_id, status) VALUES ({id}, {{0}}, {{1}}, {Role(roleCode)}, {Str(subRole)}, "
                + $"{(onCampus ? CampusId.ToString() : "NULL")}, {(inIc ? IcDeptId.ToString() : "NULL")}, 'ACTIVE')",
                name, Mail(id));

        await User(SenderA, "G5F A", "STAFF", "STAFF", true);
        await User(RecipientB, "G5F B", "STAFF", "STAFF", true);
        await User(CopiedC, "G5F C", "STAFF", "STAFF", true);
        await User(BlindD, "G5F D", "STAFF", "STAFF", true);
        await User(BlindE, "G5F E", "STAFF", "STAFF", true);
        await User(HostF, "G5F F", "STAFF", "STAFF", true);
        await User(OutsiderG, "G5F G", "STAFF", "STAFF", true);
        await User(HoH, "G5F H", "HO", null, false);
        await User(StaffLeaderI, "G5F I", "STAFF", "LEADER", true);
        await User(RegistrantJ, "G5F J", "VISITOR", null, false, onCampus: false);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_requests (visit_request_id, request_code, status, created_at, "
            + "registrant_user_id, registrant_full_name, registrant_organization, registrant_job_title, "
            + "registrant_phone, registrant_email, registrant_nationality) "
            + $"VALUES ({{0}}, 'G5F-REQ', 'PENDING_APPROVAL', NOW(), {RegistrantJ}, 'G5F Người đăng ký', "
            + "'G5F Org', 'G5F Title', '0900000000', {1}, 'Việt Nam')",
            VisitRequestId, Mail(RegistrantJ));

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_request_campuses (visit_instance_id, visit_request_id, campus_id, status, "
            + "operational_contact_user_id, operational_contact_confirmed_at, "
            + "operational_contact_confirmation_source, "
            + "current_host_user_id, host_assigned_by, host_assigned_at, decided_by, decided_at, "
            + "decision_actor_role, decision_source, planned_start_at, planned_end_at, created_at) "
            + $"VALUES ({{0}}, {{1}}, {{2}}, 'ASSIGNED', {RegistrantJ}, NOW(), 'REGISTRANT_SELF_MATCH', "
            + $"{HostF}, {StaffLeaderI}, NOW(), {StaffLeaderI}, "
            + "NOW(), 'STAFF_LEADER', 'STANDARD_CAMPUS_REVIEW', {3}, {4}, NOW())",
            VisitInstanceId, VisitRequestId, CampusId,
            new DateTime(2026, 8, 12, 9, 0, 0), new DateTime(2026, 8, 12, 11, 30, 0));

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_participants (participant_id, visit_instance_id, user_id, participant_role, "
            + "status, created_at) VALUES ({0}, {1}, {2}, 'IC_SUPPORT', 'INVITED', NOW())",
            ParticipantId, VisitInstanceId, RecipientB);
    }

    /// <summary>A message A → B, cc C, bcc D and E, carrying <paramref name="fileId"/>.</summary>
    private static async Task<ulong> SeedEmailAsync(
        ApplicationDbContext db, ulong fileId, string relatedType = "GENERAL", ulong? relatedId = null)
    {
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO sent_emails (related_type, related_id, subject, body_snapshot, body_format, "
            + $"status, sent_by, sent_at, created_at) VALUES ({{0}}, {(relatedId?.ToString() ?? "NULL")}, "
            + $"'Tài liệu kèm theo', '<p>x</p>', 'HTML', 'SENT', {SenderA}, NOW(), NOW())",
            relatedType);

        var emailId = await db.SentEmails.AsNoTracking()
            .Where(e => e.SentBy == SenderA).OrderByDescending(e => e.SentEmailId)
            .Select(e => e.SentEmailId).FirstAsync();

        async Task Recipient(ulong userId, string type)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO sent_email_recipients (sent_email_id, recipient_email, recipient_name, "
                + "recipient_type, delivery_status, created_at) VALUES ({0}, {1}, 'x', {2}, 'SENT', NOW())",
                emailId, Mail(userId), type);

        await Recipient(RecipientB, "TO");
        await Recipient(CopiedC, "CC");
        await Recipient(BlindD, "BCC");
        await Recipient(BlindE, "BCC");

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO sent_email_attachments (sent_email_id, file_id, attachment_type, display_name, "
            + "display_order, created_at) VALUES ({0}, {1}, 'ATTACHMENT', 'tai-lieu.pdf', 0, NOW())",
            emailId, fileId);

        return emailId;
    }

    /// <summary>
    /// One photo in the visit's shared folder, and the file row it needs.
    ///
    /// <para>
    /// A database trigger requires a visit photo to be a Google Drive image with the
    /// <c>VISIT_REQUEST_PHOTO</c> purpose, and another requires the uploader to be the host, staff, an
    /// admin or a participant. Both are honoured here rather than worked around — which means the bytes
    /// live in Drive and cannot be fetched in an offline test, so the two visit tests assert the
    /// authorization decision itself. That is the half this closure changed; the "bytes actually come
    /// back" half is covered by the LOCAL-file tests above.
    /// </para>
    /// </summary>
    private static async Task<ulong> SeedVisitPhotoAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO files (storage_provider, object_key, original_filename, mime_type, file_size, "
            + $"external_file_id, uploaded_by, uploaded_at, file_purpose) VALUES ('GOOGLE_DRIVE', {{0}}, "
            + $"'anh.jpg', 'image/jpeg', 6, 'g5fix-drive-id', {HostF}, NOW(), "
            + $"'{FilePurposeDbValues.VisitRequestPhoto}')",
            "g5fix/" + Guid.NewGuid().ToString("N") + ".jpg");

        var fileId = await db.Files.AsNoTracking()
            .Where(f => f.UploadedBy == HostF).OrderByDescending(f => f.FileId)
            .Select(f => f.FileId).FirstAsync();

        await SeedVisitPhotoRowAsync(db, fileId);
        return fileId;
    }

    private static async Task SeedVisitPhotoRowAsync(ApplicationDbContext db, ulong fileId)
    {
        await db.Database.ExecuteSqlRawAsync(
            "INSERT IGNORE INTO visit_photo_folders (visit_photo_folder_id, visit_request_id, "
            + "storage_provider, external_folder_id, folder_name, status, created_at) "
            + $"VALUES ({Base + 40}, {VisitRequestId}, 'GOOGLE_DRIVE', 'g5fix-folder', 'G5F', "
            + "'ACTIVE', NOW())");

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_photos (visit_request_id, visit_instance_id, visit_photo_folder_id, "
            + $"file_id, status, uploaded_by, uploaded_at) VALUES ({VisitRequestId}, {VisitInstanceId}, "
            + $"{Base + 40}, {{0}}, 'ACTIVE', {HostF}, NOW())",
            fileId);
    }

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    /// <summary>
    /// Puts this suite's id band back to empty, children first.
    ///
    /// <para>
    /// This was eleven DELETE statements in the order the schema happened to need when they were
    /// written, and such a list is only correct until the next foreign key: <c>files</c> alone has
    /// eleven referrers. It named none of them, so one <c>documents</c> row pointing at a fixture file
    /// — ON DELETE RESTRICT — was enough to refuse the <c>files</c> delete and fail the whole class in
    /// setup, a long way from whatever left the row. <see cref="FixtureCleanup"/> derives the order
    /// from the live schema, so the visit tree, the photos, the drafts and the sessions below are all
    /// reached from the roots rather than listed.
    /// </para>
    /// <para>
    /// Order between roots still matters: <c>files.uploaded_by</c> and <c>sent_emails.sent_by</c>
    /// reference <c>users</c> ON DELETE SET NULL, so removing the users first would blank the columns
    /// these roots identify their rows by and strand them.
    /// </para>
    /// </summary>
    private static Task CleanupRowsAsync(ApplicationDbContext db)
        => FixtureCleanup.For(db)
            .Root("sent_emails", $"sent_by BETWEEN {Base} AND {Base + 100}")
            .Root("files", $"uploaded_by BETWEEN {Base} AND {Base + 100}")
            .Root("visit_requests", $"visit_request_id = {VisitRequestId}")
            .Root("users", $"user_id BETWEEN {Base} AND {Base + 100}")
            .Root("departments", $"department_id = {IcDeptId}")
            .Root("campuses", $"campus_id = {CampusId}")
            .RunAsync();

    // ── B. The email-attachment matrix ───────────────────────────────────────

    [Fact]
    public async Task Email_attachment_follows_exactly_the_rule_that_governs_the_message()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedFileAsync(db, SenderA, FilePurposeDbValues.ReportAttachment);
        await SeedEmailAsync(db, fileId);

        // Everyone on the envelope gets the attachment — blind copies included, because their own copy
        // of the message carried it.
        Assert.True(await CanDownload(db, Viewer(SenderA), fileId));
        Assert.True(await CanDownload(db, Viewer(RecipientB), fileId));
        Assert.True(await CanDownload(db, Viewer(CopiedC), fileId));
        Assert.True(await CanDownload(db, Viewer(BlindD), fileId));
        Assert.True(await CanDownload(db, Viewer(BlindE), fileId));

        // Nobody else — and seniority is not a relation to a message.
        Assert.False(await CanDownload(db, Viewer(OutsiderG), fileId));
        Assert.False(await CanDownload(db, Ho, fileId));
        Assert.False(await CanDownload(db, StaffLeader, fileId));

        // The visit host has no claim on a message that is not about their visit.
        Assert.False(await CanDownload(db, Viewer(HostF), fileId));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task Object_scope_grants_the_attachment_exactly_when_it_grants_the_message()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedFileAsync(db, SenderA, FilePurposeDbValues.ReportAttachment);
        await SeedEmailAsync(db, fileId, EmailActionTargetTypes.VisitParticipant, ParticipantId);

        // F hosts the visit this message is about, so the same linked-object branch that opens the
        // message opens its attachment.
        Assert.True(await CanDownload(db, Viewer(HostF), fileId));
        Assert.True(await CanDownload(db, Ho, fileId));
        Assert.True(await CanDownload(db, StaffLeader, fileId));

        // Still nothing for someone with no relation to the visit or the message.
        Assert.False(await CanDownload(db, Viewer(OutsiderG), fileId));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_refusal_reveals_nothing_about_the_file()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedFileAsync(db, SenderA, FilePurposeDbValues.ReportAttachment, "bao-cao-mat.pdf");
        await SeedEmailAsync(db, fileId);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => Download(db, Viewer(OutsiderG))
            .Handle(new GetFileContentQuery(fileId), CancellationToken.None));

        // Not the filename, the size, the MIME type, the owner, the storage key, nor a blind recipient.
        var message = error.Message ?? string.Empty;
        Assert.DoesNotContain("bao-cao-mat", message);
        Assert.DoesNotContain("application/pdf", message);
        Assert.DoesNotContain("g5fix/", message);
        Assert.DoesNotContain(Mail(BlindD), message);
        Assert.DoesNotContain(Mail(SenderA), message);

        await CleanupRowsAsync(db);
    }


    // ── D. IDOR ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Walking_the_ids_returns_neither_bytes_nor_metadata()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedFileAsync(db, SenderA, FilePurposeDbValues.ReportAttachment);
        await SeedEmailAsync(db, fileId);

        // Every id around the real one: an outsider learns nothing from any of them.
        for (var candidate = fileId - 2; candidate <= fileId + 2; candidate++)
        {
            var outcome = await Attempt(db, Viewer(OutsiderG), candidate);
            Assert.NotEqual("ok", outcome);
        }

        // A file that does not exist is reported as missing, and one that exists but is not theirs is
        // reported as forbidden — the repository's existing convention, applied consistently.
        Assert.Equal("forbidden", await Attempt(db, Viewer(OutsiderG), fileId));
        Assert.Equal("notfound", await Attempt(db, Viewer(OutsiderG), 999_999_999));
        Assert.Equal("ok", await Attempt(db, Viewer(RecipientB), fileId));

        await CleanupRowsAsync(db);

        async Task<string> Attempt(ApplicationDbContext context, ICurrentUserService user, ulong id)
        {
            try
            {
                await Download(context, user).Handle(new GetFileContentQuery(id), CancellationToken.None);
                return "ok";
            }
            catch (ForbiddenException) { return "forbidden"; }
            catch (NotFoundException) { return "notfound"; }
        }
    }

    [Fact]
    public async Task An_unreferenced_upload_is_visible_only_to_the_person_who_uploaded_it()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        // Mid-compose: the file is uploaded but nothing points at it yet.
        var fileId = await SeedFileAsync(db, SenderA, FilePurposeDbValues.Other);

        Assert.True(await CanDownload(db, Viewer(SenderA), fileId));
        Assert.False(await CanDownload(db, Viewer(RecipientB), fileId));
        Assert.False(await CanDownload(db, Ho, fileId));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task Uploading_a_file_does_not_keep_access_to_an_object_the_uploader_cannot_see()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        // G uploads a file which then becomes an attachment on a message G is not part of.
        var fileId = await SeedFileAsync(db, OutsiderG, FilePurposeDbValues.Other);
        await SeedEmailAsync(db, fileId);

        // The uploader fallback applies only while nothing references the file. Once it belongs to a
        // message, the message decides — and G is not on it.
        Assert.False(await CanDownload(db, Viewer(OutsiderG), fileId));
        Assert.True(await CanDownload(db, Viewer(RecipientB), fileId));

        await CleanupRowsAsync(db);
    }

    // ── E. The other modules still work ──────────────────────────────────────

    [Fact]
    public async Task A_visit_photo_follows_the_visits_own_relation_rule()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedVisitPhotoAsync(db);
        var file = await db.Files.AsNoTracking().FirstAsync(f => f.FileId == fileId);

        // Host, campus Staff Leader and HO hold an internal relation to the visit.
        Assert.True(await Allowed(db, Viewer(HostF), file));
        Assert.True(await Allowed(db, StaffLeader, file));
        Assert.True(await Allowed(db, Ho, file));

        // An invited-but-not-accepted participant does not, which is the visit module's own rule —
        // borrowed here, not re-decided.
        Assert.False(await Allowed(db, Viewer(RecipientB), file));
        Assert.False(await Allowed(db, Viewer(OutsiderG), file));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task An_accepted_participant_gets_the_visit_photo()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedVisitPhotoAsync(db);
        var file = await db.Files.AsNoTracking().FirstAsync(f => f.FileId == fileId);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_participants SET status = 'ACCEPTED' WHERE participant_id = {0}", ParticipantId);

        Assert.True(await Allowed(db, Viewer(RecipientB), file));
        Assert.False(await Allowed(db, Viewer(OutsiderG), file));

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_partner_file_stays_readable_because_the_partner_screen_is_open_to_staff()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedFileAsync(db, SenderA, FilePurposeDbValues.PartnerDocument, "logo.png");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO partners (partner_id, owner_campus_id, name, partner_type, cooperation_status, "
            + $"logo_file_id, created_at) VALUES ({Base + 50}, {CampusId}, 'G5F Partner', 'UNIVERSITY', "
            + $"'ACTIVE', {fileId}, NOW())");

        // PartnersController is [Authorize] with no further role gate, so every internal account can open
        // a partner and see its logo. Scoping the file tighter than the screen would only break the screen.
        Assert.True(await CanDownload(db, Viewer(OutsiderG), fileId));

        // No hand-written delete for the partner: partners.owner_campus_id is ON DELETE RESTRICT, so
        // the campuses root reaches it. (Its logo_file_id is SET NULL, which is why the file root
        // alone would not have.)
        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_document_row_keeps_the_file_readable_for_internal_users()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedFileAsync(db, SenderA, FilePurposeDbValues.Document);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO documents (document_id, file_id, owner_type, title, status, created_at, created_by) "
            + $"VALUES ({Base + 60}, {fileId}, 'GENERAL', 'G5F Tài liệu', 'PUBLISHED', NOW(), {SenderA})");

        Assert.True(await CanDownload(db, Viewer(OutsiderG), fileId));

        // No hand-written delete for the document row: documents.file_id is ON DELETE RESTRICT, which
        // is exactly the edge the files root follows — and exactly the row that used to make the
        // hand-written cleanup fail.
        await CleanupRowsAsync(db);
    }

    // ── F. Storage safety ────────────────────────────────────────────────────

    [Fact]
    public async Task An_object_key_that_escapes_the_storage_root_is_never_opened()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        // A file the caller IS entitled to, whose stored key points outside the root. Authorization
        // passes; the read must still refuse rather than serve whatever is at that path.
        var outsideName = "escaped-" + Guid.NewGuid().ToString("N") + ".pdf";
        var outsidePath = Path.Combine(Path.GetTempPath(), outsideName);
        await File.WriteAllTextAsync(outsidePath, "secret");

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO files (storage_provider, object_key, original_filename, mime_type, file_size, "
                + $"uploaded_by, uploaded_at, file_purpose) VALUES ('LOCAL', {{0}}, 'x.pdf', "
                + $"'application/pdf', 6, {SenderA}, NOW(), 'OTHER')",
                "../../../../../../" + outsideName);

            var escapedId = await db.Files.AsNoTracking()
                .Where(f => f.UploadedBy == SenderA).OrderByDescending(f => f.FileId)
                .Select(f => f.FileId).FirstAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => Download(db, Viewer(SenderA))
                .Handle(new GetFileContentQuery(escapedId), CancellationToken.None));
        }
        finally
        {
            try { File.Delete(outsidePath); } catch (IOException) { }
            await CleanupRowsAsync(db);
        }
    }

    [Fact]
    public async Task A_missing_file_on_disk_fails_the_same_way_every_time()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO files (storage_provider, object_key, original_filename, mime_type, file_size, "
            + $"uploaded_by, uploaded_at, file_purpose) VALUES ('LOCAL', {{0}}, 'khong-co.pdf', "
            + $"'application/pdf', 6, {SenderA}, NOW(), 'OTHER')",
            "g5fix/" + Guid.NewGuid().ToString("N") + ".pdf");

        var ghostId = await db.Files.AsNoTracking()
            .Where(f => f.UploadedBy == SenderA).OrderByDescending(f => f.FileId)
            .Select(f => f.FileId).FirstAsync();

        var error = await Assert.ThrowsAsync<NotFoundException>(() => Download(db, Viewer(SenderA))
            .Handle(new GetFileContentQuery(ghostId), CancellationToken.None));
        Assert.DoesNotContain(_storageRoot, error.Message ?? string.Empty);

        await CleanupRowsAsync(db);
    }
}
