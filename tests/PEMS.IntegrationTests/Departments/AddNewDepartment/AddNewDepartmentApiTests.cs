using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Security;
using PEMS.Application.Departments.Commands.AddNewDepartment;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Departments.AddNewDepartment;

/// <summary>
/// Integration tests for UC-101 Add New Department (Staff Leader).
///
/// Source-confirmed facts (see AddNewDepartmentCommand/Handler/Validator, DepartmentsController,
/// StaffLeaderDepartmentScope, and docs/Department_Staff_Leader/UC-101_ADD_NEW_DEPARTMENT_STAFF_LEADER.md):
/// - Real endpoint: POST /api/departments/addnewdepartment (DepartmentsController route is
///   "api/[controller]" + an explicit action-name route per action — NOT the UC doc's
///   "recommended" POST /api/departments; this project convention is shared by every other action
///   on DepartmentsController).
/// - AddNewDepartmentCommand only carries Name. CampusId/DepartmentType/HeadUserId/Status are all
///   server-populated — the handler always uses currentUser.PrimaryCampusId, always creates
///   DepartmentType=GENERAL, Status=ACTIVE, HeadUserId=null. There is no way for the caller to
///   target another campus or department type through this endpoint.
/// - DepartmentsController has no [Authorize]/[RoleAuthorize] attribute at all; the Staff-Leader
///   check happens only inside the handler via StaffLeaderDepartmentScope.EnsureStaffLeaderCampus.
///   Confirmed real (and per the UC doc's own authorization rule, intentional) behavior: an
///   anonymous caller is NOT challenged with 401 — the request reaches the handler, where
///   IsAuthenticated is false, so it is rejected the same way as a wrong-role actor: 403
///   Forbidden. This differs from the FAQ endpoints' 401-for-anonymous convention; tests below are
///   named Anonymous_Forbidden (not Anonymous_Unauthorized) to match real, confirmed behavior.
/// - Name is trimmed AND has internal repeated whitespace collapsed to one space before save
///   (Regex.Replace(name, @"\s+", " ")). Duplicate-name check is case-insensitive within the same
///   campus (Name.ToLower() comparison).
/// </summary>
public sealed class AddNewDepartmentApiTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string Url = "/api/departments/addnewdepartment";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PemsWebApplicationFactory _factory;

    public AddNewDepartmentApiTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseResetHelper.DeleteTestDepartmentsAsync(db, DatabaseResetHelper.AddDepartmentNamePrefix);
    }

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

    private async Task<HttpClient> CreateInactiveCampusStaffLeaderClientAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = await DatabaseResetHelper.EnsureInactiveCampusStaffLeaderAsync(db);
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

    private async Task<int> CountTestDepartmentsAsync(string namePrefix)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Departments.CountAsync(d => d.Name.StartsWith(namePrefix));
    }

    /// <summary>
    /// Counts every row in the departments table, unfiltered. Used for invalid-name payloads
    /// (empty/whitespace) whose value never carries <see cref="DatabaseResetHelper.AddDepartmentNamePrefix"/>
    /// — a prefix-filtered count would miss a garbage row the handler wrongly persisted, since
    /// that row's Name would not start with the prefix either.
    /// </summary>
    private async Task<int> CountAllDepartmentsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Departments.CountAsync();
    }

    // ---- Authorization (full matrix: Anonymous + all 7 non-StaffLeader effective roles —
    // DepartmentsController has no [Authorize]/[RoleAuthorize], the guard is a single handler-only
    // check, so every actor class is verified against real behavior rather than assumed) ----

    // Every payload below carries AddDepartmentNamePrefix, even though these tests only assert
    // the status code: if a real authorization bug ever let one of these roles create a
    // department, DisposeAsync's prefix-based cleanup must still be able to remove it — otherwise
    // the row would leak into pems_test permanently and pollute later runs (e.g. duplicate-name
    // checks, total-count assertions).

    [Fact]
    public async Task Anonymous_Forbidden()
    {
        var client = _factory.CreateClient();
        var name = $"{DatabaseResetHelper.AddDepartmentNamePrefix}anonymous {UniqueToken()}";
        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = name });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Staff);
        var name = $"{DatabaseResetHelper.AddDepartmentNamePrefix}staff {UniqueToken()}";
        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = name });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DepartmentLead_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.DepartmentLead);
        var name = $"{DatabaseResetHelper.AddDepartmentNamePrefix}department-lead {UniqueToken()}";
        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = name });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Department_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Department);
        var name = $"{DatabaseResetHelper.AddDepartmentNamePrefix}department {UniqueToken()}";
        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = name });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Student_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Student);
        var name = $"{DatabaseResetHelper.AddDepartmentNamePrefix}student {UniqueToken()}";
        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = name });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Ho_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var name = $"{DatabaseResetHelper.AddDepartmentNamePrefix}ho {UniqueToken()}";
        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = name });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Admin);
        var name = $"{DatabaseResetHelper.AddDepartmentNamePrefix}admin {UniqueToken()}";
        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = name });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Visitor_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Visitor);
        var name = $"{DatabaseResetHelper.AddDepartmentNamePrefix}visitor {UniqueToken()}";
        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = name });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Happy path / DB state ----------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_ValidPayload_CreatesDepartment()
    {
        var (client, _, campusId) = await CreateStaffLeaderClientAsync();
        var name = $"{DatabaseResetHelper.AddDepartmentNamePrefix}valid {UniqueToken()}";

        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = name });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AddNewDepartmentResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(campusId, body!.CampusId);
        Assert.Equal("GENERAL", body.DepartmentType);
        Assert.Equal("ACTIVE", body.Status);
        Assert.Null(body.HeadUserId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.DepartmentId == body.DepartmentId);
        Assert.NotNull(saved);
        Assert.Equal(campusId, saved!.CampusId);
        Assert.Equal(name, saved.Name);
        Assert.Equal("GENERAL", saved.DepartmentType);
        Assert.Equal("ACTIVE", saved.Status);
        Assert.Null(saved.HeadUserId);
    }

    [Fact]
    public async Task StaffLeader_ValidPayload_SetsCreateAudit()
    {
        var (client, staffLeaderUserId, _) = await CreateStaffLeaderClientAsync();
        var name = $"{DatabaseResetHelper.AddDepartmentNamePrefix}audit {UniqueToken()}";

        var beforeCreate = DateTime.UtcNow.AddSeconds(-2);
        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = name });
        var afterCreate = DateTime.UtcNow.AddSeconds(5);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AddNewDepartmentResponse>(JsonOptions);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Departments.AsNoTracking().FirstAsync(d => d.DepartmentId == body!.DepartmentId);

        Assert.Equal(staffLeaderUserId, saved.CreatedBy);
        Assert.InRange(saved.CreatedAt, beforeCreate, afterCreate);
        Assert.Null(saved.UpdatedAt);
        Assert.Null(saved.UpdatedBy);
    }

    // ---- Validation / no persist ----------------------------------------------------------------

    [Fact]
    public async Task EmptyName_DoesNotPersist()
    {
        var (client, _, _) = await CreateStaffLeaderClientAsync();
        var before = await CountAllDepartmentsAsync();

        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var after = await CountAllDepartmentsAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task WhitespaceName_DoesNotPersist()
    {
        var (client, _, _) = await CreateStaffLeaderClientAsync();
        var before = await CountAllDepartmentsAsync();

        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var after = await CountAllDepartmentsAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task TooLongName_DoesNotPersist()
    {
        var (client, _, _) = await CreateStaffLeaderClientAsync();
        var before = await CountTestDepartmentsAsync(DatabaseResetHelper.AddDepartmentNamePrefix);

        // Prefix + 151 chars still starts with the cleanup prefix, so it is caught even if the
        // validator somehow let it through (defense in depth for cleanup, not expected here).
        var longName = DatabaseResetHelper.AddDepartmentNamePrefix + new string('A', 151);
        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = longName });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var after = await CountTestDepartmentsAsync(DatabaseResetHelper.AddDepartmentNamePrefix);
        Assert.Equal(before, after);
    }

    // ---- Business rule / no persist ----------------------------------------------------------------

    [Fact]
    public async Task DuplicateNameSameCampus_DoesNotPersistSecondRecord()
    {
        var (client, staffLeaderUserId, campusId) = await CreateStaffLeaderClientAsync();
        var baseName = $"{DatabaseResetHelper.AddDepartmentNamePrefix}duplicate {UniqueToken()}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await DatabaseResetHelper.CreateTestDepartmentAsync(
                db, baseName, campusId, "GENERAL", "ACTIVE", staffLeaderUserId);
        }

        // Same name, different case/whitespace — duplicate check is case-insensitive on the
        // normalized name.
        var duplicatePayloadName = "  " + baseName.ToUpperInvariant() + "  ";
        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = duplicatePayloadName });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var matchCount = await assertDb.Departments
            .CountAsync(d => d.CampusId == campusId && d.Name.ToLower() == baseName.ToLower());
        Assert.Equal(1, matchCount);
    }

    [Fact]
    public async Task InactiveCampus_DoesNotPersist()
    {
        var client = await CreateInactiveCampusStaffLeaderClientAsync();
        var name = $"{DatabaseResetHelper.AddDepartmentNamePrefix}inactive-campus {UniqueToken()}";

        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = name });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exists = await db.Departments.AnyAsync(d => d.Name == name);
        Assert.False(exists);
    }

    // ---- Input integrity (UC-101 manual test cases #4/#5: client cannot choose campus/type) ----

    [Fact]
    public async Task ExtraFieldsInPayload_IgnoredUsesOwnCampusAndGeneralType()
    {
        var (client, _, campusId) = await CreateStaffLeaderClientAsync();
        var name = $"{DatabaseResetHelper.AddDepartmentNamePrefix}extra-fields {UniqueToken()}";

        // AddNewDepartmentCommand has no CampusId/DepartmentType/HeadUserId/Status property, so
        // these extra fields can only reach the handler if the binder silently ignores them —
        // proving the client cannot choose campus or type through this endpoint.
        var response = await client.PostAsJsonAsync(Url, new
        {
            name,
            campusId = 999999999,
            departmentType = "IC",
            headUserId = 123456,
            status = "INACTIVE"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AddNewDepartmentResponse>(JsonOptions);
        Assert.Equal(campusId, body!.CampusId);
        Assert.Equal("GENERAL", body.DepartmentType);
        Assert.Equal("ACTIVE", body.Status);
        Assert.Null(body.HeadUserId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Departments.AsNoTracking().FirstAsync(d => d.DepartmentId == body.DepartmentId);
        Assert.Equal(campusId, saved.CampusId);
        Assert.Equal("GENERAL", saved.DepartmentType);
        Assert.Equal("ACTIVE", saved.Status);
        Assert.Null(saved.HeadUserId);
    }

    [Fact]
    public async Task Name_TrimmedAndCollapsedBeforeSave()
    {
        var (client, _, _) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var trimmedPrefix = DatabaseResetHelper.AddDepartmentNamePrefix.Trim();
        var rawName = $"  {trimmedPrefix}   spaced-name   {token}  ";
        var expectedName = $"{trimmedPrefix} spaced-name {token}";

        var response = await client.PostAsJsonAsync(Url, new AddNewDepartmentCommand { Name = rawName });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AddNewDepartmentResponse>(JsonOptions);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Departments.AsNoTracking().FirstAsync(d => d.DepartmentId == body!.DepartmentId);
        Assert.Equal(expectedName, saved.Name);
    }
}
