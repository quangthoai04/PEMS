using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Security;
using PEMS.Application.Faqs.Queries.ViewListFAQ;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Faqs.SearchFaq;

/// <summary>
/// Integration tests for UC-66 Search FAQ (HO management search).
///
/// Source-confirmed facts:
/// - There is no dedicated Search FAQ endpoint. Search is the <c>keyword</c> query parameter of
///   the same <c>GET /api/faqs</c> endpoint covered by UC-62 View List FAQ
///   (<c>ViewListFAQQueryHandler.cs</c>): <c>keyword?.Trim()</c>, then
///   <c>EF.Functions.Like</c> OR'd across Question, Answer, AND FaqType.
/// - <c>ViewListFAQQueryValidator</c> has no rule at all for Keyword (no min/max length) — hence
///   no Unit Test project for UC-66 (see report for the explicit reasoning).
///
/// UC ID note: the user's request calls this UC-66. docs/use-cases/USE_CASE_LIST.md lists Search
/// FAQ as UC-68 (and separately labels UC-66 as Update FAQ — a different feature). Neither number
/// is hardcoded; the folder name (Faqs/SearchFaq) is the stable identifier.
///
/// This class intentionally does NOT duplicate what UC-62's ViewListFaqApiTests already proves
/// (same endpoint, same handler):
/// - Full 5-role authorization matrix -> ViewListFaqApiTests already covers Anonymous/Staff/
///   StaffLeader/Admin/Visitor. Only a minimal Anonymous + one Forbidden case is repeated here as
///   a sanity check that this test class's own client wiring works.
/// - Keyword matches Answer content -> ViewListFaqApiTests.Keyword_Search_MatchesAnswerContent.
/// - Keyword no match -> empty result -> ViewListFaqApiTests.Ho_KeywordNoMatch_ReturnsEmptyResult
///   (identical code path; re-testing here would add no value).
/// - PUBLISHED+HIDDEN both visible to HO -> ViewListFaqApiTests.Ho_ReturnsPublishedAndHiddenFaqs.
/// - keyword + faqType / keyword + status AND-logic -> already proven by
///   ViewListFaqApiTests.FaqType_Filter_ReturnsOnlyMatchingType and
///   Status_Filter_ReturnsOnlyMatchingStatus, both of which already combine a keyword with a
///   filter.
/// - Read-only (GET never modifies FAQs) -> ViewListFaqApiTests.Ho_GetList_DoesNotModifyFaqs
///   (same handler, no search-specific write path exists to re-check).
///
/// What IS new here: keyword matching Question specifically (isolated from Answer), keyword
/// matching FaqType (never exercised by any existing FAQ test), and keyword trim +
/// case-insensitivity (never exercised by any existing FAQ test).
/// </summary>
public sealed class SearchFaqApiTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PemsWebApplicationFactory _factory;

    public SearchFaqApiTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseResetHelper.DeleteTestFaqsAsync(db, DatabaseResetHelper.SearchFaqQuestionPrefix);
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

    private async Task<ulong> SeedFaqAsync(string question, string answer, string faqType, string status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await DatabaseResetHelper.CreateTestFaqAsync(db, question, answer, faqType, status);
    }

    private static string BuildListUrl(string? keyword = null, int? pageSize = null)
    {
        var query = new List<string>();
        if (keyword is not null) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
        if (pageSize is not null) query.Add($"pageSize={pageSize}");
        return "/api/faqs" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
    }

    // ---- Authorization (minimal sanity check — full matrix already in UC-62) ----------------

    [Fact]
    public async Task Anonymous_Unauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(BuildListUrl(keyword: "anything"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_Forbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Staff);
        var response = await client.GetAsync(BuildListUrl(keyword: "anything"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Search core behavior (new coverage, not duplicated from UC-62) ---------------------

    [Fact]
    public async Task Ho_KeywordMatchesQuestion()
    {
        var token = UniqueToken();
        // Token only in Question — Answer deliberately does not contain it, so a match here can
        // only come from the Question side of the OR clause.
        var faqId = await SeedFaqAsync(
            $"{DatabaseResetHelper.SearchFaqQuestionPrefix}question-match {token}?",
            "Câu trả lời không liên quan.",
            FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.GetAsync(BuildListUrl(keyword: token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.Contains(result!.Items, i => i.FaqId == faqId);
    }

    [Fact]
    public async Task Ho_KeywordMatchesFaqType()
    {
        var token = UniqueToken();
        var faqId = await SeedFaqAsync(
            $"{DatabaseResetHelper.SearchFaqQuestionPrefix}faqtype-match {token}?",
            "Câu trả lời.",
            FaqConstants.Type.AccountAccess, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        // FaqType is a fixed enum, not free text, so it can't carry a unique token — search by
        // the exact type value instead. Use the validator's max pageSize (50) so the seeded row
        // surfaces regardless of how many real seed FAQs in pems_test already share this FaqType;
        // per the prompt's own guidance this test only asserts presence, never an exact count.
        var response = await client.GetAsync(BuildListUrl(keyword: FaqConstants.Type.AccountAccess, pageSize: 50));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.Contains(result!.Items, i => i.FaqId == faqId);
    }

    [Fact]
    public async Task Ho_KeywordCaseInsensitiveAndTrimmed()
    {
        var token = UniqueToken(); // lower-case hex from Guid
        var faqId = await SeedFaqAsync(
            $"{DatabaseResetHelper.SearchFaqQuestionPrefix}case-trim {token}?",
            "Câu trả lời.",
            FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        // Deliberately different case + surrounding whitespace vs. what was seeded.
        var searchTerm = $"  {token.ToUpperInvariant()}  ";
        var response = await client.GetAsync(BuildListUrl(keyword: searchTerm));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ViewListFAQDto>>(JsonOptions);
        Assert.Contains(result!.Items, i => i.FaqId == faqId);
    }
}
