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
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.News;

/// <summary>
/// DB-TXN-007: <c>CreateNewsCommandHandler</c>'s best-effort auto-translate used to call the
/// external translation provider from inside its open write transaction (between
/// <c>BeginTransactionAsync</c> and <c>CommitAsync</c>), holding the transaction open for an
/// HTTP round trip. The fix moves the translate call before the transaction opens.
///
/// This proves it empirically rather than by reading the diff: <see cref="TransactionProbeTranslator"/>
/// replaces the real <see cref="INewsTranslationService"/> for one request and records whether
/// <c>ApplicationDbContext.Database.CurrentTransaction</c> was set at the moment it was invoked.
/// </summary>
public sealed class CreateNewsTranslateOutsideTransactionTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string TitlePrefix = "[IT-CREATE-NEWS-TXN007] ";

    private readonly PemsWebApplicationFactory _factory;

    public CreateNewsTranslateOutsideTransactionTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var newsIds = await db.NewsTranslations
            .Where(t => t.Title.StartsWith(TitlePrefix))
            .Select(t => t.NewsId)
            .Distinct()
            .ToListAsync();

        if (newsIds.Count == 0) return;

        // news_translations / news_content_sections cascade-delete from news (ON DELETE CASCADE),
        // so removing the parent row is enough.
        var rows = await db.News.Where(n => newsIds.Contains(n.NewsId)).ToListAsync();
        db.News.RemoveRange(rows);
        await db.SaveChangesAsync();
    }

    /// <summary>Records whether a DB transaction was open at the moment the translator was invoked.</summary>
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

    [Fact]
    public async Task CreateNews_AutoTranslate_RunsOutsideDbTransaction()
    {
        using var seedScope = _factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var staffUserId = await DatabaseResetHelper.EnsureTestUserAsync(seedDb, EffectiveRole.Staff);
        var sessionId = await DatabaseResetHelper.CreateActiveSessionAsync(seedDb, staffUserId, EffectiveRole.Staff);

        var recorder = new TransactionProbeRecorder();
        var testFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(recorder);
                services.RemoveAll<INewsTranslationService>();
                services.AddScoped<INewsTranslationService, TransactionProbeTranslator>();
            }));

        var client = testFactory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, staffUserId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, RoleCode.Staff);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubRoleHeader, SubRole.Staff);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, sessionId.ToString());

        var title = $"{TitlePrefix}{Guid.NewGuid():N}";

        // No EnglishContentSections -> the handler's auto-translate branch runs (DB-TXN-007's path).
        var payload = new
        {
            Title = title,
            Summary = "Tóm tắt kiểm thử DB-TXN-007, xác nhận dịch tự động chạy ngoài transaction.",
            ContentSections = new[]
            {
                new { SectionOrder = 1, SectionTitle = "Phần 1", SectionBodyHtml = "<p>Nội dung kiểm thử DB-TXN-007.</p>" }
            }
        };

        var response = await client.PostAsJsonAsync("/api/news", payload);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {response.StatusCode}: {responseBody}");

        Assert.True(recorder.TranslateWasCalled,
            "The fake translator was never invoked — the auto-translate branch did not run as expected, so this test proves nothing.");
        Assert.False(recorder.TransactionWasOpenDuringTranslateCall,
            "Auto-translate was called while a DB write transaction was open (DB-TXN-007 regression).");
    }
}
