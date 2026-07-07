using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Faqs.CreateFaq;

/// <summary>
/// Integration tests for UC-63 Create FAQ — <c>POST /api/faqs</c>. Exercises the real
/// controller + MediatR pipeline + validator + handler against a real <c>pems_test</c>
/// MySQL database, with authentication faked via <see cref="TestAuthHandler"/>.
///
/// All FAQ rows created here are prefixed with <see cref="DatabaseResetHelper.FaqQuestionPrefix"/>
/// and removed on dispose so repeated runs never accumulate leftover data.
/// </summary>
public sealed class CreateFaqApiTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string Endpoint = "/api/faqs";

    private readonly PemsWebApplicationFactory _factory;

    public CreateFaqApiTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseResetHelper.DeleteTestFaqsAsync(db);
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

    private static string UniqueQuestion(string label) =>
        $"{DatabaseResetHelper.FaqQuestionPrefix}{label} {Guid.NewGuid():N}?";

    private sealed record CreateFaqRequest(string FaqType, string Question, string Answer, string? Status);

    // ---- 1. Authentication / Authorization ---------------------------------------------------

    [Fact]
    public async Task Anonymous_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient(); // no test-auth headers -> anonymous

        var response = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            FaqConstants.Type.Other, UniqueQuestion("anon"), "Câu trả lời.", FaqConstants.Status.Published));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_ReturnsForbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Staff);

        var response = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            FaqConstants.Type.Other, UniqueQuestion("staff"), "Câu trả lời.", FaqConstants.Status.Published));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_ReturnsForbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Admin);

        var response = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            FaqConstants.Type.Other, UniqueQuestion("admin"), "Câu trả lời.", FaqConstants.Status.Published));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Visitor_ReturnsForbidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Visitor);

        var response = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            FaqConstants.Type.Other, UniqueQuestion("visitor"), "Câu trả lời.", FaqConstants.Status.Published));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- 2. Happy path (HO) -------------------------------------------------------------------

    [Fact]
    public async Task Ho_Published_ReturnsOkAndPersistsPublished()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var question = UniqueQuestion("published");

        var response = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            FaqConstants.Type.AccountAccess, question, "Bạn dùng email FPT để đăng nhập.", FaqConstants.Status.Published));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.Question == question);

        Assert.Equal(FaqConstants.Status.Published, saved.Status);
        Assert.Equal(FaqConstants.Type.AccountAccess, saved.FaqType);
    }

    [Fact]
    public async Task Ho_Hidden_ReturnsOkAndPersistsHidden()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var question = UniqueQuestion("hidden");

        var response = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            FaqConstants.Type.Other, question, "Câu trả lời.", FaqConstants.Status.Hidden));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.Question == question);

        Assert.Equal(FaqConstants.Status.Hidden, saved.Status);
    }

    [Fact]
    public async Task Ho_NoStatus_ReturnsOkAndDefaultsToPublished()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var question = UniqueQuestion("default-status");

        var response = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            FaqConstants.Type.Other, question, "Câu trả lời.", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.Question == question);

        Assert.Equal(FaqConstants.Status.Published, saved.Status);
    }

    // ---- 3. Validation (HO, but invalid payload) ----------------------------------------------

    [Fact]
    public async Task EmptyQuestion_ReturnsBadRequestAndDoesNotPersist()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        // Question is empty, so it can't be used to look up a possible leftover row —
        // embed a unique marker in Answer instead. The finally block cleans that marker up
        // directly (not via DeleteTestFaqsAsync, which matches on Question) in case a
        // validation regression lets the row through despite the expected 400.
        var answerMarker = $"{DatabaseResetHelper.FaqQuestionPrefix}EmptyQuestionCheck {Guid.NewGuid():N}";

        try
        {
            var response = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
                FaqConstants.Type.Other, "", answerMarker, FaqConstants.Status.Published));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Faqs.AnyAsync(f => f.Answer == answerMarker));
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var leaked = await db.Faqs.Where(f => f.Answer == answerMarker).ToListAsync();
            if (leaked.Count > 0)
            {
                db.Faqs.RemoveRange(leaked);
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task EmptyAnswer_ReturnsBadRequestAndDoesNotPersist()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var question = UniqueQuestion("empty-answer");

        var response = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            FaqConstants.Type.Other, question, "", FaqConstants.Status.Published));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Faqs.AnyAsync(f => f.Question == question));
    }

    [Fact]
    public async Task InvalidFaqType_ReturnsBadRequestAndDoesNotPersist()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var question = UniqueQuestion("bad-type");

        var response = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            "PROGRAM", question, "Câu trả lời.", FaqConstants.Status.Published));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Faqs.AnyAsync(f => f.Question == question));
    }

    [Fact]
    public async Task InvalidStatus_ReturnsBadRequestAndDoesNotPersist()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var question = UniqueQuestion("bad-status");

        var response = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            FaqConstants.Type.Other, question, "Câu trả lời.", "VISIBLE"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Faqs.AnyAsync(f => f.Question == question));
    }

    // ---- 4. Duplicate question (trim + case-insensitive) ---------------------------------------

    [Fact]
    public async Task DuplicateQuestion_ReturnsConflictAndDoesNotCreateSecondRecord()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var baseQuestion = UniqueQuestion("duplicate");

        var first = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            FaqConstants.Type.Other, baseQuestion, "Câu trả lời đầu tiên.", FaqConstants.Status.Published));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Same question, different case + surrounding whitespace.
        var duplicateVariant = $"  {baseQuestion.ToUpperInvariant()}  ";
        var second = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            FaqConstants.Type.Other, duplicateVariant, "Câu trả lời khác.", FaqConstants.Status.Published));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await db.Faqs.CountAsync(f => f.Question.ToLower() == baseQuestion.ToLower());
        Assert.Equal(1, count);
    }

    // ---- 5. Sanitize -----------------------------------------------------------------------------

    [Fact]
    public async Task ScriptTag_ReturnsOkAndPersistsSanitizedContent()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var question = UniqueQuestion("xss") + "<script>alert(1)</script>";
        var answer = "Câu trả lời <script>alert(2)</script> an toàn.";

        var response = await client.PostAsJsonAsync(Endpoint, new CreateFaqRequest(
            FaqConstants.Type.Other, question, answer, FaqConstants.Status.Published));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking()
            .Where(f => f.Question.StartsWith(DatabaseResetHelper.FaqQuestionPrefix) && f.Question.Contains("xss"))
            .OrderByDescending(f => f.FaqId)
            .FirstAsync();

        Assert.DoesNotContain("<script>", saved.Question, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script>", saved.Answer, StringComparison.OrdinalIgnoreCase);
    }
}
