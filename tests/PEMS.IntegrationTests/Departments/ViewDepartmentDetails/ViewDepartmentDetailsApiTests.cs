using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Security;
using PEMS.Application.Departments.Common;
using PEMS.Application.Departments.Queries.ViewDepartmentDetails;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Departments.ViewDepartmentDetails;

/// <summary>
/// Integration tests for UC-105 View Department Details (Staff Leader).
///
/// Source-confirmed facts (see ViewDepartmentDetailsQuery/Handler/Dto, StaffLeaderDepartmentScope,
/// DepartmentErrorCodes, ExceptionHandlingMiddleware):
/// - Real endpoint: GET /api/departments/viewdepartmentdetails?departmentId={id}.
/// - Two authorization layers since 2026-08-05: anonymous gets 401 from the API-wide fallback
///   policy, and an authenticated wrong-role caller gets 403 + DepartmentManagementForbidden.
///   This endpoint's gate admits three roles (StaffLeader, DepartmentLead, Department) because the
///   handler distinguishes them; a DepartmentLead therefore passes the gate and is refused by
///   StaffLeaderDepartmentScope INSIDE the handler — with the same code, which is the point.
///   A Staff Leader whose claims never carried a campus gets 422 (NoCampusAssigned).
/// - <see cref="ViewDepartmentDetailsQuery.DepartmentId"/> is a plain <c>ulong</c> with no
///   FluentValidation validator: a missing querystring value binds to 0 and reaches the handler,
///   which then throws a generic <c>NotFoundException("Department", 0)</c> (404, no errorCode) —
///   NOT a 400. Only a genuinely non-numeric value (e.g. "abc") fails ASP.NET Core model binding
///   and short-circuits to 400 before the handler ever runs.
/// - 404 (NotFoundException) carries NO errorCode (unlike DepartmentErrorCodes.DepartmentNotFound,
///   which exists in the enum but is never thrown by this handler) — message format is
///   "{entity} ({key}) was not found.".
/// - Cross-campus existing department -> 403 DepartmentScopeForbidden (only reachable with a REAL
///   department id in another campus; a random nonexistent id would hit the 404 branch instead,
///   since the campus-scope check runs only after a successful lookup).
/// - CanEditName / CanToggleStatus are true only when DepartmentType == "GENERAL"; both false for
///   "IC". Cannot seed a second ACTIVE IC department in the Staff Leader's own campus (DB trigger
///   trg_departments_one_ic_bi/bu allows only one per campus) so IC coverage reads the campus's
///   real, pre-existing IC department read-only (no write, nothing to corrupt/clean up).
/// - Read-only: the handler only ever queries (AsNoTracking projection), never writes.
/// </summary>
public sealed class ViewDepartmentDetailsApiTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string Url = "/api/departments/viewdepartmentdetails";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PemsWebApplicationFactory _factory;

    public ViewDepartmentDetailsApiTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseResetHelper.DeleteTestDepartmentsAsync(db, DatabaseResetHelper.ViewDepartmentDetailsNamePrefix);
    }

    private sealed record ErrorResponse(bool Success, string? ErrorCode, string? Message, string? TraceId);

    private async Task<(HttpClient Client, ulong CampusId)> CreateStaffLeaderClientAsync()
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

        return (client, campusId);
    }

    /// <summary>
    /// A valid, ACTIVE Staff Leader whose request simply never carries a PrimaryCampusId claim —
    /// simulating a session/token that lost its campus claim, without violating the
    /// users.department_id/sub_role DB trigger (the underlying DB user is untouched and still has
    /// a real campus; only the HTTP claim sent for this specific request omits it).
    /// </summary>
    private async Task<HttpClient> CreateStaffLeaderWithoutCampusClaimClientAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.StaffLeader);
        var sessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, userId, EffectiveRole.StaffLeader);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, RoleCode.Staff);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubRoleHeader, SubRole.Leader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, sessionId.ToString());
        // PrimaryCampusIdHeader / DepartmentIdHeader deliberately omitted.
        return client;
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

    private static string UniqueToken() => Guid.NewGuid().ToString("N");

    private async Task<ulong> SeedDepartmentAsync(string name, ulong campusId, string status, ulong? headUserId = null, string departmentType = "GENERAL")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await DatabaseResetHelper.CreateTestDepartmentAsync(db, name, campusId, departmentType, status, headUserId: headUserId);
    }

    private static string BuildUrl(ulong? departmentId) =>
        departmentId is null ? Url : $"{Url}?departmentId={departmentId}";

    private async Task<ulong> GetGuaranteedNonExistingDepartmentIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var maxId = await db.Departments.AsNoTracking().MaxAsync(d => (ulong?)d.DepartmentId) ?? 0;
        return maxId + 1_000_000;
    }

    // ---- Authorization (full matrix: Anonymous + all 7 non-StaffLeader effective roles, plus the
    // Staff-Leader-without-campus-claim edge case) --------------------------------------------------

    /// <summary>
    /// No token means 401, not 403. See SearchFilterDepartmentsApiTests.Anonymous_Unauthorized for
    /// why this changed on 2026-08-05.
    /// </summary>
    [Fact]
    public async Task Anonymous_Unauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(BuildUrl(1));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Staff);
        var response = await client.GetAsync(BuildUrl(1));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task DepartmentLead_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.DepartmentLead);
        var response = await client.GetAsync(BuildUrl(1));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Department_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Department);
        var response = await client.GetAsync(BuildUrl(1));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Student_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Student);
        var response = await client.GetAsync(BuildUrl(1));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    // Important regression guard: legacy docs once granted HO read access to this endpoint.
    [Fact]
    public async Task Ho_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var response = await client.GetAsync(BuildUrl(1));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Admin_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Admin);
        var response = await client.GetAsync(BuildUrl(1));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Visitor_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Visitor);
        var response = await client.GetAsync(BuildUrl(1));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task StaffLeader_WithoutCampusClaim_UnprocessableEntity()
    {
        var client = await CreateStaffLeaderWithoutCampusClaimClientAsync();
        var response = await client.GetAsync(BuildUrl(1));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.NoCampusAssigned, body!.ErrorCode);
    }

    // ---- Input/model binding/not found --------------------------------------------------------

    [Fact]
    public async Task MalformedDepartmentId_BadRequest()
    {
        var (client, _) = await CreateStaffLeaderClientAsync();

        var response = await client.GetAsync($"{Url}?departmentId=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Source contract: DepartmentId has no [Required]/validator, ulong defaults to 0, so a missing
    // querystring value reaches the handler as DepartmentId=0 and falls into the generic 404 path
    // (NOT 400). If a validator is added later, this test must be updated to match and the change
    // reported.
    [Fact]
    public async Task StaffLeader_MissingDepartmentId_NotFound()
    {
        var (client, _) = await CreateStaffLeaderClientAsync();

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Null(body.ErrorCode);
        Assert.Contains("Department", body.Message);
        Assert.Contains("0", body.Message);
    }

    [Fact]
    public async Task StaffLeader_NonExistingDepartment_NotFoundWithoutErrorCode()
    {
        var (client, _) = await CreateStaffLeaderClientAsync();
        var nonExistingId = await GetGuaranteedNonExistingDepartmentIdAsync();

        var response = await client.GetAsync(BuildUrl(nonExistingId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Null(body.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
        Assert.Contains("Department", body.Message);
        Assert.False(string.IsNullOrWhiteSpace(body.TraceId));
    }

    // ---- Campus scope (security) ---------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_OtherCampusDepartment_ForbiddenWithScopeErrorCode()
    {
        var (client, ownCampusId) = await CreateStaffLeaderClientAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var otherCampusId = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active && c.CampusId != ownCampusId)
            .Select(c => c.CampusId)
            .FirstAsync();

        var token = UniqueToken();
        var otherCampusDepartmentId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.ViewDepartmentDetailsNamePrefix}other-campus {token}", otherCampusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(otherCampusDepartmentId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal(DepartmentErrorCodes.DepartmentScopeForbidden, body.ErrorCode);
    }

    [Fact]
    public async Task StaffLeader_ExactId_ReturnsRequestedDepartment_NotSameCampusDistractor()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var targetName = $"{DatabaseResetHelper.ViewDepartmentDetailsNamePrefix}target {token}";
        var distractorName = $"{DatabaseResetHelper.ViewDepartmentDetailsNamePrefix}distractor {token}";
        var targetId = await SeedDepartmentAsync(targetName, campusId, "ACTIVE");
        var distractorId = await SeedDepartmentAsync(distractorName, campusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(targetId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ViewDepartmentDetailsDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(targetId, result!.DepartmentId);
        Assert.Equal(targetName, result.Name);
        Assert.NotEqual(distractorId, result.DepartmentId);
        Assert.NotEqual(distractorName, result.Name);
    }

    // ---- Core projection ------------------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_GeneralActiveDepartment_ReturnsFullDetailAndEditableFlags()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var name = $"{DatabaseResetHelper.ViewDepartmentDetailsNamePrefix}general-active {token}";
        var departmentId = await SeedDepartmentAsync(name, campusId, "ACTIVE");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var campus = await db.Campuses.AsNoTracking().FirstAsync(c => c.CampusId == campusId);

        var response = await client.GetAsync(BuildUrl(departmentId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ViewDepartmentDetailsDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(departmentId, result!.DepartmentId);
        Assert.Equal(name, result.Name);
        Assert.Equal(campusId, result.CampusId);
        Assert.Equal(campus.CampusCode, result.CampusCode);
        Assert.Equal(campus.Name, result.CampusName);
        Assert.Equal("ACTIVE", result.Status);
        Assert.Equal("GENERAL", result.DepartmentType);
        Assert.True(result.CanEditName);
        Assert.True(result.CanToggleStatus);
        Assert.Null(result.HeadUserId);
        Assert.Null(result.HeadFullName);
    }

    [Fact]
    public async Task StaffLeader_GeneralInactiveDepartment_IsStillViewable()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var departmentId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.ViewDepartmentDetailsNamePrefix}general-inactive {token}", campusId, "INACTIVE");

        var response = await client.GetAsync(BuildUrl(departmentId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ViewDepartmentDetailsDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(departmentId, result!.DepartmentId);
        Assert.Equal("INACTIVE", result.Status);
        Assert.Equal("GENERAL", result.DepartmentType);
        Assert.True(result.CanEditName);
        Assert.True(result.CanToggleStatus);
    }

    // Cannot seed a second ACTIVE IC department in the Staff Leader's own campus (DB trigger
    // trg_departments_one_ic_bi/bu allows only one per campus), and View Department Details is
    // read-only so there is no risk in simply reading the campus's real, pre-existing IC
    // department — no write, no cleanup needed, nothing to corrupt. Head fields are intentionally
    // not asserted since a live-seeded IC department may already carry a real head assignment.
    [Fact]
    public async Task StaffLeader_IcDepartment_IsViewableButNotEditableOrToggleable()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var icDepartmentId = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == campusId && d.DepartmentType == "IC" && d.Status == EntityStatuses.Active)
            .Select(d => d.DepartmentId)
            .FirstAsync();

        var response = await client.GetAsync(BuildUrl(icDepartmentId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ViewDepartmentDetailsDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(icDepartmentId, result!.DepartmentId);
        Assert.Equal(campusId, result.CampusId);
        Assert.Equal("IC", result.DepartmentType);
        Assert.False(result.CanEditName);
        Assert.False(result.CanToggleStatus);
    }

    [Fact]
    public async Task StaffLeader_UnassignedHead_ReturnsNullHeadFields()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var departmentId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.ViewDepartmentDetailsNamePrefix}no-head {token}", campusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(departmentId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var headUserId = document.RootElement.GetProperty("headUserId");
        var headFullName = document.RootElement.GetProperty("headFullName");

        // Assert real JSON null, not an empty-string/placeholder fallback — that responsibility
        // belongs to the frontend, not this DTO.
        Assert.Equal(JsonValueKind.Null, headUserId.ValueKind);
        Assert.Equal(JsonValueKind.Null, headFullName.ValueKind);
    }

    [Fact]
    public async Task StaffLeader_AssignedHead_ReturnsHeadIdentity()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();

        // Reuse the existing deterministic DepartmentLead test user as head — guaranteed to be in
        // the same campus as the Staff Leader (both resolve to the first active campus in
        // EnsureTestUserAsync), and reused/idempotent rather than a fresh throwaway user.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var headUserId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.DepartmentLead);
        var headUser = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == headUserId);

        var name = $"{DatabaseResetHelper.ViewDepartmentDetailsNamePrefix}assigned-head {token}";
        var departmentId = await SeedDepartmentAsync(name, campusId, "ACTIVE", headUserId);

        var response = await client.GetAsync(BuildUrl(departmentId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ViewDepartmentDetailsDto>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(departmentId, result!.DepartmentId);
        Assert.Equal(headUserId, result.HeadUserId);
        Assert.Equal(headUser.FullName, result.HeadFullName);
        Assert.NotEqual(name, result.HeadFullName);
    }

    // ---- Response minimization/security ----------------------------------------------------------

    [Fact]
    public async Task StaffLeader_Response_ContainsOnlyExpectedPublicFields()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var departmentId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.ViewDepartmentDetailsNamePrefix}fields {token}", campusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(departmentId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var actualNames = document.RootElement
            .EnumerateObject()
            .Select(p => p.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var expectedNames = new[]
        {
            "canEditName", "canToggleStatus", "campusCode", "campusId", "campusName",
            "departmentId", "departmentType", "headFullName", "headUserId", "name", "status",
        }.OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.Equal(expectedNames, actualNames);

        var forbidden = new[]
        {
            "email", "phone", "passwordHash", "roleCode", "subRole", "users",
            "createdAt", "createdBy", "updatedAt", "updatedBy",
        };
        foreach (var field in forbidden)
            Assert.DoesNotContain(field, actualNames);
    }

    // ---- Read-only --------------------------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_ViewDetails_DoesNotModifyDepartment()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var departmentId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.ViewDepartmentDetailsNamePrefix}readonly {token}", campusId, "ACTIVE");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var before = await db.Departments.AsNoTracking().SingleAsync(d => d.DepartmentId == departmentId);
        var beforeTestRowCount = await db.Departments.CountAsync(d => d.Name.StartsWith(DatabaseResetHelper.ViewDepartmentDetailsNamePrefix));

        var response = await client.GetAsync(BuildUrl(departmentId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var after = await assertDb.Departments.AsNoTracking().SingleAsync(d => d.DepartmentId == departmentId);
        var afterTestRowCount = await assertDb.Departments.CountAsync(d => d.Name.StartsWith(DatabaseResetHelper.ViewDepartmentDetailsNamePrefix));

        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.CampusId, after.CampusId);
        Assert.Equal(before.DepartmentType, after.DepartmentType);
        Assert.Equal(before.HeadUserId, after.HeadUserId);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(before.CreatedBy, after.CreatedBy);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Equal(before.UpdatedBy, after.UpdatedBy);
        Assert.Equal(beforeTestRowCount, afterTestRowCount);
    }
}
