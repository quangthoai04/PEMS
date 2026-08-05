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

namespace PEMS.IntegrationTests.Departments.SearchFilterDepartments;

/// <summary>
/// Integration tests for UC-103 Search and Filter Departments (Staff Leader).
///
/// Source-confirmed facts (see SearchandFilterDepartmentsQuery/Handler, DepartmentListQueryExecutor,
/// IDepartmentListCriteria, DepartmentListItemDto, StaffLeaderDepartmentScope):
/// - Real endpoint: GET /api/departments/searchandfilterdepartments.
/// - <see cref="SearchandFilterDepartmentsQuery"/> is BYTE-FOR-BYTE identical in shape to UC-104's
///   ViewDepartmentListQuery (Page, PageSize, Keyword, Status, SortBy, SortDirection) — both
///   delegate to the exact same internal <c>DepartmentListQueryExecutor.ExecuteAsync</c>. There is
///   no campusId or departmentType query parameter at all: campus is always resolved server-side
///   from the Staff Leader, and department type is not filterable through this endpoint.
/// - There is NO FluentValidation validator for this query. Invalid/out-of-range input is never
///   rejected with 400 — the executor silently clamps/falls back instead:
///     page &lt; 1            -&gt; treated as 1
///     pageSize &lt; 1 or &gt; 100 -&gt; clamped to 20 (default) / 100
///     sortBy not in {name, status, headname, createdat} -&gt; falls back to "name"
///     status not ACTIVE/INACTIVE -&gt; filter is simply not applied (no error, no exclusion)
///   PaginatedResult.Page/PageSize in the response echo the executor's clamped values, not the raw
///   request, so clamping is directly observable without needing an invalid-input BadRequest test.
/// - Keyword search matches department Name OR head user's FullName (LEFT JOIN), case-insensitive
///   (both sides ToLower()), trimmed. This is a real, distinct behavior from FAQ's search (which
///   never searches a joined user table) — worth its own test.
/// - Authorization is now enforced at TWO layers, and they answer differently by design (2026-08-05):
///   an anonymous caller is stopped by the API-wide fallback policy with 401, and an authenticated
///   caller with the wrong role is stopped by [RoleAuthorize] with 403 +
///   DepartmentManagementForbidden. StaffLeaderDepartmentScope still guards the handler underneath
///   and returns the same code, so a role that slips past the gate is refused identically.
///   Every 403 test asserts the errorCode, not just the status, so a regression that reaches a
///   DIFFERENT 403 (campus-scope, say) cannot pass as this one.
/// - Read-only: DepartmentListQueryExecutor always uses AsNoTracking() and never writes.
/// </summary>
public sealed class SearchFilterDepartmentsApiTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string Url = "/api/departments/searchandfilterdepartments";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PemsWebApplicationFactory _factory;

    public SearchFilterDepartmentsApiTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseResetHelper.DeleteTestDepartmentsAsync(db, DatabaseResetHelper.SearchFilterDepartmentNamePrefix);
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

    private async Task<ulong> SeedDepartmentAsync(string name, ulong campusId, string status, ulong? headUserId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await DatabaseResetHelper.CreateTestDepartmentAsync(db, name, campusId, "GENERAL", status, headUserId: headUserId);
    }

    private static string BuildUrl(
        string? keyword = null, string? status = null,
        string? sortBy = null, string? sortDirection = null, int? page = null, int? pageSize = null)
    {
        var query = new List<string>();
        if (keyword is not null) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
        if (status is not null) query.Add($"status={Uri.EscapeDataString(status)}");
        if (sortBy is not null) query.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
        if (sortDirection is not null) query.Add($"sortDirection={Uri.EscapeDataString(sortDirection)}");
        if (page is not null) query.Add($"page={page}");
        if (pageSize is not null) query.Add($"pageSize={pageSize}");
        return Url + (query.Count > 0 ? "?" + string.Join("&", query) : "");
    }

    // ---- Authorization (full matrix: Anonymous + all 7 non-StaffLeader effective roles — same
    // handler-only guard as UC-101/102) ---------------------------------------------------------

    /// <summary>
    /// A caller with no token is told to authenticate, not that they lack a permission.
    ///
    /// <para>
    /// This changed on 2026-08-05 and the change is deliberate. These endpoints used to carry no
    /// <c>[Authorize]</c> at all, so an anonymous request ran all the way into the handler and was
    /// refused there as a wrong ACTOR — 403 with the department code. The authorization work merged
    /// from Dev added an API-wide fallback policy requiring an authenticated user, which is what
    /// closed eight controllers that were reachable with no token; anonymous now stops at the
    /// authentication layer with 401.
    /// </para>
    /// <para>
    /// 401 is also the answer the rest of this codebase already gave — the FAQ endpoints were
    /// documented as differing from these — and the department screen maps it to "Phiên đăng nhập đã
    /// hết hạn. Vui lòng đăng nhập lại.", which tells someone whose session expired what to do.
    /// "Bạn không có quyền quản lý phòng ban." told them to give up.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Anonymous_Unauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(BuildUrl());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// An AUTHENTICATED caller with the wrong role still gets the department code, from the role gate
    /// in front of the handler rather than from the handler itself.
    ///
    /// <para>
    /// Pinned separately because the two layers are now different code paths that must give the same
    /// answer. Moving the refusal earlier is a security improvement; changing what it says was not
    /// intended, and it reached the user as a vaguer sentence.
    /// </para>
    /// </summary>
    [Fact]
    public async Task WrongRole_KeepsTheDepartmentErrorCode()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var response = await client.GetAsync(BuildUrl());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body.ErrorCode);
        Assert.Equal("Bạn không có quyền quản lý phòng ban.", body.Message);
        Assert.False(string.IsNullOrWhiteSpace(body.TraceId));
    }

    [Fact]
    public async Task Staff_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Staff);
        var response = await client.GetAsync(BuildUrl());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task DepartmentLead_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.DepartmentLead);
        var response = await client.GetAsync(BuildUrl());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Department_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Department);
        var response = await client.GetAsync(BuildUrl());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Student_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Student);
        var response = await client.GetAsync(BuildUrl());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Ho_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var response = await client.GetAsync(BuildUrl());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Admin_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Admin);
        var response = await client.GetAsync(BuildUrl());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    [Fact]
    public async Task Visitor_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Visitor);
        var response = await client.GetAsync(BuildUrl());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DepartmentErrorCodes.DepartmentManagementForbidden, body!.ErrorCode);
    }

    // ---- Search core behavior -----------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_KeywordMatchesName()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var departmentId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}name-match {token}", campusId, "ACTIVE");
        // Same-campus distractor WITHOUT the token, so Assert.Single below is self-contained —
        // it proves exclusion against a row this test itself controls, instead of silently
        // relying on however many real departments pems_test's background seed happens to have
        // in this campus right now.
        var distractorId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}name-distractor {UniqueToken()}", campusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        var item = Assert.Single(result!.Items);
        Assert.Equal(departmentId, item.DepartmentId);
        Assert.NotEqual(distractorId, item.DepartmentId);
    }

    // Proves the executor's documented "name + head full name" search scope — the token is
    // deliberately absent from the department's own Name, so a match can only come from the
    // LEFT JOIN on head_user.full_name.
    [Fact]
    public async Task StaffLeader_KeywordMatchesHeadFullName()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var headUserId = await DatabaseResetHelper.CreateHeadUserCandidateAsync(db, campusId, $"Head {token}");

        var departmentId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}head-match", campusId, "ACTIVE", headUserId);
        // Same-campus distractor with no head and a name that doesn't contain the token, so
        // Assert.Single is self-contained instead of depending on how many real departments the
        // campus already has.
        var distractorId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}head-distractor {UniqueToken()}", campusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        var item = Assert.Single(result!.Items);
        Assert.Equal(departmentId, item.DepartmentId);
        Assert.NotEqual(distractorId, item.DepartmentId);
        Assert.Equal($"Head {token}", item.HeadFullName);
    }

    [Fact]
    public async Task StaffLeader_KeywordNoMatch_ReturnsEmptyResult()
    {
        var (client, _) = await CreateStaffLeaderClientAsync();
        var nonExistentToken = UniqueToken();

        var response = await client.GetAsync(BuildUrl(keyword: nonExistentToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Empty(result!.Items);
        Assert.Equal(0, result.TotalItems);
    }

    [Fact]
    public async Task StaffLeader_KeywordCaseInsensitiveAndTrimmed()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken(); // lower-case hex from Guid
        var departmentId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}case-trim {token}", campusId, "ACTIVE");
        // Same-campus distractor without the token, so Assert.Single is self-contained regardless
        // of how many real departments pems_test already has in this campus.
        var distractorId = await SeedDepartmentAsync(
            $"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}case-trim-distractor {UniqueToken()}", campusId, "ACTIVE");

        var searchTerm = $"  {token.ToUpperInvariant()}  ";
        var response = await client.GetAsync(BuildUrl(keyword: searchTerm));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        var item = Assert.Single(result!.Items);
        Assert.Equal(departmentId, item.DepartmentId);
        Assert.NotEqual(distractorId, item.DepartmentId);
    }

    // ---- Filter behavior ------------------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_StatusFilter_ReturnsOnlyMatchingStatus()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var activeId = await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}status-active {token}", campusId, "ACTIVE");
        var inactiveId = await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}status-inactive {token}", campusId, "INACTIVE");

        var response = await client.GetAsync(BuildUrl(keyword: token, status: "ACTIVE"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Contains(result!.Items, i => i.DepartmentId == activeId);
        Assert.DoesNotContain(result.Items, i => i.DepartmentId == inactiveId);
        Assert.All(result.Items, i => Assert.Equal("ACTIVE", i.Status));
    }

    [Fact]
    public async Task StaffLeader_SearchAndStatusFilter_UsesAndLogic()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var otherToken = UniqueToken();
        // Three seeds, each failing exactly one of the two conditions except the target:
        //   activeId          -> matches keyword, WRONG status      -> must be excluded
        //   wrongKeywordId    -> matches status,  WRONG keyword     -> must be excluded
        //   inactiveId        -> matches keyword AND status         -> the only expected result
        // wrongKeywordId is what StaffLeader_StatusFilter_ReturnsOnlyMatchingStatus can't cover
        // (that test always keeps the same keyword across both statuses) — it proves status alone
        // is not enough either, closing the "status filter silently overrides/replaces keyword"
        // false-pass gap.
        var activeId = await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}and-active {token}", campusId, "ACTIVE");
        var wrongKeywordId = await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}and-wrong-keyword {otherToken}", campusId, "INACTIVE");
        var inactiveId = await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}and-inactive {token}", campusId, "INACTIVE");

        var response = await client.GetAsync(BuildUrl(keyword: token, status: "INACTIVE"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        // Assert.Single proves both directions of the AND at once: only the row matching BOTH
        // keyword and status survives, and nothing else (including real/foreign data) leaks in.
        var item = Assert.Single(result!.Items);
        Assert.Equal(inactiveId, item.DepartmentId);
        Assert.DoesNotContain(result.Items, i => i.DepartmentId == activeId);
        Assert.DoesNotContain(result.Items, i => i.DepartmentId == wrongKeywordId);
    }

    // Confirmed real behavior: an invalid status value is NOT rejected and NOT applied as a filter
    // — every status still comes back, unfiltered (DepartmentListQueryExecutor only applies the
    // Where clause when status is exactly ACTIVE or INACTIVE).
    [Fact]
    public async Task InvalidStatusFilter_ReturnsUnfilteredResults()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var activeId = await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}unfiltered-active {token}", campusId, "ACTIVE");
        var inactiveId = await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}unfiltered-inactive {token}", campusId, "INACTIVE");

        var response = await client.GetAsync(BuildUrl(keyword: token, status: "VISIBLE"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        // Exact count (not just Contains) closes the compound false-pass gap: if a future bug
        // ignored BOTH the invalid status AND the keyword (returning the whole campus unfiltered),
        // these two rows would still happen to be present and Contains alone would still pass —
        // asserting the count is exactly 2 proves keyword scoping is still genuinely applied.
        Assert.Equal(2, result!.Items.Count);
        Assert.Contains(result.Items, i => i.DepartmentId == activeId);
        Assert.Contains(result.Items, i => i.DepartmentId == inactiveId);
    }

    // ---- Scope/security behavior ------------------------------------------------------------------

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
        var ownId = await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}scope-own {token}", ownCampusId, "ACTIVE");
        var otherId = await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}scope-other {token}", otherCampusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Contains(result!.Items, i => i.DepartmentId == ownId);
        Assert.DoesNotContain(result.Items, i => i.DepartmentId == otherId);
    }

    // ---- Paging/sort input handling (no validator — silently clamped, never 400) ------------------

    [Fact]
    public async Task Page_LessThanOne_DefaultsToFirstPage()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        for (var i = 0; i < 3; i++)
            await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}page-{i} {token}", campusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(keyword: token, page: 0, pageSize: 2));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result!.Page);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.TotalItems);
    }

    [Fact]
    public async Task PageSize_OutOfRange_ClampedToAllowedRange()
    {
        var (client, _) = await CreateStaffLeaderClientAsync();

        var tooSmallResponse = await client.GetAsync(BuildUrl(pageSize: 0));
        Assert.Equal(HttpStatusCode.OK, tooSmallResponse.StatusCode);
        var tooSmallResult = await tooSmallResponse.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(tooSmallResult);
        Assert.Equal(20, tooSmallResult!.PageSize); // executor default

        var tooLargeResponse = await client.GetAsync(BuildUrl(pageSize: 99999));
        Assert.Equal(HttpStatusCode.OK, tooLargeResponse.StatusCode);
        var tooLargeResult = await tooLargeResponse.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(tooLargeResult);
        Assert.Equal(100, tooLargeResult!.PageSize); // executor max
    }

    [Fact]
    public async Task SortBy_NotAllowed_FallsBackToDefaultNameSort()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        // "b-..." sorts after "a-..." alphabetically — seeded out of name order so a real
        // name-ascending fallback is observable.
        var secondId = await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}b-second {token}", campusId, "ACTIVE");
        var firstId = await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}a-first {token}", campusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(keyword: token, sortBy: "not-a-real-column"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(2, result!.Items.Count);
        Assert.Equal(firstId, result.Items[0].DepartmentId);
        Assert.Equal(secondId, result.Items[1].DepartmentId);
    }

    // ---- Result shape -----------------------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_ResultItem_ContainsManagementFields()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var name = $"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}fields {token}";
        var departmentId = await SeedDepartmentAsync(name, campusId, "ACTIVE");

        var response = await client.GetAsync(BuildUrl(keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<DepartmentListItemDto>>(JsonOptions);
        Assert.NotNull(result);
        var item = Assert.Single(result!.Items);

        Assert.Equal(departmentId, item.DepartmentId);
        Assert.Equal(campusId, item.CampusId);
        Assert.Equal(name, item.Name);
        Assert.Null(item.HeadUserId);
        Assert.Null(item.HeadFullName);
        Assert.Equal("ACTIVE", item.Status);
        Assert.Equal("GENERAL", item.DepartmentType);
        Assert.True(item.CanToggleStatus); // GENERAL departments are always toggleable
    }

    // ---- Read-only behavior -----------------------------------------------------------------------

    [Fact]
    public async Task StaffLeader_Search_DoesNotModifyDepartments()
    {
        var (client, campusId) = await CreateStaffLeaderClientAsync();
        var token = UniqueToken();
        var departmentId = await SeedDepartmentAsync($"{DatabaseResetHelper.SearchFilterDepartmentNamePrefix}readonly {token}", campusId, "ACTIVE");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var before = await db.Departments.AsNoTracking().SingleAsync(d => d.DepartmentId == departmentId);

        var response = await client.GetAsync(BuildUrl(keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var after = await assertDb.Departments.AsNoTracking().SingleAsync(d => d.DepartmentId == departmentId);

        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.CampusId, after.CampusId);
        Assert.Equal(before.DepartmentType, after.DepartmentType);
        Assert.Equal(before.HeadUserId, after.HeadUserId);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(before.CreatedBy, after.CreatedBy);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Equal(before.UpdatedBy, after.UpdatedBy);
    }
}
