using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;
using PEMS.Application.Common;

namespace PEMS.IntegrationTests.Faqs.UpdateFaq;

/// <summary>
/// Integration tests for Update FAQ — <c>PUT /api/faqs/{faqId}</c>. Endpoint, request DTO
/// (<c>UpdateFAQBody</c>: FaqType/Question/Answer — no Status, no FaqId in the body) and HO-only
/// authorization confirmed against <c>FaqsController.UpdateFAQ</c> and
/// <c>UpdateFAQCommandHandler.cs</c>.
///
/// UC ID note: source comments UC-64 for both "View FAQ Detail" and "Update FAQ" in the same
/// controller (internally inconsistent), while docs/use-cases/USE_CASE_LIST.md lists Update FAQ as
/// UC-66. Neither is hardcoded here — see test report for the full mapping.
///
/// Confirmed from UpdateFAQCommandHandler.cs: when the submitted question/answer/faqType are
/// byte-for-byte identical (after trim+sanitize) to what's already stored, the handler skips the
/// duplicate check, the audit refresh and the SaveChanges call entirely (returns Changed=false).
/// This differs from an older assumption that audit is always refreshed on save — the
/// Ho_NoChange_KeepsRecordUnchanged test below asserts the real behavior, not that assumption.
///
/// FAQ rows are seeded directly via <see cref="DatabaseResetHelper.CreateTestFaqAsync"/> (not
/// through the Create FAQ API) so Update FAQ tests stay independent of Create FAQ's endpoint, and
/// are always prefixed with <see cref="DatabaseResetHelper.UpdateFaqQuestionPrefix"/> — a prefix
/// dedicated to this test class, never shared with CreateFaqApiTests (see DatabaseResetHelper's
/// doc comment for the race condition that sharing one prefix previously caused).
/// </summary>
public sealed class UpdateFaqApiTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private readonly PemsWebApplicationFactory _factory;

    public UpdateFaqApiTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseResetHelper.DeleteTestFaqsAsync(db, DatabaseResetHelper.UpdateFaqQuestionPrefix);
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
        $"{DatabaseResetHelper.UpdateFaqQuestionPrefix}{label} {Guid.NewGuid():N}?";

    private sealed record UpdateFaqBody(string FaqType, string Question, string Answer);

    private async Task<ulong> SeedFaqAsync(string question, string answer, string faqType, string status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await DatabaseResetHelper.CreateTestFaqAsync(db, question, answer, faqType, status);
    }

    private sealed record FaqSnapshot(string Question, string Answer, string FaqType, string Status, DateTime? UpdatedAt, ulong? UpdatedBy);

    private async Task<FaqSnapshot> SnapshotFaqAsync(ulong faqId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var faq = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);
        return new FaqSnapshot(faq.Question, faq.Answer, faq.FaqType, faq.Status, faq.UpdatedAt, faq.UpdatedBy);
    }

    /// <summary>Reloads the FAQ and asserts every field (not just the one under attack) still matches <paramref name="expected"/>.</summary>
    private async Task AssertFaqUnchangedAsync(ulong faqId, FaqSnapshot expected)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

        Assert.Equal(expected.Question, saved.Question);
        Assert.Equal(expected.Answer, saved.Answer);
        Assert.Equal(expected.FaqType, saved.FaqType);
        Assert.Equal(expected.Status, saved.Status);
        Assert.Equal(expected.UpdatedAt, saved.UpdatedAt);
        Assert.Equal(expected.UpdatedBy, saved.UpdatedBy);
    }

    // ---- Authentication / Authorization ---------------------------------------------------

    [Fact]
    public async Task Anonymous_Unauthorized()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("anon"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = _factory.CreateClient(); // no test-auth headers -> anonymous

        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, UniqueQuestion("anon-new"), "Câu trả lời mới."));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_Forbidden()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("staff"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Staff);

        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, UniqueQuestion("staff-new"), "Câu trả lời mới."));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Forbidden()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("admin"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Admin);

        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, UniqueQuestion("admin-new"), "Câu trả lời mới."));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Visitor_Forbidden()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("visitor"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Visitor);

        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, UniqueQuestion("visitor-new"), "Câu trả lời mới."));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffLeader_Forbidden()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("staff-leader"), "Câu trả lời.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.StaffLeader);

        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, UniqueQuestion("staff-leader-new"), "Câu trả lời mới."));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // FaqId comes from the route, not the body ({faqId:long} accepts 0 as a valid long) — this
    // proves the full pipeline (routing -> controller -> MediatR validator) rejects it, which the
    // Unit Test (FaqId_Zero_HasError, validator only) can't demonstrate on its own.
    [Fact]
    public async Task FaqId_Zero_BadRequest()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.PutAsJsonAsync("/api/faqs/0", new UpdateFaqBody(
            FaqConstants.Type.Other, UniqueQuestion("zero-id"), "Câu trả lời."));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Happy path / DB state --------------------------------------------------------------

    [Fact]
    public async Task Ho_ValidPayload_UpdatesRecord()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("valid-old"), "Câu trả lời cũ.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        var newQuestion = UniqueQuestion("valid-new");

        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.AccountAccess, newQuestion, "Câu trả lời mới."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

        Assert.Equal(newQuestion, saved.Question);
        Assert.Equal("Câu trả lời mới.", saved.Answer);
        Assert.Equal(FaqConstants.Type.AccountAccess, saved.FaqType);
    }

    [Fact]
    public async Task Ho_Published_KeepsStatus()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("keep-published"), "Câu trả lời cũ.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, UniqueQuestion("keep-published-new"), "Câu trả lời mới."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

        Assert.Equal(FaqConstants.Status.Published, saved.Status);
    }

    [Fact]
    public async Task Ho_Hidden_KeepsStatus()
    {
        var faqId = await SeedFaqAsync(UniqueQuestion("keep-hidden"), "Câu trả lời cũ.", FaqConstants.Type.Other, FaqConstants.Status.Hidden);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, UniqueQuestion("keep-hidden-new"), "Câu trả lời mới."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

        Assert.Equal(FaqConstants.Status.Hidden, saved.Status);
    }

    // Real handler behavior (UpdateFAQCommandHandler.cs lines ~44-67): when FaqType/Question/Answer
    // submitted are byte-for-byte identical to what's stored, `changed` is false and the handler
    // returns early — no duplicate check, no SaveChanges, no audit refresh. This contradicts an
    // older assumption (audit always refreshed on save) recorded in the original prompt; this test
    // asserts the real, current behavior instead.
    [Fact]
    public async Task Ho_NoChange_KeepsRecordUnchanged()
    {
        var question = UniqueQuestion("no-change");
        const string answer = "Câu trả lời không đổi.";
        var faqId = await SeedFaqAsync(question, answer, FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, question, answer));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

        Assert.Equal(question, saved.Question);
        Assert.Equal(answer, saved.Answer);
        Assert.Equal(FaqConstants.Type.Other, saved.FaqType);
        Assert.Equal(FaqConstants.Status.Published, saved.Status);
        Assert.Null(saved.UpdatedAt);
        Assert.Null(saved.UpdatedBy);
    }

    // ---- Validation / no modification --------------------------------------------------------

    [Fact]
    public async Task EmptyQuestion_DoesNotModify()
    {
        var question = UniqueQuestion("empty-question-old");
        const string answer = "Câu trả lời cũ.";
        var faqId = await SeedFaqAsync(question, answer, FaqConstants.Type.Other, FaqConstants.Status.Published);
        var snapshot = await SnapshotFaqAsync(faqId);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        // FaqType/Answer deliberately differ from the seeded record (not just Question, which is
        // the field under test) so a partial-update bug — e.g. FaqType saved despite Question
        // failing validation — would actually be caught instead of being masked by sending back
        // values identical to what's already stored.
        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.AccountAccess, "", "Câu trả lời marker mới."));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertFaqUnchangedAsync(faqId, snapshot);
    }

    [Fact]
    public async Task EmptyAnswer_DoesNotModify()
    {
        var question = UniqueQuestion("empty-answer-old");
        const string answer = "Câu trả lời cũ.";
        var faqId = await SeedFaqAsync(question, answer, FaqConstants.Type.Other, FaqConstants.Status.Published);
        var snapshot = await SnapshotFaqAsync(faqId);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        // FaqType/Question deliberately differ from the seeded record, same reasoning as above.
        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.AccountAccess, UniqueQuestion("empty-answer-attempt"), ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertFaqUnchangedAsync(faqId, snapshot);
    }

    [Fact]
    public async Task InvalidFaqType_DoesNotModify()
    {
        var question = UniqueQuestion("invalid-type-old");
        const string answer = "Câu trả lời cũ.";
        var faqId = await SeedFaqAsync(question, answer, FaqConstants.Type.Other, FaqConstants.Status.Published);
        var snapshot = await SnapshotFaqAsync(faqId);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            "PROGRAM", UniqueQuestion("invalid-type-attempt"), "Câu trả lời mới."));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertFaqUnchangedAsync(faqId, snapshot);
    }

    [Fact]
    public async Task NonExistingFaq_NotFound()
    {
        var client = await CreateClientAsAsync(EffectiveRole.Ho);
        const ulong nonExistingFaqId = 999999999;

        var response = await client.PutAsJsonAsync($"/api/faqs/{nonExistingFaqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, UniqueQuestion("not-found"), "Câu trả lời."));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Duplicate question -------------------------------------------------------------------

    [Fact]
    public async Task DuplicateOtherFaq_DoesNotModify()
    {
        var questionA = UniqueQuestion("dup-a");
        var questionB = UniqueQuestion("dup-b");
        await SeedFaqAsync(questionA, "Trả lời A.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var faqBId = await SeedFaqAsync(questionB, "Trả lời B.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var snapshotB = await SnapshotFaqAsync(faqBId);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        // Same question as FAQ A (different case + surrounding whitespace); FaqType/Answer
        // deliberately differ from FAQ B's seeded values so a partial-update bug would be caught.
        var duplicateAttempt = $"  {questionA.ToUpperInvariant()}  ";
        var response = await client.PutAsJsonAsync($"/api/faqs/{faqBId}", new UpdateFaqBody(
            FaqConstants.Type.AccountAccess, duplicateAttempt, "Trả lời B mới."));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertFaqUnchangedAsync(faqBId, snapshotB);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var countWithNormalizedA = await db.Faqs.CountAsync(f => f.Question.ToLower() == questionA.ToLower());
        Assert.Equal(1, countWithNormalizedA);
    }

    [Fact]
    public async Task SameQuestionSelf_UpdatesRecord()
    {
        var question = UniqueQuestion("self-dup");
        var faqId = await SeedFaqAsync(question, "Trả lời cũ.", FaqConstants.Type.Other, FaqConstants.Status.Published);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        // Same question after trim, but Answer differs so `changed` is true and the duplicate
        // check actually executes (excluding self) instead of short-circuiting as a no-op save.
        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, $"  {question}  ", "Trả lời mới khác."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

        Assert.Equal(question, saved.Question);
        Assert.Equal("Trả lời mới khác.", saved.Answer);
    }

    // ---- Sanitize / XSS ------------------------------------------------------------------------

    [Fact]
    public async Task ScriptTag_SanitizedBeforeSave()
    {
        var oldQuestion = UniqueQuestion("xss-old");
        var faqId = await SeedFaqAsync(oldQuestion, "Trả lời cũ.", FaqConstants.Type.Other, FaqConstants.Status.Hidden);
        var client = await CreateClientAsAsync(EffectiveRole.Ho);

        var newQuestion = UniqueQuestion("xss-new") + "<script>alert(1)</script>";
        var newAnswer = "Câu trả lời <script>alert(2)</script> an toàn.";

        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, newQuestion, newAnswer));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

        Assert.DoesNotContain("<script>", saved.Question, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script>", saved.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(FaqConstants.Status.Hidden, saved.Status);
    }

    // ---- Audit ----------------------------------------------------------------------------------

    [Fact]
    public async Task Ho_ValidPayload_UpdatesAudit()
    {
        var oldQuestion = UniqueQuestion("audit-old");
        var faqId = await SeedFaqAsync(oldQuestion, "Trả lời cũ.", FaqConstants.Type.Other, FaqConstants.Status.Published);

        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seeded = await seedDb.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);
            Assert.Null(seeded.UpdatedAt);
            Assert.Null(seeded.UpdatedBy);
        }

        var (hoUserId, client) = await CreateClientWithUserIdAsAsync(EffectiveRole.Ho);
        var beforeUpdate = VietnamTime.Now();

        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, UniqueQuestion("audit-new"), "Trả lời mới."));

        var afterUpdate = VietnamTime.Now();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Faqs.AsNoTracking().SingleAsync(f => f.FaqId == faqId);

        Assert.Equal(hoUserId, saved.UpdatedBy);
        Assert.NotNull(saved.UpdatedAt);
        Assert.True(saved.UpdatedAt!.Value >= beforeUpdate.AddSeconds(-2));
        Assert.True(saved.UpdatedAt!.Value <= afterUpdate.AddSeconds(5));
    }
}
