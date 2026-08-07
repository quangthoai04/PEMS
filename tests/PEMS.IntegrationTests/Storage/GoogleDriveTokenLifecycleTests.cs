using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;
using PEMS.Infrastructure.FileStorage.GoogleDrive;
using Xunit;

namespace PEMS.IntegrationTests.Storage;

/// <summary>
/// How the Drive client handles its credential across a call, driven entirely through a stub HTTP handler
/// — no network, no credentials, no real Drive.
///
/// <para>
/// <b>Why "refresh the expired access token and retry" is not a thing this client does.</b> It holds no
/// access token between calls. Every operation resolves the stored refresh token and exchanges it for a
/// fresh access token immediately before using it, so the token in flight is at most one request old and
/// there is no stale-token case to detect, no cache to invalidate, and no retry to schedule. That is the
/// property pinned below, because it is easy to "fix" by adding a cache and a retry loop and thereby
/// introduce exactly the staleness the design does not have — along with a refresh race between
/// concurrent uploads.
/// </para>
/// <para>
/// The refresh token itself now comes from <see cref="IGoogleDriveCredentialResolver"/> rather than from
/// the options object, because an administrator can reconnect the account at runtime. That makes
/// re-reading it per call load-bearing rather than merely tidy — see
/// <c>The_stored_credential_is_re_read_for_every_call</c>.
/// </para>
/// <para>
/// A 401 from Drive AFTER a token was just minted therefore does not mean "expired"; it means the grant
/// itself is refused, and retrying with another token from the same grant would fail identically. The
/// client does not retry, and that is asserted rather than assumed: an unbounded retry against an
/// authentication failure is how a broken credential becomes an outage.
/// </para>
/// </summary>
public sealed class GoogleDriveTokenLifecycleTests
{
    private const string TokenEndpointHost = "oauth2.googleapis.com";

    /// <summary>
    /// One token request, one upload, and the upload carries the token that was just minted.
    /// </summary>
    [Fact]
    public async Task An_upload_mints_one_fresh_token_and_uses_it_once()
    {
        var log = new List<HttpRequestMessage>();
        var service = Create(log, req => IsTokenRequest(req)
            ? Json(HttpStatusCode.OK, """{"access_token":"minted-token","expires_in":3599}""")
            : Json(HttpStatusCode.OK, """{"id":"drive-file-1","size":"8"}"""));

        var result = await service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1");

        Assert.Equal("drive-file-1", result.ExternalFileId);
        Assert.Equal(1, log.Count(IsTokenRequest));
        Assert.Equal(1, log.Count(r => !IsTokenRequest(r)));

        var upload = log.Single(r => !IsTokenRequest(r));
        Assert.Equal("Bearer", upload.Headers.Authorization?.Scheme);
        Assert.Equal("minted-token", upload.Headers.Authorization?.Parameter);
    }

