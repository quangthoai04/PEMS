using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Security;
using PEMS.Application.Departments.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Departments.ViewDepartmentList;

/// <summary>
/// Integration tests for UC-104 View Department List (Staff Leader).
///
/// Source-confirmed facts (see ViewDepartmentListQuery/Handler, DepartmentListQueryExecutor,
/// IDepartmentListCriteria, DepartmentListItemDto, StaffLeaderDepartmentScope, PaginatedResult):
/// - Real endpoint: GET /api/departments/viewdepartmentlist.
/// - <see cref="ViewDepartmentListQuery"/> is BYTE-FOR-BYTE identical in shape to UC-103's
///   SearchandFilterDepartmentsQuery (Page, PageSize, Keyword, Status, SortBy, SortDirection) —
///   both delegate to the exact same internal DepartmentListQueryExecutor.ExecuteAsync. No
///   campusId/departmentType query parameter exists.
/// - No FluentValidation validator exists. Invalid/out-of-range input is silently
///   clamped/defaulted, never rejected with 400 (see UC-103's tests for that behavior in depth —
///   not re-tested here to avoid duplication; this class focuses on default-list contract,
///   pagination, DTO shape, GENERAL/IC action flags, head projection, campus scope, read-only, and
///   parity with UC-103's shared executor).
/// - Two authorization layers since 2026-08-05: anonymous is stopped by the API-wide fallback policy
///   with 401; an authenticated wrong-role caller is stopped by [RoleAuthorize] with 403
///   (DepartmentManagementForbidden), verified via errorCode, not just status code. A Staff Leader
///   whose session/claims never carried a campus gets 422 (NoCampusAssigned) — verified using a
///   client that deliberately omits the PrimaryCampusId header rather than an internal user that
///   would violate the users.department_id/sub_role DB trigger.
/// - Read-only: DepartmentListQueryExecutor always uses AsNoTracking() and never writes.
/// </summary>
public sealed class ViewDepartmentListApiTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string Url = "/api/departments/viewdepartmentlist";
    private const string SearchFilterUrl = "/api/departments/searchandfilterdepartments";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PemsWebApplicationFactory _factory;

    public ViewDepartmentListApiTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseResetHelper.DeleteTestDepartmentsAsync(db, DatabaseResetHelper.ViewDepartmentListNamePrefix);
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

    private static string BuildUrl(
        string url, string? keyword = null, string? status = null,
        string? sortBy = null, string? sortDirection = null, int? page = null, int? pageSize = null)
    {
        var query = new List<string>();
        if (keyword is not null) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
        if (status is not null) query.Add($"status={Uri.EscapeDataString(status)}");
        if (sortBy is not null) query.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
        if (sortDirection is not null) query.Add($"sortDirection={Uri.EscapeDataString(sortDirection)}");
        if (page is not null) query.Add($"page={page}");
        if (pageSize is not null) query.Add($"pageSize={pageSize}");
        return url + (query.Count > 0 ? "?" + string.Join("&", query) : "");
    }

    // ---- Authorization (full matrix: Anonymous + all 7 non-StaffLeader effective roles, plus the
    // Staff-Leader-without-campus-claim edge case — same handler-only guard as UC-101/102/103) ----

    /// <summary>
    /// No token means 401, not 403. See SearchFilterDepartmentsApiTests.Anonymous_Unauthorized for
    /// why this changed on 2026-08-05: the API-wide fallback policy that closed eight silently-public
    /// controllers stops an anonymous caller at authentication, before any role gate.
    /// </summary>
    [Fact]
    public async Task Anonymous_Unauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(BuildUrl(Url));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Staff);
        var response = await client.GetAsync(BuildUrl(Url));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task DepartmentLead_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.DepartmentLead);
        var response = await client.GetAsync(BuildUrl(Url));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Department_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Department);
        var response = await client.GetAsync(BuildUrl(Url));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Student_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Student);
        var response = await client.GetAsync(BuildUrl(Url));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    // Important regression guard: legacy docs once granted HO read access to this list.
    [Fact]
    public async Task Ho_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var response = await client.GetAsync(BuildUrl(Url));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Admin_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Admin);
        var response = await client.GetAsync(BuildUrl(Url));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Visitor_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Visitor);
        var response = await client.GetAsync(BuildUrl(Url));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task StaffLeader_WithoutCampusClaim_UnprocessableEntity()
    {
        var client = await CreateStaffLeaderWithoutCampusClaimClientAsync();
        var response = await client.GetAsync(BuildUrl(Url));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.NoCampusAssigned, body!.ErrorCode);
    }

    // ---- Default criteria contract ---------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_DefaultCriteria_ReturnsFirstPageSortedByName()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        // Seeded deliberately out of alphabetical order to prove real sorting happens rather than
        // insertion order coincidentally matching.
        var thirdId = await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}c-third {token}", campusId, "ACTIVE");
        var firstId = await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}a-first {token}", campusId, "ACTIVE");
        var secondId = await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}b-second {token}", campusId, "ACTIVE");

        // keyword is used ONLY to isolate this test's own rows from real/background campus data —
        // this test's purpose is the default paging/sort contract, not UC-103's keyword-search
        // behavior (already covered there).
        var response = await client.GetAsync(BuildUrl(Url, keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result!.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(firstId, result.Items[0].DepartmentId);
        Assert.Equal(secondId, result.Items[1].DepartmentId);
        Assert.Equal(thirdId, result.Items[2].DepartmentId);
    }

    // ---- Pagination ---------------------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_Pagination_ReturnsCorrectSlicesAndMetadata()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var ids = new List<ulong>();
        foreach (var label in new[] { "a", "b", "c", "d", "e" })
            ids.Add(await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}{label} {token}", campusId, "ACTIVE"));

        var page1Response = await client.GetAsync(BuildUrl(Url, keyword: token, sortBy: "name", sortDirection: "asc", page: 1, pageSize: 2));
        Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);
        var page1 = await page1Response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(page1);
        Assert.Equal(1, page1!.Page);
        Assert.Equal(2, page1.PageSize);
        Assert.Equal(5, page1.TotalItems);
        Assert.Equal(3, page1.TotalPages);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);
        Assert.Equal(new[] { ids[0], ids[1] }, page1.Items.Select(i => i.DepartmentId));

        var page2Response = await client.GetAsync(BuildUrl(Url, keyword: token, sortBy: "name", sortDirection: "asc", page: 2, pageSize: 2));
        var page2 = await page2Response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(page2);
        Assert.True(page2!.HasNextPage);
        Assert.True(page2.HasPreviousPage);
        Assert.Equal(new[] { ids[2], ids[3] }, page2.Items.Select(i => i.DepartmentId));
        // No overlap with page 1.
        Assert.Empty(page2.Items.Select(i => i.DepartmentId).Intersect(page1.Items.Select(i => i.DepartmentId)));

        var page3Response = await client.GetAsync(BuildUrl(Url, keyword: token, sortBy: "name", sortDirection: "asc", page: 3, pageSize: 2));
        var page3 = await page3Response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(page3);
        Assert.False(page3!.HasNextPage);
        Assert.True(page3.HasPreviousPage);
        Assert.Equal(new[] { ids[4] }, page3.Items.Select(i => i.DepartmentId));
    }

    [Fact]
    public async Task StaffLeader_PageBeyondLast_ReturnsEmptyItemsWithMetadata()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        for (var i = 0; i < 3; i++)
            await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}beyond-{i} {token}", campusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(Url, keyword: token, page: 99, pageSize: 2));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Empty(result!.Items);
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(99, result.Page); // executor echoes the raw page number, does not clamp to last
        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    // ---- Status/type/result shape ------------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_StatusOmitted_ReturnsActiveAndInactive()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var activeId = await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}status-active {token}", campusId, "ACTIVE");
        var inactiveId = await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}status-inactive {token}", campusId, "INACTIVE");

        var response = await client.GetAsync(BuildUrl(Url, keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(2, result!.Items.Count);
        var active = Assert.Single(result.Items, i => i.DepartmentId == activeId);
        var inactive = Assert.Single(result.Items, i => i.DepartmentId == inactiveId);
        Assert.Equal("ACTIVE", active.Status);
        Assert.Equal("INACTIVE", inactive.Status);
    }

    [Fact]
    public async Task StaffLeader_GeneralDepartment_IsToggleable()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}general {token}", campusId, "ACTIVE", departmentType: "GENERAL");

        var response = await client.GetAsync(BuildUrl(Url, keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        var item = Assert.Single(result!.Items);
        Assert.Equal(departmentId, item.DepartmentId);
        Assert.Equal("GENERAL", item.DepartmentType);
        Assert.True(item.CanToggleStatus);
    }

    // Cannot seed a second ACTIVE IC department in the Staff Leader's own campus (DB trigger
    // trg_departments_one_ic_bi/bu allows only one per campus), and View Department List is
    // read-only so there is no risk in simply reading the campus's real, pre-existing IC
    // department — no write, no cleanup needed, nothing to corrupt.
    [Fact]
    public async Task StaffLeader_IcDepartment_IsNotToggleable()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var icDepartmentId = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == campusId && d.DepartmentType == "IC" && d.Status == EntityStatuses.Active)
            .Select(d => d.DepartmentId)
            .FirstAsync();

        // pageSize=100 to be safe: no keyword isolation is possible here (the real IC department's
        // name doesn't carry a test token), so the campus's full (small, ~6-7 row) department list
        // must all fit on one page.
        var response = await client.GetAsync(BuildUrl(Url, pageSize: 100));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        var icItem = result!.Items.First(i => i.DepartmentId == icDepartmentId);
        Assert.Equal("IC", icItem.DepartmentType);
        Assert.False(icItem.CanToggleStatus);
    }

    [Fact]
    public async Task StaffLeader_ResultItem_ContainsManagementFields()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var name = $"{DatabaseResetHelper.ViewDepartmentListNamePrefix}fields {token}";
        var departmentId = await SeedDepartmentAsync(name, campusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(Url, keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        var item = Assert.Single(result!.Items);

        Assert.Equal(departmentId, item.DepartmentId);
        Assert.Equal(campusId, item.CampusId);
        Assert.False(string.IsNullOrWhiteSpace(item.CampusName));
        Assert.Equal(name, item.Name);
        Assert.Null(item.HeadUserId);
        Assert.Null(item.HeadFullName);
        Assert.Equal("ACTIVE", item.Status);
        Assert.Equal("GENERAL", item.DepartmentType);
        Assert.True(item.CanToggleStatus);
        Assert.True(item.CreatedAt > DateTime.MinValue);
        Assert.Null(item.UpdatedAt);
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

        var departmentId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.ViewDepartmentListNamePrefix}assigned-head {token}", campusId, "ACTIVE", headUserId);

        var response = await client.GetAsync(BuildUrl(Url, keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        var item = Assert.Single(result!.Items);
        Assert.Equal(departmentId, item.DepartmentId);
        Assert.Equal(headUserId, item.HeadUserId);
        Assert.Equal(headUser.FullName, item.HeadFullName);
    }

    // ---- Campus scope (security) ------------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_DoesNotSeeOtherCampusDepartments()
    {
        var (client, ownCampusId) = await CreateStaffLeaderClientAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var otherCampusId = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active && c.CampusId != ownCampusId)
            .Select(c => c.CampusId)
            .FirstAsync();

        var token = UniqueToken();
        var ownId = await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}scope-own {token}", ownCampusId, "ACTIVE");
        var otherId = await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}scope-other {token}", otherCampusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(Url, keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        // Assert.Single (not just Contains) proves the other-campus row is excluded, not merely
        // that the own-campus row happens to be present somewhere in a larger, unfiltered result.
        var item = Assert.Single(result!.Items);
        Assert.Equal(ownId, item.DepartmentId);
        Assert.NotEqual(otherId, item.DepartmentId);
    }

    // ---- Shared executor parity with UC-103 --------------------------------------------------------

    [Fact]
    public async Task ViewListAndSearchFilter_SameCriteria_ReturnEquivalentResults()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var firstId = await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}parity-a {token}", campusId, "ACTIVE");
        var secondId = await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}parity-b {token}", campusId, "INACTIVE");

        var viewListResponse = await client.GetAsync(BuildUrl(Url, keyword: token, status: "ACTIVE", sortBy: "name", sortDirection: "asc", page: 1, pageSize: 20));
        var searchFilterResponse = await client.GetAsync(BuildUrl(SearchFilterUrl, keyword: token, status: "ACTIVE", sortBy: "name", sortDirection: "asc", page: 1, pageSize: 20));
        Assert.Equal(HttpStatusCode.OK, viewListResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, searchFilterResponse.StatusCode);

        var viewListResult = await viewListResponse.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        var searchFilterResult = await searchFilterResponse.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(viewListResult);
        Assert.NotNull(searchFilterResult);

        Assert.Equal(searchFilterResult!.Page, viewListResult!.Page);
        Assert.Equal(searchFilterResult.PageSize, viewListResult.PageSize);
        Assert.Equal(searchFilterResult.TotalItems, viewListResult.TotalItems);
        Assert.Equal(searchFilterResult.TotalPages, viewListResult.TotalPages);
        Assert.Equal(searchFilterResult.HasNextPage, viewListResult.HasNextPage);
        Assert.Equal(searchFilterResult.HasPreviousPage, viewListResult.HasPreviousPage);
        Assert.Equal(
            searchFilterResult.Items.Select(i => i.DepartmentId),
            viewListResult.Items.Select(i => i.DepartmentId));

        // Sanity: only the ACTIVE row (firstId) should have matched the status filter on both.
        var item = Assert.Single(viewListResult.Items);
        Assert.Equal(firstId, item.DepartmentId);
        Assert.NotEqual(secondId, item.DepartmentId);
    }

    // ---- Read-only ------------------------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_ViewList_DoesNotModifyDepartments()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.ViewDepartmentListNamePrefix}readonly {token}", campusId, "ACTIVE");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var before = await db.Departments.AsNoTracking().SingleAsync(d => d.DepartmentId == departmentId);
        var beforeTestRowCount = await db.Departments.CountAsync(d => d.Name.StartsWith(DatabaseResetHelper.ViewDepartmentListNamePrefix));

        var response = await client.GetAsync(BuildUrl(Url, keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var after = await assertDb.Departments.AsNoTracking().SingleAsync(d => d.DepartmentId == departmentId);
        var afterTestRowCount = await assertDb.Departments.CountAsync(d => d.Name.StartsWith(DatabaseResetHelper.ViewDepartmentListNamePrefix));

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
