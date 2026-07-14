using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Security;
using PEMS.Application.Faqs.Queries.ViewListFAQ;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Faqs.ViewListFaq;

/// <summary>
/// Integration tests for UC-62 View List FAQ — the HO **management** list
/// (<c>GET /api/faqs</c>), confirmed from <c>FaqsController.GetFaqs</c> /
/// <c>ViewListFAQQueryHandler.cs</c> — NOT the public FAQ page
/// (<c>PublicContentController</c> / <c>GET /api/public/faqs</c>, PUBLISHED-only, anonymous).
/// The defining difference under test: this endpoint is HO-only and returns both PUBLISHED and
/// HIDDEN rows, matching the "Ho_ReturnsPublishedAndHiddenFaqs" case below.
///
/// UC ID note: FaqsController comments this endpoint "UC-62" (matching the user's request), but
/// docs/use-cases/USE_CASE_LIST.md lists View List FAQ as UC-64 — the same mismatch pattern
/// already recorded for Create/Update FAQ. Neither number is hardcoded; the folder name
/// (Faqs/ViewListFaq) is the stable identifier.
///
/// Confirmed from ViewListFAQQueryHandler.cs: GET /api/faqs supports pagination (page/pageSize),
/// filtering (faqType/status, "ALL" bypasses the filter), keyword search (LIKE across
/// Question/Answer/FaqType) and sorting (createdAt/displayOrder, asc/desc) all in the same
/// endpoint — there is no separate Search FAQ use case/endpoint in this codebase, so those cases
/// are tested here rather than skipped.
///
/// Every seeded FAQ carries a unique per-test token (a GUID) passed back as the `keyword` filter,
/// so each test only ever sees its own rows — never the ~90 real rows already in pems_test from
/// the fresh-create seed data, and never another test's in-flight rows.
/// </summary>
public sealed class ViewListFaqApiTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PemsWebApplicationFactory _factory;

    public ViewListFaqApiTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseResetHelper.DeleteTestFaqsAsync(db, DatabaseResetHelper.ViewListFaqQuestionPrefix);
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
            EffectiveRole.StaffLeader => (RoleCode.Staff, SubRole.Leader),
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

    private static string SeedQuestion(string token, string label) =>
        $"{DatabaseResetHelper.ViewListFaqQuestionPrefix}{label} {token}?";

    private async Task<ulong> SeedFaqAsync(string question, string answer, string faqType, string status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await DatabaseResetHelper.CreateTestFaqAsync(db, question, answer, faqType, status);
    }

    private static string BuildListUrl(
        string? keyword = null, string? faqType = null, string? status = null,
        string? sortBy = null, string? sortDirection = null, int? page = null, int? pageSize = null)
    {
        var query = new List<string>();
        if (keyword is not null) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
        if (faqType is not null) query.Add($"faqType={Uri.EscapeDataString(faqType)}");
        if (status is not null) query.Add($"status={Uri.EscapeDataString(status)}");
        if (sortBy is not null) query.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
        if (sortDirection is not null) query.Add($"sortDirection={Uri.EscapeDataString(sortDirection)}");
        if (page is not null) query.Add($"page={page}");
        if (pageSize is not null) query.Add($"pageSize={pageSize}");
        return "/api/faqs" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
    }

    // ---- Authentication / Authorization ---------------------------------------------------

    [Fact]
    public async Task Anonymous_Unauthorized()
    {
        var client = _factory.CreateClient(); // no test-auth headers -> anonymous
        var response = await client.GetAsync("/api/faqs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Staff);
        var response = await client.GetAsync("/api/faqs");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffLeader_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.StaffLeader);
        var response = await client.GetAsync("/api/faqs");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Admin);
        var response = await client.GetAsync("/api/faqs");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Visitor_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Visitor);
        var response = await client.GetAsync("/api/faqs");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Happy path / management scope -----------------------------------------------------

    [Fact]
    public async Task Ho_ReturnsFaqList()
    {
        var token = UniqueToken();
        var faqId = await SeedFaqAsync(SeedQuestion(token, "returns-list"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.GetAsync(BuildListUrl(keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.Contains(result!.Items, i => i.FaqId == faqId);
    }

    // The defining behavior that separates this HO management endpoint from the public FAQ page:
    // both PUBLISHED and HIDDEN rows must appear here, unfiltered.
    [Fact]
    public async Task Ho_ReturnsPublishedAndHiddenFaqs()
    {
        var token = UniqueToken();
        var publishedId = await SeedFaqAsync(SeedQuestion(token, "published"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var hiddenId = await SeedFaqAsync(SeedQuestion(token, "hidden"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Hidden);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.GetAsync(BuildListUrl(keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.Contains(result!.Items, i => i.FaqId == publishedId);
        Assert.Contains(result.Items, i => i.FaqId == hiddenId);
    }

    [Fact]
    public async Task Ho_ListItem_ContainsManagementFields()
    {
        var token = UniqueToken();
        var question = SeedQuestion(token, "fields");
        const string answer = "Câu trả lời đầy đủ cho quản trị.";
        var faqId = await SeedFaqAsync(question, answer, FaqConstants.Type.AccountAccess, FaqConstants.Status.Hidden);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.GetAsync(BuildListUrl(keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        var item = Assert.Single(result!.Items);

        Assert.Equal(faqId, item.FaqId);
        Assert.Equal(FaqConstants.Type.AccountAccess, item.FaqType);
        Assert.Equal(FaqConstants.ToVietnameseTypeLabel(FaqConstants.Type.AccountAccess), item.FaqTypeLabel);
        Assert.Equal(question, item.Question);
        Assert.Equal(answer, item.Answer);
        Assert.Equal(FaqConstants.Status.Hidden, item.Status);
        Assert.Equal(FaqConstants.ToVietnameseStatusLabel(FaqConstants.Status.Hidden), item.StatusLabel);
    }

    // ---- Read-only behavior -----------------------------------------------------------------

    [Fact]
    public async Task Ho_GetList_DoesNotModifyFaqs()
    {
        var token = UniqueToken();
        var faqId = await SeedFaqAsync(SeedQuestion(token, "readonly"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);

        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var before = await seedDb.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

            var client = await CreateClientAsAsync(EffectiveRole.Ho);
            var response = await client.GetAsync(BuildListUrl(keyword: token));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var after = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

            Assert.Equal(before.Question, after.Question);
            Assert.Equal(before.Answer, after.Answer);
            Assert.Equal(before.FaqType, after.FaqType);
            Assert.Equal(before.Status, after.Status);
            Assert.Equal(before.CreatedAt, after.CreatedAt);
            Assert.Equal(before.CreatedBy, after.CreatedBy);
            Assert.Equal(before.UpdatedAt, after.UpdatedAt);
            Assert.Equal(before.UpdatedBy, after.UpdatedBy);
        }
    }

    // ---- Empty / no matching data -------------------------------------------------------------

    [Fact]
    public async Task Ho_KeywordNoMatch_ReturnsEmptyResult()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var nonExistentToken = UniqueToken(); // never seeded -> guaranteed no match

        var response = await client.GetAsync(BuildListUrl(keyword: nonExistentToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.Empty(result!.Items);
        Assert.Equal(0, result.TotalItems);
    }

    // ---- Pagination ---------------------------------------------------------------------------

    [Fact]
    public async Task Ho_Pagination_ReturnsRequestedPage()
    {
        var token = UniqueToken();
        for (var i = 0; i < 3; i++)
            await SeedFaqAsync(SeedQuestion(token, $"page-{i}"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);

        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var page1Response = await client.GetAsync(BuildListUrl(keyword: token, page: 1, pageSize: 2));
        Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);
        var page1 = await page1Response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.Equal(2, page1!.Items.Count);
        Assert.Equal(3, page1.TotalItems);
        Assert.Equal(2, page1.TotalPages);

        var page2Response = await client.GetAsync(BuildListUrl(keyword: token, page: 2, pageSize: 2));
        var page2 = await page2Response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.Equal(1, page2!.Items.Count);
    }

    [Fact]
    public async Task Page_Zero_BadRequest()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var response = await client.GetAsync(BuildListUrl(page: 0));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Filter / search -----------------------------------------------------------------------

    [Fact]
    public async Task FaqType_Filter_ReturnsOnlyMatchingType()
    {
        var token = UniqueToken();
        await SeedFaqAsync(SeedQuestion(token, "account"), "Câu trả lời.", FaqConstants.Type.AccountAccess, FaqConstants.Status.Published);
        await SeedFaqAsync(SeedQuestion(token, "other"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.GetAsync(BuildListUrl(keyword: token, faqType: FaqConstants.Type.AccountAccess));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.NotEmpty(result!.Items);
        Assert.All(result.Items, i => Assert.Equal(FaqConstants.Type.AccountAccess, i.FaqType));
    }

    [Fact]
    public async Task Status_Filter_ReturnsOnlyMatchingStatus()
    {
        var token = UniqueToken();
        await SeedFaqAsync(SeedQuestion(token, "published"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        await SeedFaqAsync(SeedQuestion(token, "hidden"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Hidden);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.GetAsync(BuildListUrl(keyword: token, status: FaqConstants.Status.Published));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.NotEmpty(result!.Items);
        Assert.All(result.Items, i => Assert.Equal(FaqConstants.Status.Published, i.Status));
    }

    [Fact]
    public async Task FaqType_All_ReturnsBothTypes()
    {
        var token = UniqueToken();
        var idAccount = await SeedFaqAsync(SeedQuestion(token, "all-account"), "Câu trả lời.", FaqConstants.Type.AccountAccess, FaqConstants.Status.Published);
        var idOther = await SeedFaqAsync(SeedQuestion(token, "all-other"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.GetAsync(BuildListUrl(keyword: token, faqType: "ALL"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.Contains(result!.Items, i => i.FaqId == idAccount);
        Assert.Contains(result.Items, i => i.FaqId == idOther);
    }

    [Fact]
    public async Task Status_All_ReturnsBothStatuses()
    {
        var token = UniqueToken();
        var idPublished = await SeedFaqAsync(SeedQuestion(token, "all-published"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var idHidden = await SeedFaqAsync(SeedQuestion(token, "all-hidden"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Hidden);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.GetAsync(BuildListUrl(keyword: token, status: "ALL"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.Contains(result!.Items, i => i.FaqId == idPublished);
        Assert.Contains(result.Items, i => i.FaqId == idHidden);
    }

    // Proves the handler's keyword search covers Answer (not just Question) — the token only
    // appears in Answer here.
    [Fact]
    public async Task Keyword_Search_MatchesAnswerContent()
    {
        var token = UniqueToken();
        var faqId = await SeedFaqAsync(
            $"{DatabaseResetHelper.ViewListFaqQuestionPrefix}answer-search question?",
            $"Câu trả lời chứa mã {token}.",
            FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.GetAsync(BuildListUrl(keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.Contains(result!.Items, i => i.FaqId == faqId);
    }

    [Fact]
    public async Task InvalidFaqTypeFilter_BadRequest()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var response = await client.GetAsync(BuildListUrl(faqType: "PROGRAM"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidStatusFilter_BadRequest()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var response = await client.GetAsync(BuildListUrl(status: "VISIBLE"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SortBy_NotAllowed_BadRequest()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var response = await client.GetAsync(BuildListUrl(sortBy: "answer"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
