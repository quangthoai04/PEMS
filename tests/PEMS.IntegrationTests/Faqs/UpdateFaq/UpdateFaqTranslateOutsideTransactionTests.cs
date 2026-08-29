using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PEMS.Application.Common.Security;
using PEMS.Application.News.Services;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Faqs.UpdateFaq;

/// <summary>
/// DB-TXN-007: <c>UpdateFAQCommandHandler</c>'s best-effort auto-translate used to call the
/// external translation provider from inside its open write transaction (between
/// <c>BeginTransactionAsync</c> and <c>CommitAsync</c>), holding the transaction open for an
/// HTTP round trip. The fix moves the translate call before the transaction opens.
///
/// This proves it empirically rather than by reading the diff: <see cref="TransactionProbeTranslator"/>
/// replaces the real <see cref="INewsTranslationService"/> for one request and records whether
/// <c>ApplicationDbContext.Database.CurrentTransaction</c> was set at the moment it was invoked.
/// </summary>
public sealed class UpdateFaqTranslateOutsideTransactionTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string QuestionPrefix = "[IT-UPDATE-FAQ-TXN007] ";

    private readonly PemsWebApplicationFactory _factory;

    public UpdateFaqTranslateOutsideTransactionTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseResetHelper.DeleteTestFaqsAsync(db, QuestionPrefix);
    }

    private sealed class TransactionProbeRecorder
    {
        public bool TranslateWasCalled;
        public bool TransactionWasOpenDuringTranslateCall;
    }

    private sealed class TransactionProbeTranslator : INewsTranslationService
    {
        private readonly ApplicationDbContext _db;
        private readonly TransactionProbeRecorder _recorder;

        public TransactionProbeTranslator(ApplicationDbContext db, TransactionProbeRecorder recorder)
        {
            _db = db;
            _recorder = recorder;
        }

        public Task<IReadOnlyList<string>> TranslateTextAsync(
            IReadOnlyList<string> contents, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken)
        {
            _recorder.TranslateWasCalled = true;
            _recorder.TransactionWasOpenDuringTranslateCall = _db.Database.CurrentTransaction is not null;
            return Task.FromResult<IReadOnlyList<string>>(contents.Select(c => c + " (EN)").ToList());
        }

        public Task<IReadOnlyList<string>> TranslateHtmlAsync(
            IReadOnlyList<string> contents, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken)
        {
            _recorder.TranslateWasCalled = true;
            if (_db.Database.CurrentTransaction is not null)
                _recorder.TransactionWasOpenDuringTranslateCall = true;
            return Task.FromResult<IReadOnlyList<string>>(contents.ToList());
        }

        public Task<NewsTranslationConnectionTestResult> TestConnectionAsync(
            string projectId, string location, string credentialJson, int timeoutSeconds, CancellationToken cancellationToken)
            => Task.FromResult(new NewsTranslationConnectionTestResult { Success = true });
    }

    private sealed record UpdateFaqBody(string FaqType, string Question, string Answer);

    [Fact]
    public async Task UpdateFaq_AutoTranslate_RunsOutsideDbTransaction()
    {
        ulong faqId;
        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            faqId = await DatabaseResetHelper.CreateTestFaqAsync(
                seedDb,
                $"{QuestionPrefix}old {Guid.NewGuid():N}?",
                "Câu trả lời cũ.",
                FaqConstants.Type.Other,
                FaqConstants.Status.Published);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hoUserId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Ho);
        var sessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, hoUserId, EffectiveRole.Ho);

        var recorder = new TransactionProbeRecorder();
        var testFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(recorder);
                services.RemoveAll<INewsTranslationService>();
                services.AddScoped<INewsTranslationService, TransactionProbeTranslator>();
            }));

        var client = testFactory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, hoUserId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, RoleCode.Ho);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, sessionId.ToString());

        // No EnglishQuestion/EnglishAnswer and no existing EN translation -> the handler's
        // auto-translate branch runs (DB-TXN-007's path).
        var newQuestion = $"{QuestionPrefix}new {Guid.NewGuid():N}?";
        var response = await client.PutAsJsonAsync($"/api/faqs/{faqId}", new UpdateFaqBody(
            FaqConstants.Type.Other, newQuestion, "Câu trả lời mới."));

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {response.StatusCode}: {responseBody}");

        Assert.True(recorder.TranslateWasCalled,
            "The fake translator was never invoked — the auto-translate branch did not run as expected, so this test proves nothing.");
        Assert.False(recorder.TransactionWasOpenDuringTranslateCall,
            "Auto-translate was called while a DB write transaction was open (DB-TXN-007 regression).");
    }
}
