using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Common.Files;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// G5 security closure over real HTTP: <c>GET /api/files/{id}/download</c> and <c>/content</c>.
///
/// <para>
/// The policy itself is proved against the database in <see cref="FileDownloadAuthorizationTests"/>.
/// What this suite adds is the part only a real request can show: that the route is wired to the gate at
/// all, that an unauthenticated caller is turned away before anything else happens, and that a refusal
/// comes back as a status code carrying none of the file's details.
/// </para>
/// </summary>
public sealed class FileDownloadRouteTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private readonly PemsWebApplicationFactory _factory;
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "pems-g5fix-http-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// This suite's own id band, [Base, Base+100].
    ///
    /// <para>
    /// It was 991_700 — the same value <see cref="FilePreviewDownloadTests"/> uses, so the two suites
    /// claimed exactly the same hundred ids. Nothing raced, because the assembly runs serially, but
    /// each one opens by deleting its whole band: whichever ran second wiped rows the first had left,
    /// and either one failing part-way through left the other seeding on top of a half-deleted world.
    /// Moved here, into space no other suite touches, rather than shrinking one of the two.
    /// </para>
    /// <para>
    /// The bands in use, so the next one does not have to be found by collision:
    /// 990_900 report e-mail · 991_100 manual pipeline · 991_300 sent-e-mail history ·
    /// 991_400 G8 journey / reply-all / report invoice · 991_500 file-download authorization ·
    /// 991_600…991_647 UT-RESUBMIT auto-increment · 991_700 file preview · 992_500 here ·
    /// 993_100 fixture cleanup · 8_400_000 send idempotency.
    /// </para>
    /// </summary>
    private const ulong Base = 992_500;

    private const ulong CampusId = Base + 1;
    private const ulong IcDeptId = Base + 2;
    private const ulong SenderA = Base + 10;
    private const ulong RecipientB = Base + 11;
    private const ulong OutsiderG = Base + 12;
    private const ulong HoH = Base + 13;

    private const string MailPrefix = "g5http-";
    private const string MailDomain = "@partner.example.com";
    private static string Mail(ulong id) => $"{MailPrefix}{id}{MailDomain}";

    private readonly Dictionary<ulong, ulong> _sessions = new();
    private ulong _fileId;

    public FileDownloadRouteTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await CleanupAsync(db);
        await SeedAsync(db);
    }

    public async Task DisposeAsync()
    {
        using (var db = EmailEvidenceHarness.NewContext()) await CleanupAsync(db);
        try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true); }
        catch (IOException) { /* a temp dir left behind must never fail a test run */ }
    }

    // ── Rig ─────────────────────────────────────────────────────────────────

    private HttpClient Client(ulong? userId, string roleCode = RoleCodes.Staff, string? subRole = "STAFF")
    {
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["FileStorage:LocalRoot"] = _storageRoot })))
            .CreateClient();

        if (userId is not { } id) return client;

        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, id.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, roleCode);
        if (subRole is not null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubRoleHeader, subRole);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, _sessions[id].ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.PrimaryCampusIdHeader, CampusId.ToString());
        return client;
    }

    // ── Seed ────────────────────────────────────────────────────────────────

    private async Task SeedAsync(ApplicationDbContext db)
    {
        var roles = await db.Database.SqlQueryRaw<RoleRow>(
            "SELECT role_id AS RoleId, role_code AS RoleCode FROM roles").ToListAsync();
        ulong Role(string code) => roles.First(r => r.RoleCode == code).RoleId;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, status) VALUES ({0}, 'G5H', {1}, 'ACTIVE')",
            CampusId, "PEMS G5-http Campus");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'IC', 'ACTIVE')",
            IcDeptId, CampusId, "PEMS G5-http IC");

        static string Str(string? v) => v is null ? "NULL" : $"'{v}'";

        async Task User(ulong id, string name, string roleCode, string? subRole, bool inIc)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, "
                + $"department_id, status) VALUES ({id}, {{0}}, {{1}}, {Role(roleCode)}, {Str(subRole)}, "
                + $"{CampusId}, {(inIc ? IcDeptId.ToString() : "NULL")}, 'ACTIVE')",
                name, Mail(id));
            // The API needs a live session row, not just the claims the test handler supplies.
            _sessions[id] = await DatabaseResetHelper.CreateActiveSessionAsync(db, id, roleCode);
        }

        await User(SenderA, "G5H A", RoleCodes.Staff, "STAFF", true);
        await User(RecipientB, "G5H B", RoleCodes.Staff, "STAFF", true);
        await User(OutsiderG, "G5H G", RoleCodes.Staff, "STAFF", true);
        await User(HoH, "G5H H", RoleCodes.Ho, null, false);

        // A real file on disk, attached to a message from A to B.
        var objectKey = $"g5http/{Guid.NewGuid():N}.pdf";
        var fullPath = Path.Combine(_storageRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 });

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO files (storage_provider, object_key, original_filename, mime_type, file_size, "
            + $"uploaded_by, uploaded_at, file_purpose) VALUES ('LOCAL', {{0}}, 'bao-cao-mat.pdf', "
            + $"'application/pdf', 6, {SenderA}, NOW(), '{FilePurposeDbValues.ReportAttachment}')",
            objectKey);
        _fileId = await db.Files.AsNoTracking()
            .Where(f => f.ObjectKey == objectKey).Select(f => f.FileId).FirstAsync();

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO sent_emails (related_type, subject, body_snapshot, body_format, status, sent_by, "
            + $"sent_at, created_at) VALUES ('GENERAL', 'Tài liệu', '<p>x</p>', 'HTML', 'SENT', {SenderA}, "
            + "NOW(), NOW())");
        var emailId = await db.SentEmails.AsNoTracking()
            .Where(e => e.SentBy == SenderA).OrderByDescending(e => e.SentEmailId)
            .Select(e => e.SentEmailId).FirstAsync();

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO sent_email_recipients (sent_email_id, recipient_email, recipient_name, "
            + "recipient_type, delivery_status, created_at) VALUES ({0}, {1}, 'B', 'TO', 'SENT', NOW())",
            emailId, Mail(RecipientB));
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO sent_email_attachments (sent_email_id, file_id, attachment_type, display_name, "
            + "display_order, created_at) VALUES ({0}, {1}, 'ATTACHMENT', 'bao-cao-mat.pdf', 0, NOW())",
            emailId, _fileId);
    }

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    /// <summary>
    /// Puts this suite's id band back to empty, children first.
    ///
    /// <para>
    /// This was six DELETE statements in the order the schema needed on the day they were written, and
    /// that order is only ever correct until the next foreign key. It names no referrer of
    /// <c>files</c> at all, so a single <c>documents</c> row pointing at one of these files — the
    /// constraint is ON DELETE RESTRICT — would refuse the delete and take the whole class down in
    /// setup, before a line of product code ran. <see cref="FixtureCleanup"/> reads the order from the
    /// live schema instead, which is also how <c>user_sessions</c> stops needing a line here: it is
    /// reached from <c>users</c>.
    /// </para>
    /// <para>
    /// Order between roots still matters: <c>files.uploaded_by</c> and <c>sent_emails.sent_by</c> both
    /// reference <c>users</c> ON DELETE SET NULL, so deleting the users first would blank the very
    /// columns these roots identify their rows by and leave them behind, unowned and invisible.
    /// </para>
    /// </summary>
    private static Task CleanupAsync(ApplicationDbContext db)
        => FixtureCleanup.For(db)
            .Root("sent_emails", $"sent_by BETWEEN {Base} AND {Base + 100}")
            .Root("files", $"uploaded_by BETWEEN {Base} AND {Base + 100}")
            .Root("users", $"user_id BETWEEN {Base} AND {Base + 100}")
            .Root("departments", $"department_id = {IcDeptId}")
            .Root("campuses", $"campus_id = {CampusId}")
            .RunAsync();

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_anonymous_caller_never_reaches_the_file()
    {
        var response = await Client(null).GetAsync($"/api/files/{_fileId}/download");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_sender_and_the_recipient_get_the_bytes()
    {
        foreach (var userId in new[] { SenderA, RecipientB })
        {
            var response = await Client(userId).GetAsync($"/api/files/{_fileId}/download");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task A_signed_in_stranger_is_refused_on_both_routes()
    {
        foreach (var route in new[] { "download", "content" })
        {
            var response = await Client(OutsiderG).GetAsync($"/api/files/{_fileId}/{route}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            // The body is the API's ordinary error envelope, not the document: a refused request must
            // never return the file's bytes under an error status.
            var body = await response.Content.ReadAsByteArrayAsync();
            Assert.DoesNotContain("%PDF", System.Text.Encoding.UTF8.GetString(body));
            Assert.NotEqual("application/pdf", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task Being_HO_is_not_a_way_in()
    {
        var response = await Client(HoH, RoleCodes.Ho, subRole: null)
            .GetAsync($"/api/files/{_fileId}/download");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_refusal_carries_none_of_the_files_details()
    {
        var response = await Client(OutsiderG).GetAsync($"/api/files/{_fileId}/download");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("bao-cao-mat", body);
        Assert.DoesNotContain("application/pdf", body);
        Assert.DoesNotContain("g5http/", body);
        Assert.DoesNotContain(_storageRoot, body);
        Assert.Null(response.Content.Headers.ContentDisposition);
    }

    [Fact]
    public async Task Walking_the_ids_yields_nothing_but_refusals()
    {
        var client = Client(OutsiderG);
        for (var candidate = _fileId - 2; candidate <= _fileId + 2; candidate++)
        {
            var response = await client.GetAsync($"/api/files/{candidate}/download");
            Assert.True(
                response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                $"file {candidate} answered {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task The_download_filename_reaches_the_header_safely_encoded()
    {
        var response = await Client(RecipientB).GetAsync($"/api/files/{_fileId}/download");
        var disposition = response.Content.Headers.ContentDisposition;

        Assert.NotNull(disposition);
        // The framework encodes the name into the header rather than pasting it in raw, so a filename
        // could not open a header of its own even if one carried a newline.
        Assert.Contains("bao-cao-mat", disposition!.ToString());
        Assert.DoesNotContain("\r", disposition.ToString());
        Assert.DoesNotContain("\n", disposition.ToString());
    }
}
