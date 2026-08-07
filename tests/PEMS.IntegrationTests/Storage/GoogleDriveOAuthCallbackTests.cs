using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PEMS.Application.ApiIntegrations.Commands.CompleteGoogleDriveOAuth;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Storage;

/// <summary>
/// What the Google Drive OAuth callback does with everything Google can hand it — driven entirely through a
/// stub HTTP handler and a stub state service. No network, no consent screen, no shared account.
///
/// <para>
/// The endpoint this covers is anonymous by necessity: Google redirects a browser to it, so there is no
/// token to authenticate. That makes the ORDER of its checks a security property rather than a style
/// preference, and several tests below assert nothing but the order — that a code is never spent before the
/// state authenticates, and that a database is never written before either.
/// </para>
/// <para>
/// It replaces a DEV-only page that rendered the refresh token as HTML to be copied into a config file and,
/// in production, into a Railway variable followed by a redeploy.
/// </para>
/// </summary>
public sealed class GoogleDriveOAuthCallbackTests
{
    private const ulong AdminUserId = 7;

    // ── Nothing happens until the state authenticates ─────────────────────────

    [Fact]
    public async Task A_callback_with_no_state_is_refused_before_the_code_is_spent()
    {
        var harness = new Harness(stateIsValid: false);

        var result = await harness.Handle(new CompleteGoogleDriveOAuthCommand("an-auth-code", null, null));

        Assert.False(result.Success);
        Assert.Equal(GoogleDriveOAuthRedirectReasons.InvalidState, result.Reason);
        Assert.Equal(0, harness.TokenExchangeCount);
    }

    /// <summary>
    /// The property that matters most here. A callback that exchanged first would spend a stranger's code
    /// against THIS deployment's client id and secret before deciding whether to trust the caller at all.
    /// </summary>
    [Fact]
    public async Task An_invalid_state_never_reaches_google_and_never_touches_the_database()
    {
        var harness = new Harness(stateIsValid: false);
        await harness.SeedDriveRowAsync("the-token-already-stored");

        var result = await harness.Handle(
            new CompleteGoogleDriveOAuthCommand("an-auth-code", null, "forged-state"));

        Assert.False(result.Success);
        Assert.Equal(0, harness.TokenExchangeCount);
        Assert.Equal("the-token-already-stored", await harness.StoredRefreshTokenAsync());
    }

    [Fact]
    public async Task An_expired_state_is_reported_as_expired_rather_than_as_forged()
    {
        var harness = new Harness(
            stateIsValid: false, stateFailureReason: GoogleDriveOAuthRedirectReasons.StateExpired);

        var result = await harness.Handle(
            new CompleteGoogleDriveOAuthCommand("an-auth-code", null, "a-stale-state"));

        Assert.Equal(GoogleDriveOAuthRedirectReasons.StateExpired, result.Reason);
        Assert.Equal(0, harness.TokenExchangeCount);
    }

    // ── Google said no ────────────────────────────────────────────────────────

    [Fact]
    public async Task An_admin_who_declines_consent_gets_its_own_reason_and_keeps_the_old_credential()
    {
        var harness = new Harness();
        await harness.SeedDriveRowAsync("the-token-already-stored");

        var result = await harness.Handle(
            new CompleteGoogleDriveOAuthCommand(null, "access_denied", "a-valid-state"));

        Assert.False(result.Success);
        Assert.Equal(GoogleDriveOAuthRedirectReasons.AccessDenied, result.Reason);
        Assert.Equal(0, harness.TokenExchangeCount);
        Assert.Equal("the-token-already-stored", await harness.StoredRefreshTokenAsync());
    }

    [Fact]
    public async Task A_rejected_authorization_code_does_not_overwrite_the_stored_credential()
    {
        var harness = new Harness(respond: _ => Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));
        await harness.SeedDriveRowAsync("the-token-already-stored");

        var result = await harness.Handle(
            new CompleteGoogleDriveOAuthCommand("an-expired-code", null, "a-valid-state"));

