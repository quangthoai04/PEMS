using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Security;
using PEMS.Application.PublicContent.Queries.ViewFAQ;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;
using PEMS.Application.Common;

namespace PEMS.IntegrationTests.Faqs.ChangeFaqVisibility;

/// <summary>
/// Integration tests for Change FAQ Visibility — <c>PATCH /api/faqs/visibility</c>, confirmed from
/// <c>FaqsController.ChangeFAQVisibility</c> / <c>ChangeFAQVisibilityCommandHandler.cs</c>.
///
/// UC ID note: the user's request calls this UC-65. The controller comments this action only
/// "// ChangeFAQVisibility" (no UC number at all), while docs/use-cases/USE_CASE_LIST.md lists it
/// as UC-67 — and separately labels UC-65 as Create FAQ, a different feature entirely. None of
/// these numbers are hardcoded; the folder name (Faqs/ChangeFaqVisibility) is the stable
/// identifier. See the test report for the full mapping.
///
/// API shape ("Kiểu B" per the prompt): the request body is <c>{ "faqId": ... }</c> only — no
/// target status. ChangeFAQVisibilityCommandHandler.cs always flips the current value
/// (PUBLISHED -> HIDDEN, anything else -> PUBLISHED) unconditionally. Consequently there is no
/// InvalidStatus test (no status field exists to send an invalid value for) and no SameStatus
/// test (a toggle can never be asked to set "the same" status — every call flips it).
///
/// FAQ rows are seeded directly via DatabaseResetHelper.CreateTestFaqAsync (not through the
/// Create FAQ API), tagged with <see cref="DatabaseResetHelper.ChangeFaqVisibilityQuestionPrefix"/>
/// — a prefix dedicated to this test class, never shared with Create/Update/ViewList FAQ tests.
/// </summary>
public sealed class ChangeFaqVisibilityApiTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string Endpoint = "/api/faqs/visibility";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PemsWebApplicationFactory _factory;

    public ChangeFaqVisibilityApiTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseResetHelper.DeleteTestFaqsAsync(db, DatabaseResetHelper.ChangeFaqVisibilityQuestionPrefix);
    }

    private async Task<(ulong UserId, HttpClient Client)> CreateClientWithUserIdAsAsync(string effectiveRole)
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

        return (userId, client);
    }

    private async Task<HttpClient> CreateClientAsAsync(string effectiveRole) =>
        (await CreateClientWithUserIdAsAsync(effectiveRole)).Client;

    private static string UniqueQuestion(string label) =>
        $"{DatabaseResetHelper.ChangeFaqVisibilityQuestionPrefix}{label} {Guid.NewGuid():N}?";

    private async Task<ulong> SeedFaqAsync(string question, string answer, string faqType, string status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await DatabaseResetHelper.CreateTestFaqAsync(db, question, answer, faqType, status);
    }

    private sealed record ChangeVisibilityBody(ulong FaqId);

    // ---- Authentication / Authorization ---------------------------------------------------

    [Fact]
    public async Task Anonymous_Unauthorized()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("anon"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = _factory.CreateClient(); // no test-auth headers -> anonymous

        var response = await client.PatchAsJsonAsync(Endpoint, new ChangeVisibilityBody(faqId));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_Forbidden()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("staff"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Staff);

        var response = await client.PatchAsJsonAsync(Endpoint, new ChangeVisibilityBody(faqId));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffLeader_Forbidden()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("staff-leader"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.StaffLeader);

        var response = await client.PatchAsJsonAsync(Endpoint, new ChangeVisibilityBody(faqId));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Forbidden()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("admin"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Admin);

        var response = await client.PatchAsJsonAsync(Endpoint, new ChangeVisibilityBody(faqId));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Visitor_Forbidden()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("visitor"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Visitor);

        var response = await client.PatchAsJsonAsync(Endpoint, new ChangeVisibilityBody(faqId));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Happy path / status transition ------------------------------------------------------

    [Fact]
    public async Task Ho_Published_ChangesToHidden()
    {
        var question = UniqueQuestion("published-to-hidden");
        const string answer = "Câu trả lời.";
        var faqId = await SeedFaqAsync(question, answer, FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.PatchAsJsonAsync(Endpoint, new ChangeVisibilityBody(faqId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

        Assert.Equal(FaqConstants.Status.Hidden, saved.Status);
        // Content fields must stay untouched — this UC only ever flips status.
        Assert.Equal(question, saved.Question);
        Assert.Equal(answer, saved.Answer);
        Assert.Equal(FaqConstants.Type.Other, saved.FaqType);
    }

    [Fact]
    public async Task Ho_Hidden_ChangesToPublished()
    {
        var question = UniqueQuestion("hidden-to-published");
        const string answer = "Câu trả lời.";
        var faqId = await SeedFaqAsync(question, answer, FaqConstants.Type.Other, FaqConstants.Status.Hidden);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.PatchAsJsonAsync(Endpoint, new ChangeVisibilityBody(faqId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

        Assert.Equal(FaqConstants.Status.Published, saved.Status);
        Assert.Equal(question, saved.Question);
        Assert.Equal(answer, saved.Answer);
        Assert.Equal(FaqConstants.Type.Other, saved.FaqType);
    }

    // ---- Audit --------------------------------------------------------------------------------

    [Fact]
    public async Task Ho_ValidPayload_UpdatesAudit()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("audit"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);

        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seeded = await seedDb.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);
            Assert.Null(seeded.UpdatedAt);
            Assert.Null(seeded.UpdatedBy);
        }

        var (hoUserId, client) = await CreateClientWithUserIdAsAsync(EffectiveRole.Ho);
        var beforeChange = VietnamTime.Now();

        var response = await client.PatchAsJsonAsync(Endpoint, new ChangeVisibilityBody(faqId));
        var afterChange = VietnamTime.Now();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

        Assert.Equal(hoUserId, saved.UpdatedBy);
        Assert.NotNull(saved.UpdatedAt);
        Assert.True(saved.UpdatedAt!.Value >= beforeChange.AddSeconds(-2));
        Assert.True(saved.UpdatedAt!.Value <= afterChange.AddSeconds(5));
    }

    // ---- Not found / invalid route -------------------------------------------------------------

    [Fact]
    public async Task NonExistingFaq_NotFound()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var response = await client.PatchAsJsonAsync(Endpoint, new ChangeVisibilityBody(999999999UL));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FaqId_Zero_BadRequest()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var response = await client.PatchAsJsonAsync(Endpoint, new ChangeVisibilityBody(0UL));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Public visibility side effect ---------------------------------------------------------
    // ViewFaqQueryHandler.cs hardcodes .Where(x => x.Status == "PUBLISHED") for the public FAQ
    // page — these two tests prove Change Visibility's real end-user impact on that page.

    [Fact]
    public async Task Hide_RemovesFaqFromPublicList()
    {
        var token = Guid.NewGuid().ToString("N");
        var question = $"{DatabaseResetHelper.ChangeFaqVisibilityQuestionPrefix}public-hide {token}?";
        var faqId = await SeedFaqAsync(question, "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.PatchAsJsonAsync(Endpoint, new ChangeVisibilityBody(faqId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var publicClient = _factory.CreateClient();
        var publicResponse = await publicClient.GetAsync($"/api/public/faqs?keyword={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);

        var result = await publicResponse.Content.ReadFromJsonAsync<PaginatedResult<ViewFaqDto>>(JsonOptions);
        Assert.DoesNotContain(result!.Items, i => i.FaqId == faqId);
    }

    [Fact]
    public async Task Show_AddsFaqToPublicList()
    {
        var token = Guid.NewGuid().ToString("N");
        var question = $"{DatabaseResetHelper.ChangeFaqVisibilityQuestionPrefix}public-show {token}?";
        var faqId = await SeedFaqAsync(question, "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Hidden);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.PatchAsJsonAsync(Endpoint, new ChangeVisibilityBody(faqId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var publicClient = _factory.CreateClient();
        var publicResponse = await publicClient.GetAsync($"/api/public/faqs?keyword={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);

        var result = await publicResponse.Content.ReadFromJsonAsync<PaginatedResult<ViewFaqDto>>(JsonOptions);
        Assert.Contains(result!.Items, i => i.FaqId == faqId);
    }
}
