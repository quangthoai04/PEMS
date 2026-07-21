using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Security;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Integration tests for UC-17 submit-intent idempotency (<c>submission_id</c> UNIQUE) and
/// business-fingerprint duplicate detection, against the real <c>pems_test</c> MySQL DB.
///
/// Deliberately ABSENT (accepted residual risk, no dedupe-guard table in this version):
/// there is NO test asserting that two CONCURRENT verifies with DIFFERENT submissionIds
/// and the SAME fingerprint always create exactly one row — without a guard/distributed
/// lock that cannot be guaranteed and must not be claimed.
/// </summary>
public sealed class Uc17IdempotencyDuplicateApiTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string VerifyEndpoint = "/api/v2/visit-requests/verify";

    private readonly PemsWebApplicationFactory _factory;
    private string _campusCode = null!;
    private string _secondCampusCode = null!;

    public Uc17IdempotencyDuplicateApiTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.StaffLeader);
        _campusCode = await Uc17TestData.FirstActiveCampusCodeAsync(db);
        _secondCampusCode = await Uc17TestData.SecondActiveCampusCodeAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private ApplicationDbContext Db(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    private static (DateTime Start, DateTime End) FutureSlot(int daysAhead = 12)
    {
        var start = DateTime.Today.AddDays(daysAhead).AddHours(9);
        return (start, start.AddHours(6));
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Seeds a fresh challenge and returns its session token.</summary>
    private async Task<string> SeedChallengeAsync(
        string email, string submissionId, string code = "123456",
        string? campusCode = null, string? delegation = null, DateTime? start = null, DateTime? end = null)
    {
        var sessionToken = $"it-session-{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        await Uc17TestData.SeedChallengeAsync(
            Db(scope), email, submissionId, sessionToken, code,
            campusCode: campusCode ?? _campusCode,
            delegationName: delegation ?? "Đoàn Test",
            start: start, end: end);
        return sessionToken;
    }

    private async Task<HttpResponseMessage> VerifyAsync(
        string email, string submissionId, string sessionToken, string code,
        string campusCode, string delegation, DateTime start, DateTime end)
    {
        var client = _factory.CreateClient();
        return await client.PostAsJsonAsync(VerifyEndpoint,
            Uc17TestData.VerifyV2Payload(email, submissionId, sessionToken, code, campusCode, delegation, start, end));
    }

    // ── §32.1: same submissionId, sequential retry → 200 with the SAME request, 1 row ──

    [Fact]
    public async Task SameSubmission_SequentialRetry_ReplaysOriginal_OneRowTotal()
    {
        var email = Uc17TestData.UniqueEmail("idem-seq");
        var submissionId = Guid.NewGuid().ToString();
        var (start, end) = FutureSlot();
        var sessionToken = await SeedChallengeAsync(email, submissionId, "123456", _campusCode, "Đoàn Idem", start, end);

        var first = await VerifyAsync(email, submissionId, sessionToken, "123456", _campusCode, "Đoàn Idem", start, end);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await ReadJsonAsync(first);
        var requestCode = firstBody.GetProperty("requestCode").GetString();

        // Retry of the exact same intent (OTP now consumed) → idempotent 200, same code.
        var retry = await VerifyAsync(email, submissionId, sessionToken, "123456", _campusCode, "Đoàn Idem", start, end);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryBody = await ReadJsonAsync(retry);
        Assert.Equal(requestCode, retryBody.GetProperty("requestCode").GetString());

        using var scope = _factory.Services.CreateScope();
        var rows = await Db(scope).VisitRequests.AsNoTracking()
            .CountAsync(r => r.SubmissionId == submissionId);
        Assert.Equal(1, rows);
    }

    // ── §32.2: same submissionId, CONCURRENT retry → at most one row, loser replays ──

    [Fact]
    public async Task SameSubmission_ConcurrentRetry_OneRow_BothGet200SameRequest()
    {
        var email = Uc17TestData.UniqueEmail("idem-conc");
        var submissionId = Guid.NewGuid().ToString();
        var (start, end) = FutureSlot();
        var sessionToken = await SeedChallengeAsync(email, submissionId, "123456", _campusCode, "Đoàn IdemConc", start, end);

        var responses = await Task.WhenAll(
            VerifyAsync(email, submissionId, sessionToken, "123456", _campusCode, "Đoàn IdemConc", start, end),
            VerifyAsync(email, submissionId, sessionToken, "123456", _campusCode, "Đoàn IdemConc", start, end));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var codes = await Task.WhenAll(responses.Select(async r =>
            (await ReadJsonAsync(r)).GetProperty("requestCode").GetString()));
        Assert.Equal(codes[0], codes[1]);

        using var scope = _factory.Services.CreateScope();
        var rows = await Db(scope).VisitRequests.AsNoTracking()
            .CountAsync(r => r.SubmissionId == submissionId);
        Assert.Equal(1, rows);
    }

    // ── §32.3: same submissionId + changed content → IDEMPOTENCY_KEY_REUSED ──────────

    [Fact]
    public async Task SameSubmission_DifferentFingerprint_IsRejectedAsKeyReuse()
    {
        var email = Uc17TestData.UniqueEmail("idem-reuse");
        var submissionId = Guid.NewGuid().ToString();
        var (start, end) = FutureSlot();
        var sessionToken = await SeedChallengeAsync(email, submissionId, "123456", _campusCode, "Đoàn Reuse", start, end);

        var first = await VerifyAsync(email, submissionId, sessionToken, "123456", _campusCode, "Đoàn Reuse", start, end);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // New challenge, SAME submissionId, but different core content (other delegation).
        var secondSession = await SeedChallengeAsync(email, submissionId, "123456", _campusCode, "Đoàn Khác Hẳn", start, end);
        var reuse = await VerifyAsync(email, submissionId, secondSession, "123456", _campusCode, "Đoàn Khác Hẳn", start, end);

        Assert.Equal(HttpStatusCode.Conflict, reuse.StatusCode);
        var body = await ReadJsonAsync(reuse);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", body.GetProperty("errorCode").GetString());

        using var scope = _factory.Services.CreateScope();
        Assert.Equal(1, await Db(scope).VisitRequests.AsNoTracking().CountAsync(r => r.SubmissionId == submissionId));
    }

    // ── §32.4 + §32.8: different submissionId + same fingerprint in-window → 409, no side effects ──

    [Fact]
    public async Task DifferentSubmission_SameFingerprint_InWindow_Is409Duplicate_WithoutSideEffects()
    {
        var email = Uc17TestData.UniqueEmail("dup");
        var (start, end) = FutureSlot();

        var firstSubmission = Guid.NewGuid().ToString();
        var firstSession = await SeedChallengeAsync(email, firstSubmission, "123456", _campusCode, "Đoàn Dup", start, end);
        var first = await VerifyAsync(email, firstSubmission, firstSession, "123456", _campusCode, "Đoàn Dup", start, end);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstCode = (await ReadJsonAsync(first)).GetProperty("requestCode").GetString();

        int requestsBefore, guestsBefore, notificationsBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            requestsBefore = await db.VisitRequests.AsNoTracking().CountAsync(r => r.RegistrantEmail == email);
            guestsBefore = await db.VisitGuestMembers.AsNoTracking().CountAsync();
            notificationsBefore = await db.Notifications.AsNoTracking().CountAsync();
        }

        // A SECOND submit intent (new submissionId, new OTP) with identical core content.
        var secondSubmission = Guid.NewGuid().ToString();
        var secondSession = await SeedChallengeAsync(email, secondSubmission, "123456", _campusCode, "Đoàn Dup", start, end);
        var dup = await VerifyAsync(email, secondSubmission, secondSession, "123456", _campusCode, "Đoàn Dup", start, end);

        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        var body = await ReadJsonAsync(dup);
        Assert.Equal("DUPLICATE_VISIT_REQUEST", body.GetProperty("errorCode").GetString());
        var data = body.GetProperty("data");
        Assert.Equal(firstCode, data.GetProperty("existingRequestCode").GetString());
        Assert.Equal("PENDING_APPROVAL", data.GetProperty("existingStatus").GetString());
        Assert.True(data.GetProperty("existingVisitRequestId").GetUInt64() > 0);
        Assert.False(string.IsNullOrEmpty(data.GetProperty("existingSubmittedAt").GetString()));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            // No new request/children/notifications — but the OTP WAS consumed.
            Assert.Equal(requestsBefore, await db.VisitRequests.AsNoTracking().CountAsync(r => r.RegistrantEmail == email));
            Assert.Equal(guestsBefore, await db.VisitGuestMembers.AsNoTracking().CountAsync());
            Assert.Equal(notificationsBefore, await db.Notifications.AsNoTracking().CountAsync());
            var otp = await db.OtpTokens.AsNoTracking()
                .SingleAsync(t => t.ChallengeTokenHash == Uc17TestData.ChallengeHash(secondSession));
            Assert.NotNull(otp.UsedAt);
        }
    }

    // ── §32.5: different campus / different time → allowed ───────────────────────────

    [Fact]
    public async Task DifferentCampus_IsNotDuplicate()
    {
        var email = Uc17TestData.UniqueEmail("dup-campus");
        var (start, end) = FutureSlot();

        var s1 = Guid.NewGuid().ToString();
        var first = await VerifyAsync(email, s1, await SeedChallengeAsync(email, s1, "123456", _campusCode, "Đoàn Campus", start, end), "123456",
            _campusCode, "Đoàn Campus", start, end);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var s2 = Guid.NewGuid().ToString();
        var second = await VerifyAsync(email, s2, await SeedChallengeAsync(email, s2, "123456", _secondCampusCode, "Đoàn Campus", start, end), "123456",
            _secondCampusCode, "Đoàn Campus", start, end);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task DifferentTime_IsNotDuplicate()
    {
        var email = Uc17TestData.UniqueEmail("dup-time");
        var (start, end) = FutureSlot();

        var s1 = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(email, s1, await SeedChallengeAsync(email, s1, "123456", _campusCode, "Đoàn Time", start, end), "123456",
            _campusCode, "Đoàn Time", start, end)).StatusCode);

        var s2 = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(email, s2, await SeedChallengeAsync(email, s2, "123456", _campusCode, "Đoàn Time", start.AddDays(1), end.AddDays(1)), "123456",
            _campusCode, "Đoàn Time", start.AddDays(1), end.AddDays(1))).StatusCode);
    }

    // ── §32.6: a REJECTED previous request never blocks a new one ─────────────────────

    [Fact]
    public async Task PreviousRejected_DoesNotBlockResubmission()
    {
        var email = Uc17TestData.UniqueEmail("dup-rejected");
        var (start, end) = FutureSlot();

        var s1 = Guid.NewGuid().ToString();
        var first = await VerifyAsync(email, s1, await SeedChallengeAsync(email, s1, "123456", _campusCode, "Đoàn Rejected", start, end), "123456",
            _campusCode, "Đoàn Rejected", start, end);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            var request = await db.VisitRequests.SingleAsync(r => r.SubmissionId == s1);
            request.Status = "REJECTED";
            await db.SaveChangesAsync();
        }

        var s2 = Guid.NewGuid().ToString();
        var second = await VerifyAsync(email, s2, await SeedChallengeAsync(email, s2, "123456", _campusCode, "Đoàn Rejected", start, end), "123456",
            _campusCode, "Đoàn Rejected", start, end);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    // ── §32.7: same fingerprint OUTSIDE the 15-minute window → allowed ────────────────

    [Fact]
    public async Task SameFingerprint_OutsideWindow_IsAllowed()
    {
        var email = Uc17TestData.UniqueEmail("dup-window");
        var (start, end) = FutureSlot();

        var s1 = Guid.NewGuid().ToString();
        var first = await VerifyAsync(email, s1, await SeedChallengeAsync(email, s1, "123456", _campusCode, "Đoàn Window", start, end), "123456",
            _campusCode, "Đoàn Window", start, end);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            var request = await db.VisitRequests.SingleAsync(r => r.SubmissionId == s1);
            request.SubmittedAt = request.SubmittedAt.AddMinutes(-16); // age it past the window
            await db.SaveChangesAsync();
        }

        var s2 = Guid.NewGuid().ToString();
        var second = await VerifyAsync(email, s2, await SeedChallengeAsync(email, s2, "123456", _campusCode, "Đoàn Window", start, end), "123456",
            _campusCode, "Đoàn Window", start, end);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }
}
