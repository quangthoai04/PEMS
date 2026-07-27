using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Batch 9-fix — the two invoice routes the frontend has always called and the controller never
/// exposed.
///
/// <para>
/// Both handlers were fully migrated in Batch 9, so nothing about the message, the document or the
/// recipient changes here. What was missing was the door: <c>reportsApi.ts</c> posts to
/// <c>…/departments/{id}/send-invoice</c> and <c>dept-leader-report-v2/send-invoice</c>, and neither
/// existed on <see cref="PEMS.Api.Controllers.ReportsController"/>. These tests hold the routes to the
/// URLs the client already uses, and prove the scope is enforced by the backend rather than by
/// whichever screen happens to call it.
/// </para>
/// </summary>
public sealed class ReportInvoiceRouteTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string StaffLeaderUrlTemplate = "/api/reports/staff-leader-report-v2/departments/{0}/send-invoice";
    private const string DeptLeaderUrl = "/api/reports/dept-leader-report-v2/send-invoice";

    /// <summary>Suite-private id range, high enough not to collide with other suites' rows.</summary>
    private const ulong Base = 991_400;
    private const ulong CampusId = Base + 1;
    private const ulong OtherCampusId = Base + 2;
    private const ulong DeptId = Base + 3;
    private const ulong IcDeptId = Base + 4;
    private const ulong OtherCampusDeptId = Base + 5;
    private const ulong StaffLeaderId = Base + 6;
    private const ulong DeptLeaderId = Base + 7;
    private const ulong DeptStaffId = Base + 8;
    private const ulong HoId = Base + 12;
    private const ulong VisitRequestId = Base + 9;
    private const ulong VisitInstanceId = Base + 10;
    private const ulong LogisticsItemId = Base + 11;

    /// <summary>
    /// Session ids created alongside the seeded users. The API requires a live session, not just
    /// claims, so every authenticated client carries one.
    /// </summary>
    private readonly Dictionary<ulong, ulong> _sessions = new();

    private const string MailPrefix = "b9fix-";
    private const string MailDomain = "@partner.example.com";
    private static string Mail(ulong userId) => $"{MailPrefix}{userId}{MailDomain}";

    private readonly PemsWebApplicationFactory _factory;
    private readonly string _pickupDirectory =
        Path.Combine(Path.GetTempPath(), "pems-b9fix-" + Guid.NewGuid().ToString("N"));
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "pems-b9fix-files-" + Guid.NewGuid().ToString("N"));

    public ReportInvoiceRouteTests(PemsWebApplicationFactory factory) => _factory = factory;

    private static readonly object[] Items =
    {
        new { logisticsItemId = LogisticsItemId, unitPrice = 1_500_000m },
    };

    /// <summary>The exact body shape <c>reportsApi.ts</c> sends.</summary>
    private static object InvoicePayload() => new
    {
        fromDate = "2026-07-01",
        toDate = "2026-07-31",
        note = "Ghi chú hóa đơn",
        items = Items,
    };

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await CleanupAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());

        foreach (var dir in new[] { _pickupDirectory, _storageRoot })
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch (IOException) { /* a temp dir left behind must never fail a test run */ }
        }
    }

    // ── Rig ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A host whose SMTP writes real <c>.eml</c> files instead of skipping. The default Testing config
    /// has <c>Smtp:Enabled=false</c>, which is a Skipped delivery — and under Batch 9's Mandatory rule a
    /// Skipped send fails the command, so the success path needs a provider that actually accepts.
    /// </summary>
    private HttpClient WorkingProviderClient(Action<HttpClient> authenticate)
    {
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Smtp:Enabled"] = "true",
                    ["Smtp:FromEmail"] = "no-reply@pems.test",
                    ["Smtp:FromName"] = "PEMS",
                    ["Smtp:PickupDirectory"] = _pickupDirectory,
                    ["FileStorage:LocalRoot"] = _storageRoot,
                }))).CreateClient();

        authenticate(client);
        return client;
    }

    private HttpClient PlainClient(Action<HttpClient> authenticate)
    {
        var client = _factory.CreateClient();
        authenticate(client);
        return client;
    }

    private void Authenticate(
        HttpClient client, ulong userId, string roleCode, string? subRole, ulong? deptId)
    {
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, roleCode);
        if (subRole is not null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubRoleHeader, subRole);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, _sessions[userId].ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.PrimaryCampusIdHeader, CampusId.ToString());
        if (deptId is not null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.DepartmentIdHeader, deptId.Value.ToString());
    }

    private void AsStaffLeader(HttpClient client)
        => Authenticate(client, StaffLeaderId, RoleCode.Staff, SubRole.Leader, IcDeptId);

    private void AsDeptLeader(HttpClient client)
        => Authenticate(client, DeptLeaderId, RoleCode.Department, SubRole.Leader, DeptId);

    private void AsDeptStaff(HttpClient client)
        => Authenticate(client, DeptStaffId, RoleCode.Department, SubRole.Staff, DeptId);

    private void AsHo(HttpClient client)
        => Authenticate(client, HoId, RoleCode.Ho, null, null);

    private static void Anonymous(HttpClient client) { }

    private static string StaffLeaderUrl(ulong departmentId)
        => string.Format(StaffLeaderUrlTemplate, departmentId);

    // ── Seed ────────────────────────────────────────────────────────────────

    private async Task<ApplicationDbContext> SeedAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await CleanupAsync(db);

        var roles = await db.Database.SqlQueryRaw<RoleRow>(
            "SELECT role_id AS RoleId, role_code AS RoleCode FROM roles").ToListAsync();
        ulong Role(string code) => roles.First(r => r.RoleCode == code).RoleId;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, status) VALUES "
            + "({0}, {1}, {2}, 'ACTIVE'), ({3}, {4}, {5}, 'ACTIVE')",
            CampusId, "B9F1", "PEMS B9fix Campus", OtherCampusId, "B9F2", "PEMS B9fix Campus khác");

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) VALUES "
            + "({0}, {1}, {2}, 'GENERAL', 'ACTIVE'), ({3}, {4}, {5}, 'IC', 'ACTIVE'), "
            + "({6}, {7}, {8}, 'GENERAL', 'ACTIVE')",
            DeptId, CampusId, "PEMS B9fix Phòng Hành chính",
            IcDeptId, CampusId, "PEMS B9fix Văn phòng IC",
            OtherCampusDeptId, OtherCampusId, "PEMS B9fix Phòng campus khác");

        static string Num(ulong? v) => v?.ToString() ?? "NULL";

        // sub_role is only for STAFF/DEPARTMENT; HO leaves it NULL.
        static string Sub(string? v) => v is null ? "NULL" : $"'{v}'";

        async Task User(ulong id, string name, string roleCode, string? subRole, ulong campusId, ulong? deptId)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, department_id, status) "
                + $"VALUES ({id}, {{0}}, {{1}}, {Role(roleCode)}, {Sub(subRole)}, {campusId}, {Num(deptId)}, 'ACTIVE')",
                name, Mail(id));

        await User(StaffLeaderId, "PEMS B9fix Staff Leader", RoleCode.Staff, SubRole.Leader, CampusId, IcDeptId);
        await User(DeptLeaderId, "PEMS B9fix Trưởng phòng", RoleCode.Department, SubRole.Leader, CampusId, DeptId);
        await User(DeptStaffId, "PEMS B9fix Nhân sự phòng", RoleCode.Department, SubRole.Staff, CampusId, DeptId);
        await User(HoId, "PEMS B9fix Head Office", RoleCode.Ho, null, CampusId, null);

        // A live session per actor: the API authenticates the session, not just the claims.
        _sessions.Clear();
        foreach (var (userId, role) in new[]
                 {
                     (StaffLeaderId, EffectiveRole.StaffLeader),
                     (DeptLeaderId, EffectiveRole.DepartmentLead),
                     (DeptStaffId, EffectiveRole.Department),
                     (HoId, EffectiveRole.Ho),
                 })
        {
            _sessions[userId] = await DatabaseResetHelper.CreateActiveSessionAsync(db, userId, role);
        }

        // The department head the C-26 invoice is addressed to.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE departments SET head_user_id = {0} WHERE department_id = {1}", DeptLeaderId, DeptId);

        // One priced line for both directions.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_requests (visit_request_id, request_code, status, created_at, "
            + "registrant_full_name, registrant_organization, registrant_job_title, registrant_phone, "
            + "registrant_email, registrant_nationality, contact_person_full_name, "
            + "contact_person_organization, contact_person_phone, contact_person_email) "
            + "VALUES ({0}, {1}, 'PENDING_APPROVAL', NOW(), 'B9fix Người đăng ký', 'B9fix Org', 'B9fix Title', "
            + "'0900000000', {2}, 'Việt Nam', 'B9fix Đầu mối', 'B9fix Org', '0900000001', {2})",
            VisitRequestId, "B9FIX-REQ", MailPrefix + "visitor" + MailDomain);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_request_campuses (visit_instance_id, visit_request_id, campus_id, status, "
            + "planned_start_at, planned_end_at, created_at) "
            + "VALUES ({0}, {1}, {2}, 'WAITING_REQUEST_APPROVAL', {3}, {4}, NOW())",
            VisitInstanceId, VisitRequestId, CampusId,
            new DateTime(2026, 7, 10, 9, 0, 0), new DateTime(2026, 7, 10, 11, 30, 0));

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_logistics_items (logistics_item_id, visit_instance_id, title, item_type, "
            + "quantity, requested_to_department_id, status, created_at) "
            + "VALUES ({0}, {1}, {2}, 'OTHER', 2, {3}, 'DONE', NOW())",
            LogisticsItemId, VisitInstanceId, "Thuê màn LED", DeptId);

        return db;
    }

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    private static async Task CleanupAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "DELETE r, e FROM sent_emails e JOIN sent_email_recipients r ON r.sent_email_id = e.sent_email_id "
            + "WHERE r.recipient_email LIKE {0}", MailPrefix + "%" + MailDomain);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM files WHERE file_purpose = 'REPORT_ATTACHMENT' AND uploaded_by BETWEEN {0} AND {1}",
            Base, Base + 100);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_logistics_items WHERE logistics_item_id = {0}", LogisticsItemId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_request_campuses WHERE visit_instance_id = {0}", VisitInstanceId);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM visit_requests WHERE visit_request_id = {0}", VisitRequestId);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE departments SET head_user_id = NULL WHERE department_id BETWEEN {0} AND {1}", Base, Base + 100);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM user_sessions WHERE user_id BETWEEN {0} AND {1}", Base, Base + 100);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM users WHERE user_id BETWEEN {0} AND {1}", Base, Base + 100);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM departments WHERE department_id BETWEEN {0} AND {1}", Base, Base + 100);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM campuses WHERE campus_id BETWEEN {0} AND {1}", Base, Base + 100);
    }

    private string[] Messages()
        => Directory.Exists(_pickupDirectory) ? Directory.GetFiles(_pickupDirectory, "*.eml") : Array.Empty<string>();

    // ── The routes exist at the URLs the client already posts to ────────────

    [Fact]
    public async Task The_staff_leader_invoice_url_is_no_longer_missing()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var response = await WorkingProviderClient(AsStaffLeader)
            .PostAsJsonAsync(StaffLeaderUrl(DeptId), InvoicePayload());

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_department_leader_invoice_url_is_no_longer_missing()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var response = await WorkingProviderClient(AsDeptLeader)
            .PostAsJsonAsync(DeptLeaderUrl, InvoicePayload());

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Both are POST-only; a GET must not quietly resolve to some other action.</summary>
    [Fact]
    public async Task The_invoice_urls_answer_to_post_only()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var staffLeaderGet = await PlainClient(AsStaffLeader).GetAsync(StaffLeaderUrl(DeptId));
        var deptLeaderGet = await PlainClient(AsDeptLeader).GetAsync(DeptLeaderUrl);

        Assert.Contains(staffLeaderGet.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Contains(deptLeaderGet.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    /// <summary>
    /// The department id travels in the path, not the body — the route value is what the command uses.
    /// A body that tries to name a different department must not win.
    /// </summary>
    [Fact]
    public async Task The_department_in_the_path_is_the_one_that_counts()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        // Path says the seeded department; body tries to redirect the invoice at another campus's one.
        var response = await WorkingProviderClient(AsStaffLeader).PostAsJsonAsync(
            StaffLeaderUrl(DeptId),
            new { departmentId = OtherCampusDeptId, fromDate = "2026-07-01", toDate = "2026-07-31", items = Items });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recipient = await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.RecipientEmail.StartsWith(MailPrefix))
            .OrderByDescending(r => r.SentEmailRecipientId)
            .FirstAsync();
        Assert.Equal(Mail(DeptLeaderId), recipient.RecipientEmail);
    }

    // ── Who may call them ───────────────────────────────────────────────────

    [Fact]
    public async Task An_anonymous_caller_is_challenged_on_both_routes()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var staffLeader = await PlainClient(Anonymous).PostAsJsonAsync(StaffLeaderUrl(DeptId), InvoicePayload());
        var deptLeader = await PlainClient(Anonymous).PostAsJsonAsync(DeptLeaderUrl, InvoicePayload());

        Assert.Equal(HttpStatusCode.Unauthorized, staffLeader.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deptLeader.StatusCode);
    }

    [Fact]
    public async Task A_department_leader_cannot_use_the_staff_leader_route()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var response = await PlainClient(AsDeptLeader).PostAsJsonAsync(StaffLeaderUrl(DeptId), InvoicePayload());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("staff-leader")]
    [InlineData("dept-staff")]
    [InlineData("ho")]
    public async Task Only_a_department_leader_may_send_an_invoice_upwards(string actor)
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        Action<HttpClient> authenticate = actor switch
        {
            "staff-leader" => AsStaffLeader,
            "dept-staff" => AsDeptStaff,
            _ => AsHo,
        };

        var response = await PlainClient(authenticate).PostAsJsonAsync(DeptLeaderUrl, InvoicePayload());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Role alone is not scope. A Staff Leader of another campus holds the right role and must still be
    /// refused this department — the check lives in the handler, not in whichever screen calls it.
    /// </summary>
    [Fact]
    public async Task A_staff_leader_cannot_invoice_a_department_outside_their_campus()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var response = await WorkingProviderClient(AsStaffLeader)
            .PostAsJsonAsync(StaffLeaderUrl(OtherCampusDeptId), InvoicePayload());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(Messages());
    }

    [Fact]
    public async Task An_unknown_department_is_refused_rather_than_invoiced()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var response = await WorkingProviderClient(AsStaffLeader)
            .PostAsJsonAsync(StaffLeaderUrl(Base + 999), InvoicePayload());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(Messages());
    }

    // ── End to end, through the real controller and the real handler ────────

    [Fact]
    public async Task The_staff_leader_route_produces_one_invoice_email_with_its_document()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var response = await WorkingProviderClient(AsStaffLeader)
            .PostAsJsonAsync(StaffLeaderUrl(DeptId), InvoicePayload());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await AssertOneInvoiceAsync(Mail(DeptLeaderId));
    }

    [Fact]
    public async Task The_department_leader_route_sends_the_invoice_to_the_campus_staff_leader()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var response = await WorkingProviderClient(AsDeptLeader)
            .PostAsJsonAsync(DeptLeaderUrl, InvoicePayload());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Determined by the backend from the department's campus — the request has no field for it.
        await AssertOneInvoiceAsync(Mail(StaffLeaderId));
    }

    /// <summary>One message, one PDF, one attachment row, and the shared invoice template.</summary>
    private async Task AssertOneInvoiceAsync(string expectedRecipient)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var recipient = Assert.Single(await db.SentEmailRecipients.AsNoTracking()
            .Where(r => r.RecipientEmail.StartsWith(MailPrefix)).ToListAsync());
        Assert.Equal(expectedRecipient, recipient.RecipientEmail);
        Assert.Equal(EmailRecipientTypes.To, recipient.RecipientType);

        var message = await db.SentEmails.AsNoTracking()
            .SingleAsync(e => e.SentEmailId == recipient.SentEmailId);
        var invoiceTemplateId = await db.EmailTemplates.AsNoTracking()
            .Where(t => t.TemplateCode == SystemEmailTemplates.ReportDepartmentInvoice)
            .Select(t => t.EmailTemplateId).SingleAsync();
        Assert.Equal(invoiceTemplateId, message.EmailTemplateId);
        Assert.Equal("SENT", message.Status);

        var link = Assert.Single(await db.SentEmailAttachments.AsNoTracking()
            .Where(a => a.SentEmailId == message.SentEmailId).ToListAsync());
        Assert.Equal(PEMS.Domain.Enums.EmailAttachmentType.ATTACHMENT, link.AttachmentType);
        Assert.Null(link.ContentId);
        Assert.StartsWith("PEMS_Department_Invoice_", link.DisplayName);

        var file = await db.Files.AsNoTracking().SingleAsync(f => f.FileId == link.FileId);
        Assert.Equal("application/pdf", file.MimeType);
        Assert.Equal(FilePurposeDbValues.ReportAttachment, file.FilePurpose);
        Assert.True(file.FileSize > 0);

        var stored = await File.ReadAllBytesAsync(
            Path.Combine(_storageRoot, file.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(stored, 0, 5));

        var eml = new EmlMessage(await File.ReadAllTextAsync(Assert.Single(Messages())));
        Assert.Equal(1, eml.AddressCount("To"));
        Assert.Equal(string.Empty, eml.Header("Cc"));
        Assert.Equal(string.Empty, eml.Header("Bcc"));
        Assert.Contains("application/pdf", eml.Raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("attachment", eml.Raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cid:", eml.Raw, StringComparison.OrdinalIgnoreCase);
    }

    // ── Mandatory reaches the API, not just the handler ─────────────────────

    /// <summary>
    /// The default Testing config has SMTP off, which is a Skipped delivery. For a user who pressed
    /// "gửi hóa đơn" that is not success, so the route must not answer 200.
    /// </summary>
    [Fact]
    public async Task A_send_that_never_reached_a_provider_is_not_reported_as_success()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var response = await PlainClient(AsStaffLeader)
            .PostAsJsonAsync(StaffLeaderUrl(DeptId), InvoicePayload());

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Messages());
    }

    [Fact]
    public async Task An_invoice_with_no_lines_is_refused_before_anything_is_generated()
    {
        using var scope = _factory.Services.CreateScope();
        var db = await SeedAsync(scope);

        var response = await WorkingProviderClient(AsDeptLeader).PostAsJsonAsync(
            DeptLeaderUrl, new { fromDate = "2026-07-01", toDate = "2026-07-31", items = Array.Empty<object>() });

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Messages());
        Assert.Empty(await db.Files.AsNoTracking()
            .Where(f => f.FilePurpose == FilePurposeDbValues.ReportAttachment
                        && f.UploadedBy >= Base && f.UploadedBy <= Base + 100)
            .ToListAsync());
    }
}
