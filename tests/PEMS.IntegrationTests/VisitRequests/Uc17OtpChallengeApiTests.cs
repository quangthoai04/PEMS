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
/// Integration tests for the UC-17 OTP challenge V2 — real controller + MediatR +
/// OtpService row-locking against the real <c>pems_test</c> MySQL database.
///
/// The headline regression: a WRONG code must return a typed error AND STILL PERSIST the
/// incremented attempt_count (the old code rolled the increment back with the handler
/// transaction, so attackers had unlimited attempts).
/// </summary>
public sealed class Uc17OtpChallengeApiTests : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string VerifyEndpoint = "/api/visit-requests/verify";
    private const string InitiateEndpoint = "/api/visit-requests/initiate";
    private const string ResendEndpoint = "/api/visit-requests/resend-otp";
    private const string RecoverEndpoint = "/api/visit-requests/otp/recover";
    private const string BypassToken = "TEST_HUMAN_OK"; // Turnstile:DevBypassToken in appsettings.Testing.json

    private readonly PemsWebApplicationFactory _factory;
    private string _campusCode = null!;

    public Uc17OtpChallengeApiTests(PemsWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // The create path requires an ACTIVE Staff Leader (IC) at the chosen campus.
        await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.StaffLeader);
        _campusCode = await Uc17TestData.FirstActiveCampusCodeAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private ApplicationDbContext Db(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    private static (DateTime Start, DateTime End) FutureSlot(int daysAhead = 10)
    {
        var start = DateTime.Today.AddDays(daysAhead).AddHours(9);
        return (start, start.AddHours(6));
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(raw).RootElement;
    }

    // ── 1. Regression: wrong code persists attempt_count despite the error response ──

    [Fact]
    public async Task WrongCode_ReturnsOtpInvalid_AndAttemptCountPersists()
    {
        var email = Uc17TestData.UniqueEmail("wrong");
        var submissionId = Guid.NewGuid().ToString();
        var sessionToken = $"it-session-{Guid.NewGuid():N}";
        var (start, end) = FutureSlot();

        using (var scope = _factory.Services.CreateScope())
            await Uc17TestData.SeedChallengeAsync(Db(scope), email, submissionId, sessionToken, "123456");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(VerifyEndpoint,
            Uc17TestData.VerifyPayload(email, submissionId, sessionToken, "000000", _campusCode, "Đoàn Wrong", start, end));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("OTP_INVALID", body.GetProperty("errorCode").GetString());
        Assert.Equal(9, body.GetProperty("remainingAttempts").GetInt32());

        using var verifyScope = _factory.Services.CreateScope();
        var token = await Db(verifyScope).OtpTokens.AsNoTracking()
            .SingleAsync(t => t.ChallengeTokenHash == Uc17TestData.ChallengeHash(sessionToken));
        Assert.Equal(1, token.AttemptCount);           // persisted DESPITE the 400
        Assert.NotNull(token.LastAttemptAt);
        Assert.Null(token.UsedAt);
    }

    // ── 2. The 10th wrong attempt burns the challenge (428, human verification) ──────

    [Fact]
    public async Task TenthWrongAttempt_Returns428_AndBurnsChallenge()
    {
        var email = Uc17TestData.UniqueEmail("burn");
        var submissionId = Guid.NewGuid().ToString();
        var sessionToken = $"it-session-{Guid.NewGuid():N}";
        var (start, end) = FutureSlot();

        using (var scope = _factory.Services.CreateScope())
            await Uc17TestData.SeedChallengeAsync(Db(scope), email, submissionId, sessionToken, "123456", attemptCount: 9);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(VerifyEndpoint,
            Uc17TestData.VerifyPayload(email, submissionId, sessionToken, "000000", _campusCode, "Đoàn Burn", start, end));

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("OTP_HUMAN_VERIFICATION_REQUIRED", body.GetProperty("errorCode").GetString());
        Assert.True(body.GetProperty("humanVerificationRequired").GetBoolean());

        using var verifyScope = _factory.Services.CreateScope();
        var token = await Db(verifyScope).OtpTokens.AsNoTracking()
            .SingleAsync(t => t.ChallengeTokenHash == Uc17TestData.ChallengeHash(sessionToken));
        Assert.Equal(10, token.AttemptCount);
        Assert.NotNull(token.HumanVerificationRequiredAt);
        Assert.NotNull(token.InvalidatedAt);
        Assert.Equal("MAX_ATTEMPTS", token.InvalidationReason);
    }

    // ── 3. Correct code on the final (10th) attempt still succeeds ───────────────────

    [Fact]
    public async Task CorrectCode_OnFinalAttempt_CreatesRequest()
    {
        var email = Uc17TestData.UniqueEmail("final");
        var submissionId = Guid.NewGuid().ToString();
        var sessionToken = $"it-session-{Guid.NewGuid():N}";
        var (start, end) = FutureSlot();

        using (var scope = _factory.Services.CreateScope())
            await Uc17TestData.SeedChallengeAsync(Db(scope), email, submissionId, sessionToken, "123456", attemptCount: 9);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(VerifyEndpoint,
            Uc17TestData.VerifyPayload(email, submissionId, sessionToken, "123456", _campusCode, "Đoàn Final", start, end));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("requestCode").GetString()));

        using var verifyScope = _factory.Services.CreateScope();
        var token = await Db(verifyScope).OtpTokens.AsNoTracking()
            .SingleAsync(t => t.ChallengeTokenHash == Uc17TestData.ChallengeHash(sessionToken));
        Assert.NotNull(token.UsedAt);
        var created = await Db(verifyScope).VisitRequests.AsNoTracking()
            .SingleAsync(r => r.SubmissionId == submissionId);
        Assert.Equal(email, created.RegistrantEmail);
        Assert.False(string.IsNullOrEmpty(created.BusinessFingerprint));
    }

    // ── 4. Concurrency: parallel wrong attempts never lose an update ─────────────────

    [Fact]
    public async Task ConcurrentWrongAttempts_DoNotLoseUpdates()
    {
        var email = Uc17TestData.UniqueEmail("conc");
        var submissionId = Guid.NewGuid().ToString();
        var sessionToken = $"it-session-{Guid.NewGuid():N}";
        var (start, end) = FutureSlot();

        using (var scope = _factory.Services.CreateScope())
            await Uc17TestData.SeedChallengeAsync(Db(scope), email, submissionId, sessionToken, "123456");

        var client = _factory.CreateClient();
        Task<HttpResponseMessage> Attempt(string code) => client.PostAsJsonAsync(VerifyEndpoint,
            Uc17TestData.VerifyPayload(email, submissionId, sessionToken, code, _campusCode, "Đoàn Conc", start, end));

        var responses = await Task.WhenAll(Attempt("000001"), Attempt("000002"));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));

        using var verifyScope = _factory.Services.CreateScope();
        var token = await Db(verifyScope).OtpTokens.AsNoTracking()
            .SingleAsync(t => t.ChallengeTokenHash == Uc17TestData.ChallengeHash(sessionToken));
        Assert.Equal(2, token.AttemptCount); // both wrong attempts recorded — no lost update
    }

    // ── 5. Progressive cooldown is enforced server-side ──────────────────────────────

    [Fact]
    public async Task SixthWrongAttempt_SetsCooldown_ImmediateRetryIs429_WithoutConsumingAttempt()
    {
        var email = Uc17TestData.UniqueEmail("cool");
        var submissionId = Guid.NewGuid().ToString();
        var sessionToken = $"it-session-{Guid.NewGuid():N}";
        var (start, end) = FutureSlot();

        using (var scope = _factory.Services.CreateScope())
            await Uc17TestData.SeedChallengeAsync(Db(scope), email, submissionId, sessionToken, "123456", attemptCount: 5);

        var client = _factory.CreateClient();

        // Wrong attempt #6 → typed error carries retryAfterSeconds = 2.
        var first = await client.PostAsJsonAsync(VerifyEndpoint,
            Uc17TestData.VerifyPayload(email, submissionId, sessionToken, "000000", _campusCode, "Đoàn Cool", start, end));
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        var firstBody = await ReadJsonAsync(first);
        Assert.Equal(2, firstBody.GetProperty("retryAfterSeconds").GetInt32());

        // Immediate retry (even with the CORRECT code) → 429 and NO attempt consumed.
        var second = await client.PostAsJsonAsync(VerifyEndpoint,
            Uc17TestData.VerifyPayload(email, submissionId, sessionToken, "123456", _campusCode, "Đoàn Cool", start, end));
        Assert.Equal((HttpStatusCode)429, second.StatusCode);
        var secondBody = await ReadJsonAsync(second);
        Assert.Equal("OTP_RETRY_LATER", secondBody.GetProperty("errorCode").GetString());

        using var verifyScope = _factory.Services.CreateScope();
        var token = await Db(verifyScope).OtpTokens.AsNoTracking()
            .SingleAsync(t => t.ChallengeTokenHash == Uc17TestData.ChallengeHash(sessionToken));
        Assert.Equal(6, token.AttemptCount);
        Assert.NotNull(token.NextAttemptAllowedAt);
    }

    // ── 6. Recovery: fake CAPTCHA success invalidates old + issues new ───────────────

    [Fact]
    public async Task Recover_WithBypassToken_IssuesNewChallenge_AndOldStaysDead()
    {
        var email = Uc17TestData.UniqueEmail("recover");
        var submissionId = Guid.NewGuid().ToString();
        var oldSession = $"it-session-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;
        var (start, end) = FutureSlot();

        using (var scope = _factory.Services.CreateScope())
            await Uc17TestData.SeedChallengeAsync(Db(scope), email, submissionId, oldSession, "123456",
                attemptCount: 10, humanVerificationRequiredAt: now, invalidatedAt: now);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(RecoverEndpoint, new
        {
            submissionId,
            sessionToken = oldSession,
            humanVerificationToken = BypassToken,
            registrantFullName = "IT UC17 Người đăng ký"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var newSession = body.GetProperty("sessionToken").GetString();
        Assert.False(string.IsNullOrEmpty(newSession));
        Assert.NotEqual(oldSession, newSession);
        Assert.NotEqual(email, newSession); // opaque token — NOT the email

        using var verifyScope = _factory.Services.CreateScope();
        var db = Db(verifyScope);
        var oldToken = await db.OtpTokens.AsNoTracking()
            .SingleAsync(t => t.ChallengeTokenHash == Uc17TestData.ChallengeHash(oldSession));
        Assert.NotNull(oldToken.InvalidatedAt);
        Assert.NotNull(oldToken.HumanVerifiedAt);

        var newToken = await db.OtpTokens.AsNoTracking()
            .SingleAsync(t => t.ChallengeTokenHash == Uc17TestData.ChallengeHash(newSession!));
        Assert.Equal("HUMAN_RECOVERY", newToken.IssueReason);
        Assert.Equal(0, newToken.AttemptCount);
        Assert.NotNull(newToken.HumanVerifiedAt);

        // The OLD challenge can never be used again — even with its correct code. The single
        // hourly recovery slot was just consumed by the successful recover above, so the burned
        // challenge now answers 429 (recovery rate-limited) instead of 428 (please do CAPTCHA):
        // offering another CAPTCHA would be a lie — a second recovery would be denied anyway.
        var oldVerify = await client.PostAsJsonAsync(VerifyEndpoint,
            Uc17TestData.VerifyPayload(email, submissionId, oldSession, "123456", _campusCode, "Đoàn Recover", start, end));
        Assert.Equal((HttpStatusCode)429, oldVerify.StatusCode);
    }

    // ── 7. Recovery with a bad token fails and issues nothing ────────────────────────

    [Fact]
    public async Task Recover_WithWrongToken_Fails_AndIssuesNothing()
    {
        var email = Uc17TestData.UniqueEmail("recfail");
        var submissionId = Guid.NewGuid().ToString();
        var oldSession = $"it-session-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;

        using (var scope = _factory.Services.CreateScope())
            await Uc17TestData.SeedChallengeAsync(Db(scope), email, submissionId, oldSession, "123456",
                attemptCount: 10, humanVerificationRequiredAt: now, invalidatedAt: now);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(RecoverEndpoint, new
        {
            submissionId,
            sessionToken = oldSession,
            humanVerificationToken = "not-the-right-token",
            registrantFullName = "IT UC17 Người đăng ký"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("HUMAN_VERIFICATION_FAILED", body.GetProperty("errorCode").GetString());

        using var verifyScope = _factory.Services.CreateScope();
        var tokenCount = await Db(verifyScope).OtpTokens.AsNoTracking()
            .CountAsync(t => t.Email == email);
        Assert.Equal(1, tokenCount); // no new challenge was issued
    }

    // ── 8. Correct OTP + failed create → used_at rolls back, OTP stays usable ────────

    [Fact]
    public async Task CreateFailureAfterCorrectOtp_RollsBackOtpConsumption()
    {
        var email = Uc17TestData.UniqueEmail("rollback");
        var submissionId = Guid.NewGuid().ToString();
        var sessionToken = $"it-session-{Guid.NewGuid():N}";
        var (start, end) = FutureSlot();

        using (var scope = _factory.Services.CreateScope())
            await Uc17TestData.SeedChallengeAsync(Db(scope), email, submissionId, sessionToken, "123456");

        var client = _factory.CreateClient();

        // Correct OTP but a nonexistent campus → create fails AFTER verify.
        var failing = await client.PostAsJsonAsync(VerifyEndpoint,
            Uc17TestData.VerifyPayload(email, submissionId, sessionToken, "123456", "ZZ", "Đoàn Rollback", start, end));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failing.StatusCode);

        using (var verifyScope = _factory.Services.CreateScope())
        {
            var token = await Db(verifyScope).OtpTokens.AsNoTracking()
                .SingleAsync(t => t.ChallengeTokenHash == Uc17TestData.ChallengeHash(sessionToken));
            Assert.Null(token.UsedAt); // rollback un-consumed the OTP
        }

        // Same OTP retried with a valid campus now succeeds — atomic consume+create.
        var retry = await client.PostAsJsonAsync(VerifyEndpoint,
            Uc17TestData.VerifyPayload(email, submissionId, sessionToken, "123456", _campusCode, "Đoàn Rollback", start, end));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        using (var verifyScope = _factory.Services.CreateScope())
        {
            var token = await Db(verifyScope).OtpTokens.AsNoTracking()
                .SingleAsync(t => t.ChallengeTokenHash == Uc17TestData.ChallengeHash(sessionToken));
            Assert.NotNull(token.UsedAt);
        }
    }

    // ── 9. Initiate issues an opaque challenge (session token ≠ email, hashes only) ──

    [Fact]
    public async Task Initiate_IssuesOpaqueChallenge_WithHashesOnly()
    {
        var email = Uc17TestData.UniqueEmail("init");
        var submissionId = Guid.NewGuid().ToString();
        var (start, end) = FutureSlot();

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(InitiateEndpoint,
            Uc17TestData.InitiatePayload(email, submissionId, _campusCode, "Đoàn Init", start, end));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var sessionToken = body.GetProperty("sessionToken").GetString();
        Assert.False(string.IsNullOrEmpty(sessionToken));
        Assert.NotEqual(email, sessionToken); // the old bug: sessionToken WAS the email
        Assert.Equal(10, body.GetProperty("maxAttempts").GetInt32());
        Assert.True(body.GetProperty("resendAfterSeconds").GetInt32() >= 0);

        using var verifyScope = _factory.Services.CreateScope();
        var token = await Db(verifyScope).OtpTokens.AsNoTracking()
            .SingleAsync(t => t.Email == email);
        Assert.Equal(Uc17TestData.ChallengeHash(sessionToken!), token.ChallengeTokenHash); // only SHA-256 stored
        Assert.Equal(submissionId, token.SubmissionId);
        Assert.Equal("INITIAL", token.IssueReason);
        Assert.Equal(10, token.MaxAttempts);
        Assert.Matches("^[0-9a-f]{64}$", token.TokenHash); // hashed code, never the raw 6 digits
    }

    // ── 10. Per-email hourly issue quota (soft standard limit) ───────────────────────

    [Fact]
    public async Task StandardIssueQuota_SixthInitiateInOneHour_Is429()
    {
        var email = Uc17TestData.UniqueEmail("quota");
        var (start, end) = FutureSlot();
        var client = _factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var ok = await client.PostAsJsonAsync(InitiateEndpoint,
                Uc17TestData.InitiatePayload(email, Guid.NewGuid().ToString(), _campusCode, "Đoàn Quota", start, end));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var sixth = await client.PostAsJsonAsync(InitiateEndpoint,
            Uc17TestData.InitiatePayload(email, Guid.NewGuid().ToString(), _campusCode, "Đoàn Quota", start, end));
        Assert.Equal((HttpStatusCode)429, sixth.StatusCode);
        var body = await ReadJsonAsync(sixth);
        // Specific per-quota code: the standard (INITIAL/RESEND) hourly soft limit was hit.
        Assert.Equal("OTP_STANDARD_RATE_LIMITED", body.GetProperty("errorCode").GetString());
    }

    // ── 11. A burned challenge cannot be resent — resend never bypasses the CAPTCHA ──

    [Fact]
    public async Task Resend_AfterBurn_Returns428()
    {
        var email = Uc17TestData.UniqueEmail("resendburn");
        var submissionId = Guid.NewGuid().ToString();
        var sessionToken = $"it-session-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;

        using (var scope = _factory.Services.CreateScope())
            await Uc17TestData.SeedChallengeAsync(Db(scope), email, submissionId, sessionToken, "123456",
                attemptCount: 10, humanVerificationRequiredAt: now, invalidatedAt: now);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(ResendEndpoint, new
        {
            registrantEmail = email,
            registrantFullName = "IT UC17 Người đăng ký",
            submissionId,
            sessionToken
        });

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("OTP_HUMAN_VERIFICATION_REQUIRED", body.GetProperty("errorCode").GetString());
    }
}