        Assert.False(result.Success);
        Assert.Equal(GoogleDriveOAuthRedirectReasons.TokenExchangeFailed, result.Reason);
        Assert.Equal("the-token-already-stored", await harness.StoredRefreshTokenAsync());
    }

    /// <summary>
    /// Google issues a refresh token only when it considers the grant new. Writing the absence of one over
    /// a working credential would turn a pointless reconnect into an outage across every upload path — so
    /// the stored token stays, and the admin is told why nothing changed.
    /// </summary>
    [Fact]
    public async Task A_success_with_no_refresh_token_keeps_the_one_already_stored()
    {
        var harness = new Harness(
            respond: _ => Json(HttpStatusCode.OK, """{"access_token":"short-lived","expires_in":3599}"""));
        await harness.SeedDriveRowAsync("the-token-already-stored");

        var result = await harness.Handle(
            new CompleteGoogleDriveOAuthCommand("an-auth-code", null, "a-valid-state"));

        Assert.False(result.Success);
        Assert.Equal(GoogleDriveOAuthRedirectReasons.NoRefreshToken, result.Reason);
        Assert.Equal("the-token-already-stored", await harness.StoredRefreshTokenAsync());
    }

    // ── The happy path ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_reconnect_replaces_the_stored_credential_with_the_new_one()
    {
        var harness = new Harness(
            respond: _ => Json(HttpStatusCode.OK, """{"refresh_token":"the-brand-new-token"}"""));
        await harness.SeedDriveRowAsync("the-token-already-stored");

        var result = await harness.Handle(
            new CompleteGoogleDriveOAuthCommand("an-auth-code", null, "a-valid-state"));

        Assert.True(result.Success);
        Assert.Null(result.Reason);
        Assert.Equal("the-brand-new-token", await harness.StoredRefreshTokenAsync());
    }

    /// <summary>The column holds ciphertext. A plaintext token in a database column is the thing this replaces.</summary>
    [Fact]
    public async Task The_stored_column_never_contains_the_token_in_the_clear()
    {
        var harness = new Harness(
            respond: _ => Json(HttpStatusCode.OK, """{"refresh_token":"the-brand-new-token"}"""));

        await harness.Handle(new CompleteGoogleDriveOAuthCommand("an-auth-code", null, "a-valid-state"));

        var stored = await harness.DriveRowAsync();
        Assert.NotNull(stored!.CredentialsJsonEncrypted);
        Assert.DoesNotContain("the-brand-new-token", stored.CredentialsJsonEncrypted!);
    }

    /// <summary>
    /// The credential changed, so the previous verdict describes a token that no longer exists. Left in
    /// place it would sit on the card as a green SUCCESS for a connection nobody has tried.
    /// </summary>
    [Fact]
    public async Task A_reconnect_clears_the_previous_test_verdict()
    {
        var harness = new Harness(
            respond: _ => Json(HttpStatusCode.OK, """{"refresh_token":"the-brand-new-token"}"""));
        var row = await harness.SeedDriveRowAsync("the-token-already-stored");
        row.LastTestStatus = "SUCCESS";
        row.LastTestedAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Unspecified);
        row.LastTestMessage = "Kết nối Google Drive thành công.";
        await harness.Db.SaveChangesAsync();

        await harness.Handle(new CompleteGoogleDriveOAuthCommand("an-auth-code", null, "a-valid-state"));

        var stored = await harness.DriveRowAsync();
        Assert.Null(stored!.LastTestStatus);
        Assert.Null(stored.LastTestedAt);
        Assert.Null(stored.LastTestMessage);
        Assert.Equal(AdminUserId, stored.UpdatedBy);
    }

    [Fact]
    public async Task A_reconnect_is_audited_against_the_admin_from_the_state()
    {
        var harness = new Harness(
            respond: _ => Json(HttpStatusCode.OK, """{"refresh_token":"the-brand-new-token"}"""));
        await harness.SeedDriveRowAsync("the-token-already-stored");

        await harness.Handle(new CompleteGoogleDriveOAuthCommand("an-auth-code", null, "a-valid-state"));

        var audit = Assert.Single(await harness.Db.AuditLogs.ToListAsync());
        Assert.Equal(GoogleDriveIntegrationConstants.AuditReconnect, audit.Action);
        Assert.Equal(GoogleDriveIntegrationConstants.AuditEntityType, audit.EntityType);
        Assert.Equal(AdminUserId, audit.ActorUserId);
    }

    /// <summary>A deployment with no Drive row at all gets one — with an id the audit row can point at.</summary>
    [Fact]
    public async Task A_first_connect_creates_the_well_known_row_and_audits_it_as_a_connect()
    {
        var harness = new Harness(
            respond: _ => Json(HttpStatusCode.OK, """{"refresh_token":"the-first-token"}"""));

        var result = await harness.Handle(
            new CompleteGoogleDriveOAuthCommand("an-auth-code", null, "a-valid-state"));

        Assert.True(result.Success);

        var row = Assert.Single(await harness.Db.ApiConfigurations.ToListAsync());
        Assert.Equal(GoogleDriveIntegrationConstants.ApiCode, row.ApiCode);
        Assert.Equal(GoogleDriveIntegrationConstants.Purpose, row.Purpose);
        Assert.Equal("the-first-token", await harness.StoredRefreshTokenAsync());

        var audit = Assert.Single(await harness.Db.AuditLogs.ToListAsync());
        Assert.Equal(GoogleDriveIntegrationConstants.AuditConnect, audit.Action);
        Assert.Equal(row.ApiConfigId, audit.EntityId);
        Assert.NotEqual(0ul, audit.EntityId);
    }

    /// <summary>Matching on api_code (UNIQUE) is what keeps a reconnect from fanning out into duplicates.</summary>
    [Fact]
    public async Task A_reconnect_never_adds_a_second_drive_row()
    {
        var harness = new Harness(
            respond: _ => Json(HttpStatusCode.OK, """{"refresh_token":"the-brand-new-token"}"""));
        await harness.SeedDriveRowAsync("the-token-already-stored");

        await harness.Handle(new CompleteGoogleDriveOAuthCommand("an-auth-code", null, "a-valid-state"));

        Assert.Single(await harness.Db.ApiConfigurations.ToListAsync());
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_host_with_no_oauth_client_configured_says_so_instead_of_calling_google()
    {
        var harness = new Harness(configure: o => o.ClientSecret = null);

        var result = await harness.Handle(
            new CompleteGoogleDriveOAuthCommand("an-auth-code", null, "a-valid-state"));

        Assert.False(result.Success);
        Assert.Equal(GoogleDriveOAuthRedirectReasons.ConfigMissing, result.Reason);
        Assert.Equal(0, harness.TokenExchangeCount);
    }

    /// <summary>
    /// Every refusal answers with a slug from the fixed vocabulary. The value goes into a redirect URL,
    /// which lands in browser history and proxy logs — so nothing Google said may reach it.
    /// </summary>
    [Fact]
    public async Task Every_failure_reason_comes_from_the_fixed_vocabulary()
    {
        var allowed = new HashSet<string>
        {
            GoogleDriveOAuthRedirectReasons.AccessDenied,
            GoogleDriveOAuthRedirectReasons.InvalidState,
            GoogleDriveOAuthRedirectReasons.StateExpired,
            GoogleDriveOAuthRedirectReasons.NoRefreshToken,
            GoogleDriveOAuthRedirectReasons.TokenExchangeFailed,
            GoogleDriveOAuthRedirectReasons.SaveFailed,
            GoogleDriveOAuthRedirectReasons.ConfigMissing,
        };

        var reasons = new List<string?>
        {
            (await new Harness(stateIsValid: false)
                .Handle(new CompleteGoogleDriveOAuthCommand("c", null, null))).Reason,
            (await new Harness()
                .Handle(new CompleteGoogleDriveOAuthCommand(null, "access_denied", "s"))).Reason,
            (await new Harness()
                .Handle(new CompleteGoogleDriveOAuthCommand(null, "server_error", "s"))).Reason,
            (await new Harness(configure: o => o.ClientId = null)
                .Handle(new CompleteGoogleDriveOAuthCommand("c", null, "s"))).Reason,
            (await new Harness(respond: _ => Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""))
                .Handle(new CompleteGoogleDriveOAuthCommand("c", null, "s"))).Reason,
            (await new Harness(respond: _ => Json(HttpStatusCode.OK, """{"access_token":"x"}"""))
                .Handle(new CompleteGoogleDriveOAuthCommand("c", null, "s"))).Reason,
        };

        Assert.All(reasons, reason => Assert.Contains(reason!, allowed));
    }

    // ── Harness ───────────────────────────────────────────────────────────────

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Harness
    {
        public ApiIntegrationsTestDbContext Db { get; } = ApiIntegrationsTestDbContext.Create();
        public int TokenExchangeCount { get; private set; }

        private readonly CompleteGoogleDriveOAuthCommandHandler _handler;
        private readonly StubSecretProtector _protector = new();

        public Harness(
            Func<HttpRequestMessage, HttpResponseMessage>? respond = null,
            bool stateIsValid = true,
            string? stateFailureReason = null,
            Action<GoogleDriveOptions>? configure = null)
        {
            var options = new GoogleDriveOptions
            {
                ClientId = "stub-client-id",
                ClientSecret = "stub-client-secret",
                RedirectUri = "https://api.example.test/api/google-drive/oauth/callback",
            };
            configure?.Invoke(options);

            _handler = new CompleteGoogleDriveOAuthCommandHandler(
                Db,
                _protector,
                new StubStateService(stateIsValid, stateFailureReason),
                new FixedClock(),
                new StubHttpClientFactory(request =>
                {
                    TokenExchangeCount++;
                    return (respond ?? DefaultRespond)(request);
                }),
                Options.Create(options),
                NullLogger<CompleteGoogleDriveOAuthCommandHandler>.Instance);
        }

        public Task<GoogleDriveOAuthCallbackResultDto> Handle(CompleteGoogleDriveOAuthCommand command)
            => _handler.Handle(command, CancellationToken.None);

        public async Task<ApiConfiguration> SeedDriveRowAsync(string refreshToken)
        {
            var row = new ApiConfiguration
            {
                ApiCode = GoogleDriveIntegrationConstants.ApiCode,
                Name = GoogleDriveIntegrationConstants.Name,
                BaseUrl = GoogleDriveIntegrationConstants.BaseUrl,
                CredentialsJsonEncrypted = _protector.Protect(
                    new GoogleDriveCredentialEnvelope { RefreshToken = refreshToken }.ToJson()),
            };
            Db.ApiConfigurations.Add(row);
            await Db.SaveChangesAsync();
            return row;
        }

        public Task<ApiConfiguration?> DriveRowAsync()
            => Db.ApiConfigurations.AsNoTracking()
                .FirstOrDefaultAsync(c => c.ApiCode == GoogleDriveIntegrationConstants.ApiCode);

        public async Task<string?> StoredRefreshTokenAsync()
        {
            var row = await DriveRowAsync();
            return row?.CredentialsJsonEncrypted is null
                ? null
                : GoogleDriveCredentialEnvelope.TryParse(
                    _protector.Unprotect(row.CredentialsJsonEncrypted))?.RefreshToken;
        }

        private static HttpResponseMessage DefaultRespond(HttpRequestMessage _)
            => Json(HttpStatusCode.OK, """{"refresh_token":"a-token"}""");
    }

    private sealed class StubStateService : IGoogleDriveOAuthStateService
    {
        private readonly bool _isValid;
        private readonly string? _failureReason;

        public StubStateService(bool isValid, string? failureReason)
        {
            _isValid = isValid;
            _failureReason = failureReason;
        }

        public string Create(ulong adminUserId) => "a-valid-state";

        public GoogleDriveOAuthStateValidation Validate(string? state) => _isValid
            ? new GoogleDriveOAuthStateValidation(
                true, new GoogleDriveOAuthState(AdminUserId, "nonce", DateTime.UtcNow.AddMinutes(5)), null)
            : new GoogleDriveOAuthStateValidation(
                false, null, _failureReason ?? GoogleDriveOAuthRedirectReasons.InvalidState);
    }

    private sealed class StubSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext)
            => "enc:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));

        public string Unprotect(string ciphertext)
            => Encoding.UTF8.GetString(Convert.FromBase64String(ciphertext[4..]));
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow { get; } = new(2026, 8, 7, 2, 0, 0, DateTimeKind.Utc);
        public DateTime VietnamNow { get; } = new(2026, 8, 7, 9, 0, 0, DateTimeKind.Unspecified);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> respond)
            => _respond = respond;

        public HttpClient CreateClient(string name) => new(new StubHandler(_respond));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_respond(request));
    }
}