    /// <summary>
    /// The exchange is a refresh_token grant against the configured refresh token — the thing that makes
    /// "the access token expired" un-representable rather than merely handled.
    /// </summary>
    [Fact]
    public async Task The_token_is_exchanged_from_the_refresh_token_on_every_call()
    {
        var log = new List<HttpRequestMessage>();
        var bodies = new List<string>();
        var service = Create(log, req =>
        {
            if (IsTokenRequest(req))
            {
                bodies.Add(req.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                return Json(HttpStatusCode.OK, """{"access_token":"minted-token"}""");
            }
            return Json(HttpStatusCode.OK, """{"id":"drive-file-1"}""");
        });

        await service.UploadFileAsync(Bytes(), "a.pdf", "application/pdf", "folder-1");
        await service.UploadFileAsync(Bytes(), "b.pdf", "application/pdf", "folder-1");

        // Two uploads, two exchanges. A cached token would make this 1 — and would then need an
        // expiry policy, an invalidation path and a concurrency story, none of which exist here.
        Assert.Equal(2, log.Count(IsTokenRequest));
        Assert.All(bodies, body =>
        {
            Assert.Contains("grant_type=refresh_token", body);
            Assert.Contains("refresh_token=stub-refresh-token", body);
        });
    }

    /// <summary>
    /// A refused upload is refused once. Retrying an authentication failure cannot help — the second
    /// attempt would present a token minted from the same rejected grant — and a loop here would turn one
    /// broken credential into sustained traffic against Google.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task A_refused_upload_is_not_retried(HttpStatusCode status)
    {
        var log = new List<HttpRequestMessage>();
        var service = Create(log, req => IsTokenRequest(req)
            ? Json(HttpStatusCode.OK, """{"access_token":"minted-token"}""")
            : Json(status, """{"error":{"message":"nope"}}"""));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.Equal(1, log.Count(IsTokenRequest));
        Assert.Equal(1, log.Count(r => !IsTokenRequest(r)));
    }

    /// <summary>
    /// A revoked grant stops at the token endpoint: nothing is uploaded, and the code says "reconnect"
    /// rather than "retry". This is the failure the setup-progress composer degrades around — see
    /// <c>VisitSetupProgressComposer</c>, which now returns the message without the report instead of
    /// letting this reach the Host as a dead flow.
    /// </summary>
    [Fact]
    public async Task A_revoked_grant_never_reaches_the_upload_endpoint()
    {
        var log = new List<HttpRequestMessage>();
        var service = Create(log, _ => Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.Equal(GoogleDriveErrorCodes.TokenExpired, ex.ErrorCode);
        Assert.Equal(1, log.Count(IsTokenRequest));
        Assert.DoesNotContain(log, r => !IsTokenRequest(r));
    }

    /// <summary>
    /// The stored refresh token is read again for every call, never remembered.
    ///
    /// <para>
    /// It now lives where an administrator can change it at runtime — the "kết nối lại Google Drive"
    /// action on the API configuration screen — so holding on to the one this service saw first would mean
    /// a reconnect changed nothing until somebody restarted the process. That is precisely the outage the
    /// reconnect screen exists to end, and it would present as "I reconnected and it still says the
    /// connection expired", which points the investigation at the screen rather than at the cache.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_stored_credential_is_re_read_for_every_call()
    {
        var log = new List<HttpRequestMessage>();
        var resolver = new CountingCredentialResolver(null);   // nothing connected yet
        var bodies = new List<string>();
        var service = Create(log, req =>
        {
            if (IsTokenRequest(req))
            {
                bodies.Add(req.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                return Json(HttpStatusCode.OK, """{"access_token":"minted-token"}""");
            }
            return Json(HttpStatusCode.OK, """{"id":"drive-file-1"}""");
        }, resolver);

        // Before the administrator connects: refused, and nothing is asked of Google.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "a.pdf", "application/pdf", "folder-1"));
        Assert.Equal(GoogleDriveErrorCodes.ConfigMissing, ex.ErrorCode);
        Assert.Empty(log);

        // The administrator reconnects. The very next upload works — no restart, no second attempt.
        resolver.Reconnect("reconnected-refresh-token");
        await service.UploadFileAsync(Bytes(), "a.pdf", "application/pdf", "folder-1");

        Assert.Equal(2, resolver.Calls);
        Assert.Contains("refresh_token=reconnected-refresh-token", Assert.Single(bodies));
    }

    /// <summary>
    /// No credential reaches the message a caller sees. The refusal has to be readable by a Host and
    /// forwardable to an administrator, and a token or secret quoted into it would travel with it.
    /// </summary>
    [Fact]
    public async Task A_refusal_quotes_no_credential()
    {
        var service = Create(new List<HttpRequestMessage>(), req => IsTokenRequest(req)
            ? Json(HttpStatusCode.OK, """{"access_token":"minted-token"}""")
            : Json(HttpStatusCode.Unauthorized, """{"error":"unauthorized"}"""));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.DoesNotContain("minted-token", ex.Message);
        Assert.DoesNotContain("stub-refresh-token", ex.Message);
        Assert.DoesNotContain("stub-client-secret", ex.Message);
    }

    // ── Harness ───────────────────────────────────────────────────────────────

    private static byte[] Bytes() => "%PDF-1.4"u8.ToArray();

    private static bool IsTokenRequest(HttpRequestMessage request)
        => request.RequestUri?.Host == TokenEndpointHost;

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static GoogleDriveStorageService Create(
        List<HttpRequestMessage> log,
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        CountingCredentialResolver? resolver = null)
        => new(
            Options.Create(new GoogleDriveOptions
            {
                ClientId = "stub-client-id",
                ClientSecret = "stub-client-secret",
                RootFolderId = "stub-root-folder",
                AvatarFolderId = "stub-avatar-folder",
            }),
            resolver ?? new CountingCredentialResolver("stub-refresh-token"),
            new RecordingHttpClientFactory(log, respond),
            NullLogger<GoogleDriveStorageService>.Instance);

    /// <summary>
    /// Stands in for the admin-managed credential store, and counts how often it is asked.
    ///
    /// <para>
    /// The count is the assertion in <c>The_stored_credential_is_re_read_for_every_call</c>: the refresh
    /// token now lives in configuration an administrator can change at runtime, so caching it in this
    /// service would mean a reconnect did not take effect until the process restarted — the failure the
    /// reconnect screen exists to end.
    /// </para>
    /// </summary>
    private sealed class CountingCredentialResolver : IGoogleDriveCredentialResolver
    {
        private string? _refreshToken;

        public CountingCredentialResolver(string? refreshToken) => _refreshToken = refreshToken;

        public int Calls { get; private set; }

        /// <summary>Models an administrator reconnecting between two uploads.</summary>
        public void Reconnect(string? refreshToken) => _refreshToken = refreshToken;

        public Task<string?> ResolveRefreshTokenAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(_refreshToken);
        }
    }

    /// <summary>Records every request before answering it, so a test can count them.</summary>
    private sealed class RecordingHttpClientFactory : IHttpClientFactory
    {
        private readonly List<HttpRequestMessage> _log;
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public RecordingHttpClientFactory(
            List<HttpRequestMessage> log, Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _log = log;
            _respond = respond;
        }

        public HttpClient CreateClient(string name) => new(new RecordingHandler(_log, _respond));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly List<HttpRequestMessage> _log;
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public RecordingHandler(
            List<HttpRequestMessage> log, Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _log = log;
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _log.Add(request);
            return Task.FromResult(_respond(request));
        }
    }
}
