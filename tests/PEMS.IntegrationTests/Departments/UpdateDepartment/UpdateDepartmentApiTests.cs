using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Security;
using PEMS.Application.Departments.Commands.UpdateDepartment;
using PEMS.Application.Departments.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;
using PEMS.Application.Common;

namespace PEMS.IntegrationTests.Departments.UpdateDepartment;

/// <summary>
/// Integration tests for UC-102 Update Department (Staff Leader).
///
/// Source-confirmed facts (see UpdateDepartmentCommand/Handler/Validator, DepartmentsController,
/// StaffLeaderDepartmentScope):
/// - Real endpoint: POST /api/departments/updatedepartment (same controller convention as
///   AddNewDepartment — NOT PUT /api/departments/{departmentId} as the prompt's generic template
///   guessed). DepartmentId is a field in the JSON body, not a route segment.
/// - UpdateDepartmentCommand only carries DepartmentId + Name. There is no DepartmentType,
///   HeadUserId or Status field — campus, type, head and status are never touched by this UC
///   (confirmed by the handler's own doc comment: "ONLY the department name may change").
/// - Same handler-only authorization as UC-101: DepartmentsController has no
///   [Authorize]/[RoleAuthorize]; StaffLeaderDepartmentScope.EnsureStaffLeaderCampus rejects
///   anonymous and wrong-role actors identically with 403 (not 401) — tests below use
///   Anonymous_Forbidden, matching real confirmed behavior. Every Forbidden test also asserts the
///   response's <c>errorCode</c> equals DepartmentManagementForbidden (not just the HTTP status),
///   so a future regression that reaches a *different* 403 (e.g. campus-scope) via a broken role
///   guard cannot masquerade as this test passing for the right reason.
/// - Handler enforces, in order: (1) role/scope via EnsureStaffLeaderCampus -> 403
///   (DepartmentManagementForbidden); (2) department exists -> else 404 (NotFoundException);
///   (3) department.CampusId == caller's campus -> else 403 (DepartmentScopeForbidden); (4)
///   department.DepartmentType == "GENERAL" -> else 409 (DepartmentIcNotEditable) — the default IC
///   department can never be renamed through this UC; (5) no-op short circuit when the
///   trimmed/collapsed name is byte-identical (Ordinal) to the current name -> Changed=false, no
///   write, no audit (AF-07); (6) duplicate name check within the same campus, case-insensitive,
///   excluding self -> else 409 (DepartmentNameAlreadyExists).
/// - Name is trimmed AND has internal repeated whitespace collapsed to one space before save
///   (same Regex.Replace(name, @"\s+", " ") as AddNewDepartment).
///
/// The IC-protection test (IcDepartment_DoesNotModify) deliberately does NOT target a real seed
/// campus's IC department: if a future regression ever let that update through, the department's
/// name would gain the UpdateDepartmentNamePrefix and DisposeAsync's cleanup would delete it —
/// catastrophic for a real campus's default IC department. Instead it uses a dedicated, isolated
/// test-only campus + IC department (DatabaseResetHelper.EnsureIcProtectionTestContextAsync), so
/// even a worst-case bug only ever touches disposable test data.
/// </summary>
public sealed class UpdateDepartmentApiTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string Url = "/api/departments/updatedepartment";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PemsWebApplicationFactory _factory;

    public UpdateDepartmentApiTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseResetHelper.DeleteTestDepartmentsAsync(db, DatabaseResetHelper.UpdateDepartmentNamePrefix);
    }

    private sealed record ErrorResponse(bool Success, string? ErrorCode, string? Message, string? TraceId);

    private async Task<(HttpClient Client, ulong UserId, ulong CampusId)> CreateStaffLeaderClientAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.StaffLeader);
        var sessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, userId, EffectiveRole.StaffLeader);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == userId);
        var campusId = user.PrimaryCampusId!.Value;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, RoleCode.Staff);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubRoleHeader, SubRole.Leader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, sessionId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.PrimaryCampusIdHeader, campusId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.DepartmentIdHeader, user.DepartmentId!.Value.ToString());

        return (client, userId, campusId);
    }

    private async Task<HttpClient> CreateClientAsAsync(string effectiveRole)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = await DatabaseResetHelper.EnsureTestUserAsync(db, effectiveRole);
        var sessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, userId, effectiveRole);

        var (roleCode, subRole) = effectiveRole switch
        {
            EffectiveRole.Ho => (RoleCode.Ho, (string?)null),
            EffectiveRole.Admin => (RoleCode.Admin, (string?)null),
            EffectiveRole.Staff => (RoleCode.Staff, SubRole.Staff),
            EffectiveRole.DepartmentLead => (RoleCode.Department, SubRole.Leader),
            EffectiveRole.Department => (RoleCode.Department, SubRole.Staff),
            EffectiveRole.Student => (RoleCode.Student, (string?)null),
            EffectiveRole.Visitor => (RoleCode.Visitor, (string?)null),
            _ => throw new ArgumentOutOfRangeException(nameof(effectiveRole))
        };

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, roleCode);
        if (subRole is not null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubRoleHeader, subRole);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, sessionId.ToString());

        return client;
    }

    private async Task<HttpClient> CreateIcProtectionStaffLeaderClientAsync(ulong userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, userId, EffectiveRole.StaffLeader);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == userId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, RoleCode.Staff);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubRoleHeader, SubRole.Leader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, sessionId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.PrimaryCampusIdHeader, user.PrimaryCampusId!.Value.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.DepartmentIdHeader, user.DepartmentId!.Value.ToString());

        return client;
    }

    private static string UniqueToken() => Guid.NewGuid().ToString("N");

    private async Task<ulong> SeedDepartmentAsync(string name, ulong campusId, string departmentType, string status, ulong? createdBy = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await DatabaseResetHelper.CreateTestDepartmentAsync(db, name, campusId, departmentType, status, createdBy);
    }

    private async Task<ulong> GetAnyActiveCampusIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Campuses.Where(c => c.Status == EntityStatuses.Active).Select(c => c.CampusId).FirstAsync();
    }

    /// <summary>A DepartmentId guaranteed not to exist right now, regardless of how large the table has grown.</summary>
    private async Task<ulong> GetGuaranteedNonExistingDepartmentIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var maxId = await db.Departments.Select(d => (ulong?)d.DepartmentId).MaxAsync() ?? 0;
        return maxId + 1_000_000;
    }

    private sealed record DepartmentSnapshot(
        ulong CampusId, string Name, string DepartmentType, ulong? HeadUserId, string Status,
        DateTime CreatedAt, ulong? CreatedBy, DateTime? UpdatedAt, ulong? UpdatedBy);

    private async Task<DepartmentSnapshot> SnapshotDepartmentAsync(ulong departmentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var d = await db.Departments.AsNoTracking().SingleAsync(x => x.DepartmentId == departmentId);
        return new DepartmentSnapshot(d.CampusId, d.Name, d.DepartmentType, d.HeadUserId, d.Status, d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy);
    }

    /// <summary>Reloads the department and asserts every field (not just Name) still matches <paramref name="expected"/>.</summary>
    private async Task AssertDepartmentUnchangedAsync(ulong departmentId, DepartmentSnapshot expected)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Departments.AsNoTracking().SingleAsync(x => x.DepartmentId == departmentId);

        Assert.Equal(expected.CampusId, saved.CampusId);
        Assert.Equal(expected.Name, saved.Name);
        Assert.Equal(expected.DepartmentType, saved.DepartmentType);
        Assert.Equal(expected.HeadUserId, saved.HeadUserId);
        Assert.Equal(expected.Status, saved.Status);
        Assert.Equal(expected.CreatedAt, saved.CreatedAt);
        Assert.Equal(expected.CreatedBy, saved.CreatedBy);
        Assert.Equal(expected.UpdatedAt, saved.UpdatedAt);
        Assert.Equal(expected.UpdatedBy, saved.UpdatedBy);
    }

    // ---- Authorization (full matrix: Anonymous + all 7 non-StaffLeader effective roles — same
    // handler-only guard as UC-101, no [Authorize]/[RoleAuthorize] on the controller) ------------

    // Every payload targets a freshly-seeded, prefixed department (not a real seed row) with a
    // prefixed new name, so that if a real authorization bug ever let one of these roles through,
    // DisposeAsync's prefix-based cleanup can still remove any resulting row/rename. Each test also
    // asserts errorCode == DepartmentManagementForbidden (not just the 403 status) and that the
    // seeded department is byte-for-byte unchanged, so a 403 arriving for the *wrong* reason (e.g.
    // a broken role guard that still happens to fail on campus-scope) cannot pass as this test.

    [Fact]
    public async Task Anonymous_Forbidden()
    {
        var campusId = await GetAnyActiveCampusIdAsync();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.UpdateDepartmentNamePrefix}anon-old {UniqueToken()}", campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand
        {
            DepartmentId = departmentId,
            Name = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}anon-new {UniqueToken()}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    [Fact]
    public async Task Staff_Forbidden()
    {
        var campusId = await GetAnyActiveCampusIdAsync();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.UpdateDepartmentNamePrefix}staff-old {UniqueToken()}", campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);
        var client = await CreateClientAsAsync(EffectiveRole.Staff);

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand
        {
            DepartmentId = departmentId,
            Name = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}staff-new {UniqueToken()}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    [Fact]
    public async Task DepartmentLead_Forbidden()
    {
        var campusId = await GetAnyActiveCampusIdAsync();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.UpdateDepartmentNamePrefix}dept-lead-old {UniqueToken()}", campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);
        var client = await CreateClientAsAsync(EffectiveRole.DepartmentLead);

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand
        {
            DepartmentId = departmentId,
            Name = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}dept-lead-new {UniqueToken()}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    [Fact]
    public async Task Department_Forbidden()
    {
        var campusId = await GetAnyActiveCampusIdAsync();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.UpdateDepartmentNamePrefix}dept-old {UniqueToken()}", campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);
        var client = await CreateClientAsAsync(EffectiveRole.Department);

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand
        {
            DepartmentId = departmentId,
            Name = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}dept-new {UniqueToken()}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    [Fact]
    public async Task Student_Forbidden()
    {
        var campusId = await GetAnyActiveCampusIdAsync();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.UpdateDepartmentNamePrefix}student-old {UniqueToken()}", campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);
        var client = await CreateClientAsAsync(EffectiveRole.Student);

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand
        {
            DepartmentId = departmentId,
            Name = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}student-new {UniqueToken()}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    [Fact]
    public async Task Ho_Forbidden()
    {
        var campusId = await GetAnyActiveCampusIdAsync();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.UpdateDepartmentNamePrefix}ho-old {UniqueToken()}", campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand
        {
            DepartmentId = departmentId,
            Name = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}ho-new {UniqueToken()}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    [Fact]
    public async Task Admin_Forbidden()
    {
        var campusId = await GetAnyActiveCampusIdAsync();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.UpdateDepartmentNamePrefix}admin-old {UniqueToken()}", campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);
        var client = await CreateClientAsAsync(EffectiveRole.Admin);

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand
        {
            DepartmentId = departmentId,
            Name = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}admin-new {UniqueToken()}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    [Fact]
    public async Task Visitor_Forbidden()
    {
        var campusId = await GetAnyActiveCampusIdAsync();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.UpdateDepartmentNamePrefix}visitor-old {UniqueToken()}", campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);
        var client = await CreateClientAsAsync(EffectiveRole.Visitor);

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand
        {
            DepartmentId = departmentId,
            Name = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}visitor-new {UniqueToken()}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    // ---- Happy path / DB state ----------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_ValidPayload_UpdatesDepartment()
    {
        var (client, _, campusId) = await CreateStaffLeaderClientAsync();
        var oldName = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}valid-old {UniqueToken()}";
        var newName = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}valid-new {UniqueToken()}";
        var departmentId = await SeedDepartmentAsync(oldName, campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand { DepartmentId = departmentId, Name = newName });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UpdateDepartmentResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(newName, body!.Name);
        Assert.True(body.Changed);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Departments.AsNoTracking().SingleAsync(d => d.DepartmentId == departmentId);

        // Only Name (and audit, covered by the dedicated audit test) may differ from the snapshot.
        Assert.Equal(newName, saved.Name);
        Assert.Equal(snapshot.CampusId, saved.CampusId);
        Assert.Equal(snapshot.DepartmentType, saved.DepartmentType);
        Assert.Equal(snapshot.Status, saved.Status);
        Assert.Equal(snapshot.HeadUserId, saved.HeadUserId);
        Assert.Equal(snapshot.CreatedAt, saved.CreatedAt);
        Assert.Equal(snapshot.CreatedBy, saved.CreatedBy);
    }

    [Fact]
    public async Task StaffLeader_Update_KeepsStatus()
    {
        var (client, _, campusId) = await CreateStaffLeaderClientAsync();
        var oldName = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}keep-status-old {UniqueToken()}";
        var newName = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}keep-status-new {UniqueToken()}";
        // Seeded INACTIVE deliberately (not the AddNewDepartment default) so this test would catch
        // Update Department silently resetting status to ACTIVE.
        var departmentId = await SeedDepartmentAsync(oldName, campusId, "GENERAL", "INACTIVE");

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand { DepartmentId = departmentId, Name = newName });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Departments.AsNoTracking().SingleAsync(d => d.DepartmentId == departmentId);
        // The update must actually have happened — otherwise "status unchanged" would be true
        // trivially even if the rename silently failed.
        Assert.Equal(newName, saved.Name);
        Assert.Equal("INACTIVE", saved.Status);
    }

    [Fact]
    public async Task StaffLeader_ValidPayload_UpdatesAudit()
    {
        var (client, staffLeaderUserId, campusId) = await CreateStaffLeaderClientAsync();
        var oldName = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}audit-old {UniqueToken()}";
        var newName = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}audit-new {UniqueToken()}";
        var departmentId = await SeedDepartmentAsync(oldName, campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);
        Assert.Null(snapshot.UpdatedAt);
        Assert.Null(snapshot.UpdatedBy);

        var beforeUpdate = VietnamTime.Now().AddSeconds(-2);
        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand { DepartmentId = departmentId, Name = newName });
        var afterUpdate = VietnamTime.Now().AddSeconds(5);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Departments.AsNoTracking().SingleAsync(d => d.DepartmentId == departmentId);

        Assert.Equal(staffLeaderUserId, saved.UpdatedBy);
        Assert.NotNull(saved.UpdatedAt);
        Assert.InRange(saved.UpdatedAt!.Value, beforeUpdate, afterUpdate);

        // Create audit must never be touched by an update.
        Assert.Equal(snapshot.CreatedAt, saved.CreatedAt);
        Assert.Equal(snapshot.CreatedBy, saved.CreatedBy);
    }

    [Fact]
    public async Task StaffLeader_ValidPayload_CreatesAuditLog()
    {
        var (client, staffLeaderUserId, campusId) = await CreateStaffLeaderClientAsync();
        var oldName = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}auditlog-old {UniqueToken()}";
        var newName = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}auditlog-new {UniqueToken()}";
        var departmentId = await SeedDepartmentAsync(oldName, campusId, "GENERAL", "ACTIVE");

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand { DepartmentId = departmentId, Name = newName });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var log = await db.AuditLogs
            .Include(l => l.Changes)
            .SingleAsync(l =>
                l.EntityType == "Department" &&
                l.EntityId == departmentId &&
                l.Action == "UPDATE_DEPARTMENT_NAME");

        Assert.Equal((ulong?)staffLeaderUserId, log.ActorUserId);
        Assert.Equal((ulong?)campusId, log.CampusId);

        var change = Assert.Single(log.Changes);
        Assert.Equal("name", change.FieldName);
        Assert.Equal(oldName, change.OldValueText);
        Assert.Equal(newName, change.NewValueText);
    }

    // Confirmed real handler behavior (AF-07): when the trimmed/collapsed name is byte-identical
    // (Ordinal) to the stored name, the handler short-circuits — no write, no audit, Changed=false.
    // The input deliberately carries extra/irregular whitespace (not the byte-identical stored
    // string) so this proves normalization happens BEFORE the no-op comparison, not that the two
    // strings were trivially identical to begin with.
    [Fact]
    public async Task StaffLeader_NoChange_KeepsRecordUnchanged()
    {
        var (client, _, campusId) = await CreateStaffLeaderClientAsync();
        var trimmedPrefix = DatabaseResetHelper.UpdateDepartmentNamePrefix.Trim();
        var token = UniqueToken();
        var storedName = $"{trimmedPrefix} no-change {token}";
        var departmentId = await SeedDepartmentAsync(storedName, campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);

        var whitespaceVariant = $"  {trimmedPrefix}   no-change   {token}  ";
        Assert.NotEqual(storedName, whitespaceVariant); // sanity: genuinely different raw input

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand { DepartmentId = departmentId, Name = whitespaceVariant });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UpdateDepartmentResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(body!.Changed);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    // ---- Validation / no partial update --------------------------------------------------------

    [Fact]
    public async Task DepartmentId_Zero_BadRequest()
    {
        var (client, _, _) = await CreateStaffLeaderClientAsync();
        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand
        {
            DepartmentId = 0,
            Name = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}zero-id {UniqueToken()}"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyName_DoesNotModify()
    {
        var (client, _, campusId) = await CreateStaffLeaderClientAsync();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.UpdateDepartmentNamePrefix}empty-name-old {UniqueToken()}", campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand { DepartmentId = departmentId, Name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    [Fact]
    public async Task WhitespaceName_DoesNotModify()
    {
        var (client, _, campusId) = await CreateStaffLeaderClientAsync();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.UpdateDepartmentNamePrefix}whitespace-name-old {UniqueToken()}", campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand { DepartmentId = departmentId, Name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    [Fact]
    public async Task TooLongName_DoesNotModify()
    {
        var (client, _, campusId) = await CreateStaffLeaderClientAsync();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.UpdateDepartmentNamePrefix}too-long-old {UniqueToken()}", campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);

        // Prefix + 151 chars still starts with the cleanup prefix, so it is caught even if the
        // validator somehow let it through (defense in depth for cleanup, not expected here).
        var tooLongName = DatabaseResetHelper.UpdateDepartmentNamePrefix + new string('A', 151);
        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand { DepartmentId = departmentId, Name = tooLongName });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    // ---- Existence / scope ----------------------------------------------------------------------

    [Fact]
    public async Task NonExistingDepartment_NotFound()
    {
        var (client, _, _) = await CreateStaffLeaderClientAsync();
        var nonExistingId = await GetGuaranteedNonExistingDepartmentIdAsync();

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand
        {
            DepartmentId = nonExistingId,
            Name = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}not-found {UniqueToken()}"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StaffLeader_OtherCampus_Forbidden()
    {
        var (client, _, ownCampusId) = await CreateStaffLeaderClientAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var otherCampusId = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active && c.CampusId != ownCampusId)
            .Select(c => c.CampusId)
            .FirstAsync();

        var oldName = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}other-campus-old {UniqueToken()}";
        var departmentId = await SeedDepartmentAsync(oldName, otherCampusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand
        {
            DepartmentId = departmentId,
            Name = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}other-campus-new {UniqueToken()}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentScopeForbidden, body!.ErrorCode);

        await AssertDepartmentUnchangedAsync(departmentId, snapshot);
    }

    // ---- IC protection (confirmed real business rule, not anticipated by the generic template) --

    [Fact]
    public async Task IcDepartment_DoesNotModify()
    {
        using var setupScope = _factory.Services.CreateScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (userId, _, icDepartmentId) = await DatabaseResetHelper.EnsureIcProtectionTestContextAsync(setupDb);

        var client = await CreateIcProtectionStaffLeaderClientAsync(userId);
        var snapshot = await SnapshotDepartmentAsync(icDepartmentId);

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand
        {
            DepartmentId = icDepartmentId,
            Name = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}attempt-rename-ic {UniqueToken()}"
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentIcNotEditable, body!.ErrorCode);

        await AssertDepartmentUnchangedAsync(icDepartmentId, snapshot);
    }

    // ---- Duplicate rule -------------------------------------------------------------------------

    [Fact]
    public async Task DuplicateNameSameCampus_DoesNotModify()
    {
        var (client, staffLeaderUserId, campusId) = await CreateStaffLeaderClientAsync();
        var nameA = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}dup-a {UniqueToken()}";
        var nameB = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}dup-b {UniqueToken()}";
        var departmentAId = await SeedDepartmentAsync(nameA, campusId, "GENERAL", "ACTIVE", staffLeaderUserId);
        var departmentBId = await SeedDepartmentAsync(nameB, campusId, "GENERAL", "ACTIVE", staffLeaderUserId);
        var snapshotA = await SnapshotDepartmentAsync(departmentAId);
        var snapshotB = await SnapshotDepartmentAsync(departmentBId);

        // Same name as A (different case + surrounding whitespace) — duplicate check is
        // case-insensitive on the normalized name.
        var duplicateAttempt = "  " + nameA.ToUpperInvariant() + "  ";
        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand { DepartmentId = departmentBId, Name = duplicateAttempt });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentNameAlreadyExists, body!.ErrorCode);

        // Neither the rejected target (B) nor the pre-existing owner of the name (A) may change.
        await AssertDepartmentUnchangedAsync(departmentBId, snapshotB);
        await AssertDepartmentUnchangedAsync(departmentAId, snapshotA);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var matchCount = await db.Departments.CountAsync(d => d.CampusId == campusId && d.Name.ToLower() == nameA.ToLower());
        Assert.Equal(1, matchCount);
    }

    // Proves the uniqueness rule is scoped to (campus_id, name), not a global name uniqueness:
    // reusing another campus's department name must be allowed.
    [Fact]
    public async Task SameNameDifferentCampus_Allowed()
    {
        var (client, _, ownCampusId) = await CreateStaffLeaderClientAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var otherCampusId = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active && c.CampusId != ownCampusId)
            .Select(c => c.CampusId)
            .FirstAsync();

        var sharedName = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}shared-name {UniqueToken()}";
        await SeedDepartmentAsync(sharedName, otherCampusId, "GENERAL", "ACTIVE");
        var departmentId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}own-campus-old {UniqueToken()}", ownCampusId, "GENERAL", "ACTIVE");

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand { DepartmentId = departmentId, Name = sharedName });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var saved = await db.Departments.AsNoTracking().SingleAsync(d => d.DepartmentId == departmentId);
        Assert.Equal(sharedName, saved.Name);
        Assert.Equal(ownCampusId, saved.CampusId);
    }

    // Proves the duplicate check excludes the department being updated itself: sending a
    // case/whitespace variant of its OWN current name (which bypasses the Ordinal no-op check,
    // since it differs in case) must still succeed rather than falsely conflicting with itself —
    // and must go through the *real* rename path (audit refreshed), not the no-op path.
    [Fact]
    public async Task SameNameSelf_UpdatesRecord()
    {
        var (client, staffLeaderUserId, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var originalName = $"{DatabaseResetHelper.UpdateDepartmentNamePrefix}self-dup {token}";
        var departmentId = await SeedDepartmentAsync(originalName, campusId, "GENERAL", "ACTIVE");

        var caseVariant = "  " + originalName.ToUpperInvariant() + "  ";
        Assert.NotEqual(originalName, caseVariant.Trim()); // sanity: genuinely different case, not a no-op

        var beforeUpdate = VietnamTime.Now().AddSeconds(-2);
        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand { DepartmentId = departmentId, Name = caseVariant });
        var afterUpdate = VietnamTime.Now().AddSeconds(5);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UpdateDepartmentResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body!.Changed);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Departments.AsNoTracking().SingleAsync(d => d.DepartmentId == departmentId);
        Assert.Equal(originalName.ToUpperInvariant(), saved.Name);
        Assert.Equal(staffLeaderUserId, saved.UpdatedBy);
        Assert.NotNull(saved.UpdatedAt);
        Assert.InRange(saved.UpdatedAt!.Value, beforeUpdate, afterUpdate);
    }

    // ---- Input normalization ----------------------------------------------------------------------

    [Fact]
    public async Task Name_TrimmedAndCollapsedBeforeSave()
    {
        var (client, _, campusId) = await CreateStaffLeaderClientAsync();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.UpdateDepartmentNamePrefix}trim-old {UniqueToken()}", campusId, "GENERAL", "ACTIVE");
        var snapshot = await SnapshotDepartmentAsync(departmentId);

        var token = UniqueToken();
        var trimmedPrefix = DatabaseResetHelper.UpdateDepartmentNamePrefix.Trim();
        var rawName = $"  {trimmedPrefix}   spaced-name   {token}  ";
        var expectedName = $"{trimmedPrefix} spaced-name {token}";

        var response = await client.PostAsJsonAsync(Url, new UpdateDepartmentCommand { DepartmentId = departmentId, Name = rawName });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Departments.AsNoTracking().SingleAsync(d => d.DepartmentId == departmentId);

        Assert.Equal(expectedName, saved.Name);
        // Normalization must not have any side effect on unrelated fields.
        Assert.Equal(snapshot.CampusId, saved.CampusId);
        Assert.Equal(snapshot.DepartmentType, saved.DepartmentType);
        Assert.Equal(snapshot.Status, saved.Status);
        Assert.Equal(snapshot.HeadUserId, saved.HeadUserId);
        Assert.Equal(snapshot.CreatedAt, saved.CreatedAt);
        Assert.Equal(snapshot.CreatedBy, saved.CreatedBy);
    }
}
