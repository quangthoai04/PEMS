using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Idempotency;
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
    /// The guest who submitted the request. This suite is about invoice routing, not about the
    /// confirmation gate, so the campus is seeded self-matched (registrant = operational contact) and
    /// therefore already past the gate.
    /// </summary>
    private const ulong RegistrantId = Base + 13;

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
        // One key per client, and every send in this file builds its own client — so each request is its
        // own attempt, which is what these route tests are about. Without it the six send routes answer
        // 400 EMAIL_IDEMPOTENCY_KEY_REQUIRED (G11 / R-103); that refusal has its own tests below rather
        // than being the incidental reason every other test fails.
        client.DefaultRequestHeaders.Add(IdempotencyKey.HeaderName, Guid.NewGuid().ToString());

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

        // primary_campus_id is nullable because a VISITOR must not carry one — trg_users_validate_bi
        // refuses it, and the registrant seeded below is a VISITOR.
        async Task User(ulong id, string name, string roleCode, string? subRole, ulong? campusId, ulong? deptId)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, department_id, status) "
                + $"VALUES ({id}, {{0}}, {{1}}, {Role(roleCode)}, {Sub(subRole)}, {Num(campusId)}, {Num(deptId)}, 'ACTIVE')",
                name, Mail(id));

        await User(StaffLeaderId, "PEMS B9fix Staff Leader", RoleCode.Staff, SubRole.Leader, CampusId, IcDeptId);
        await User(DeptLeaderId, "PEMS B9fix Trưởng phòng", RoleCode.Department, SubRole.Leader, CampusId, DeptId);
        await User(DeptStaffId, "PEMS B9fix Nhân sự phòng", RoleCode.Department, SubRole.Staff, CampusId, DeptId);
        await User(HoId, "PEMS B9fix Head Office", RoleCode.Ho, null, CampusId, null);
        await User(RegistrantId, "PEMS B9fix Người đăng ký", RoleCode.Visitor, null, null, null);

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
            + "registrant_user_id, registrant_full_name, registrant_organization, registrant_job_title, "
            + "registrant_phone, registrant_email, registrant_nationality) "
            + "VALUES ({0}, {1}, 'PENDING_APPROVAL', NOW(), {3}, 'B9fix Người đăng ký', 'B9fix Org', "
            + "'B9fix Title', '0900000000', {2}, 'Việt Nam')",
            VisitRequestId, "B9FIX-REQ", Mail(RegistrantId), RegistrantId);

        // Self-matched contact: the registrant is this campus's operational contact, so the campus sits
        // past the confirmation gate. A campus beyond WAITING_CONTACT_CONFIRMATION with a NULL
        // operational_contact_user_id is refused by trg_visit_campuses_op_contact_guard_bi.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_request_campuses (visit_instance_id, visit_request_id, campus_id, status, "
            + "operational_contact_user_id, operational_contact_confirmed_at, operational_contact_confirmation_source, "
            + "planned_start_at, planned_end_at, created_at) "
            + "VALUES ({0}, {1}, {2}, 'WAITING_REQUEST_APPROVAL', {5}, NOW(), 'REGISTRANT_SELF_MATCH', {3}, {4}, NOW())",
            VisitInstanceId, VisitRequestId, CampusId,
            new DateTime(2026, 7, 10, 9, 0, 0), new DateTime(2026, 7, 10, 11, 30, 0), RegistrantId);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_logistics_items (logistics_item_id, visit_instance_id, title, item_type, "
            + "quantity, requested_to_department_id, status, created_at) "
            + "VALUES ({0}, {1}, {2}, 'OTHER', 2, {3}, 'DONE', NOW())",
            LogisticsItemId, VisitInstanceId, "Thuê màn LED", DeptId);

        return db;
    }

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    /// <summary>
    /// Removes the fixture's rows by following the database's own foreign keys.
    ///
    /// <para>
    /// The hand-written version of this listed eleven statements in the order the schema needed on the
    /// day it was written, and one omission was enough to take the whole class down: nothing deleted
    /// <c>visit_guest_members</c>, so once another suite left one behind, deleting the visit request was
    /// refused and all twenty-six tests failed in setup. <see cref="FixtureCleanup"/> derives the order
    /// instead, and a foreign key added later is covered without anyone remembering to come back here.
    /// </para>
    /// <para>
    /// <c>email_send_idempotency</c>, <c>user_sessions</c>, <c>visit_request_campuses</c> and
    /// <c>visit_logistics_items</c> are no longer named: each is reached from one of the roots below.
    /// The old <c>UPDATE departments SET head_user_id = NULL</c> is gone too — that column carries no
    /// foreign key, and these departments are deleted here anyway.
    /// </para>
    /// </summary>
    private static Task CleanupAsync(ApplicationDbContext db)
        => FixtureCleanup.For(db)
            // Identified through their recipients, so the ids are resolved before those recipients go.
            .Root("sent_emails",
                "sent_email_id IN (SELECT sent_email_id FROM sent_email_recipients "
                + $"WHERE recipient_email LIKE '{MailPrefix}%{MailDomain}')")
            // Before users: files.uploaded_by is ON DELETE SET NULL, and a blanked column no longer
            // identifies the row.
            .Root("files", $"file_purpose = 'REPORT_ATTACHMENT' AND uploaded_by BETWEEN {Base} AND {Base + 100}")
            .Root("visit_requests", $"visit_request_id = {VisitRequestId}")
            .Root("users", $"user_id BETWEEN {Base} AND {Base + 100}")
            .Root("departments", $"department_id BETWEEN {Base} AND {Base + 100}")
            .Root("campuses", $"campus_id BETWEEN {Base} AND {Base + 100}")
            .RunAsync();

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

    // ── G11 / R-103, over real HTTP ─────────────────────────────────────────
    //
    // These two routes have no frontend caller, so HTTP is the only place their contract can be shown.
    // The suites above prove the idempotency state machine against the handlers; these prove that the
    // header reaches it, and that a caller who omits it is refused at the door rather than served.

    /// <summary>Both invoice routes refuse a request with no <c>Idempotency-Key</c>, and send nothing.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_invoice_send_with_no_idempotency_key_is_refused(bool staffLeaderRoute)
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var client = WorkingProviderClient(staffLeaderRoute ? AsStaffLeader : AsDeptLeader);
        client.DefaultRequestHeaders.Remove(IdempotencyKey.HeaderName);

        var response = await client.PostAsJsonAsync(
            staffLeaderRoute ? StaffLeaderUrl(DeptId) : DeptLeaderUrl, InvoicePayload());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(EmailErrorCodes.IdempotencyKeyRequired, await response.Content.ReadAsStringAsync());
        Assert.Empty(Messages());
    }

    /// <summary>
    /// The same request twice with the same key over HTTP: one message, and the second call answers with
    /// the first call's result rather than doing the work again.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_invoice_send_repeated_with_the_same_key_produces_one_message(bool staffLeaderRoute)
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var url = staffLeaderRoute ? StaffLeaderUrl(DeptId) : DeptLeaderUrl;
        var client = WorkingProviderClient(staffLeaderRoute ? AsStaffLeader : AsDeptLeader);

        var first = await client.PostAsJsonAsync(url, InvoicePayload());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Single(Messages());

        // Same client, so the same key travels again — a browser retry, not a new send.
        var replay = await client.PostAsJsonAsync(url, InvoicePayload());

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Single(Messages());
    }

    /// <summary>The same key with an edited invoice is refused over HTTP, and sends nothing further.</summary>
    [Fact]
    public async Task An_invoice_send_reusing_a_key_for_a_different_request_is_refused()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        var client = WorkingProviderClient(AsDeptLeader);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(DeptLeaderUrl, InvoicePayload())).StatusCode);
        Assert.Single(Messages());

        var edited = await client.PostAsJsonAsync(DeptLeaderUrl, new
        {
            fromDate = "2026-07-01",
            toDate = "2026-07-31",
            note = "Đã sửa nội dung",
            items = new[] { new { logisticsItemId = LogisticsItemId, unitPrice = 999_000m } },
        });

        Assert.Equal(HttpStatusCode.Conflict, edited.StatusCode);
        Assert.Contains(EmailErrorCodes.IdempotencyKeyReused, await edited.Content.ReadAsStringAsync());
        Assert.Single(Messages());
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

    /// <summary>
    /// The three scaffolded dashboard endpoints sit on the same controller. They are not built, but the
    /// class-level <c>[Authorize]</c> has to hold regardless — an unimplemented endpoint that answers an
    /// anonymous caller is a door left open for whoever implements it. They carry no
    /// <c>[RoleAuthorize]</c>; that is tracked as debt (R-104) and is deliberately NOT asserted here,
    /// because a test that locks in a missing role gate would have to be deleted to fix it.
    /// </summary>
    [Theory]
    [InlineData("GET", "/api/reports/viewdashboardstatistics")]
    [InlineData("POST", "/api/reports/exportstatisticsreport")]
    [InlineData("GET", "/api/reports/filterdashboardbytime")]
    public async Task An_anonymous_caller_is_challenged_on_the_scaffolded_dashboard_routes(
        string method, string url)
    {
        var client = PlainClient(Anonymous);

        var response = method == "GET"
            ? await client.GetAsync(url)
            : await client.PostAsJsonAsync(url, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// R-104 option B. The three routes are declared but unbuilt, so they answer <b>501 Not
    /// Implemented</b> with a stable code — not 500, which is what an unhandled
    /// <c>NotImplementedException</c> produced and which is indistinguishable from a real fault.
    ///
    /// <para>
    /// Every authenticated role gets the same answer on purpose: the role contract for these endpoints
    /// is still an open owner decision, and 501-for-everyone settles none of it.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("GET", "/api/reports/viewdashboardstatistics")]
    [InlineData("POST", "/api/reports/exportstatisticsreport")]
    [InlineData("GET", "/api/reports/filterdashboardbytime")]
    public async Task The_unbuilt_dashboard_routes_answer_501_rather_than_failing(string method, string url)
    {
        using var scope = _factory.Services.CreateScope();
        await SeedAsync(scope);

        foreach (var authenticate in new Action<HttpClient>[] { AsHo, AsStaffLeader, AsDeptLeader, AsDeptStaff })
        {
            var client = PlainClient(authenticate);

            var response = method == "GET"
                ? await client.GetAsync(url)
                : await client.PostAsJsonAsync(url, new { });

            Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.False(body.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(
                PEMS.Api.Controllers.ReportsController.DashboardNotImplementedErrorCode,
                body.RootElement.GetProperty("errorCode").GetString());
            Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("message").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
        }
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
