using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Storage;
using PEMS.Infrastructure.FileStorage.GoogleDrive;
using Xunit;

namespace PEMS.IntegrationTests.Storage;

/// <summary>
/// How a Google Drive failure is classified, driven entirely through a stub HTTP handler — no network,
/// no credentials, no real Drive.
///
/// <para>
/// This is the incident these tests exist for. A Host pressing "Gửi cập nhật chuẩn bị" was told
/// "Không thể kết nối Google Drive. Vui lòng thử lại." That one sentence, under one error code, was the
/// answer to at least four unrelated situations: the machine had no route to Google, Google rejected
/// the client secret, Google returned a 500, and Google returned a token response with no token in it.
/// Only the first and third are worth retrying; the other two need somebody to change configuration,
/// and none of the four was distinguishable from the others in the response, the log, or the UI.
/// </para>
/// <para>
/// One more thing was wrong and is pinned below: the missing-token branch threw the code
/// <c>UPLOAD_AVATAR_FAILED</c> — inherited from the feature this client was first written for — on a
/// path that every Drive upload in the system goes through, including the schedule report.
/// </para>
/// </summary>
public sealed class GoogleDriveErrorClassificationTests
{
    private const string TokenEndpointHost = "oauth2.googleapis.com";

    [Fact]
    public async Task No_route_to_Google_is_unavailable_not_an_auth_failure()
    {
        var service = Create(_ => throw new HttpRequestException("No such host is known."));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.Equal(GoogleDriveErrorCodes.Unavailable, ex.ErrorCode);
    }

    [Fact]
    public async Task A_revoked_refresh_token_is_reported_as_expired()
    {
        var service = Create(_ => Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.Equal(GoogleDriveErrorCodes.TokenExpired, ex.ErrorCode);
    }

    /// <summary>
    /// A rejected client secret is a configuration fault. It must NOT be reported as "thử lại": no
    /// number of retries fixes a wrong secret, and telling the Host to keep pressing the button is how
    /// this reached an incident report instead of whoever owns the OAuth client.
    /// </summary>
    [Fact]
    public async Task A_rejected_client_secret_is_an_auth_failure()
    {
        var service = Create(_ => Json(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}"""));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.Equal(GoogleDriveErrorCodes.AuthFailed, ex.ErrorCode);
        Assert.DoesNotContain("thử lại", ex.Message);
    }

    /// <summary>Google having a bad minute IS retryable, and must not look like a broken credential.</summary>
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_transient_token_endpoint_failure_is_unavailable(HttpStatusCode status)
    {
        var service = Create(_ => Json(status, """{"error":"backendError"}"""));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.Equal(GoogleDriveErrorCodes.Unavailable, ex.ErrorCode);
    }

    /// <summary>
    /// The regression that gave a PDF upload an avatar's error code. Any code would be "wrong" here in
    /// the sense of being unhelpful; this one actively misdirected, because it named a feature the
    /// caller was not using.
    /// </summary>
    [Fact]
    public async Task A_token_response_with_no_token_no_longer_reports_an_avatar_failure()
    {
        var service = Create(_ => Json(HttpStatusCode.OK, """{"expires_in":3599}"""));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.NotEqual("UPLOAD_AVATAR_FAILED", ex.ErrorCode);
        Assert.Equal(GoogleDriveErrorCodes.Unavailable, ex.ErrorCode);
    }

    [Fact]
    public async Task A_non_JSON_two_hundred_is_unavailable_not_an_auth_failure()
    {
        var service = Create(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>captive portal</html>", Encoding.UTF8, "text/html"),
        });

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.Equal(GoogleDriveErrorCodes.Unavailable, ex.ErrorCode);
    }

    // ── Failures AFTER a token was successfully minted ────────────────────────

    [Fact]
    public async Task A_missing_target_folder_names_the_folder_not_the_connection()
    {
        var service = Create(req => IsTokenRequest(req)
            ? Json(HttpStatusCode.OK, """{"access_token":"stub-token"}""")
            : Json(HttpStatusCode.NotFound, """{"error":{"errors":[{"reason":"notFound"}]}}"""));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.Equal(GoogleDriveErrorCodes.FolderNotFoundOrNoPermission, ex.ErrorCode);
    }

    [Fact]
    public async Task A_forbidden_upload_is_a_permission_problem_not_a_generic_upload_failure()
    {
        var service = Create(req => IsTokenRequest(req)
            ? Json(HttpStatusCode.OK, """{"access_token":"stub-token"}""")
            : Json(HttpStatusCode.Forbidden, """{"error":{"message":"insufficientPermissions"}}"""));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.Equal(GoogleDriveErrorCodes.FolderNotFoundOrNoPermission, ex.ErrorCode);
    }

    [Fact]
    public async Task A_five_hundred_from_the_upload_endpoint_is_unavailable()
    {
        var service = Create(req => IsTokenRequest(req)
            ? Json(HttpStatusCode.OK, """{"access_token":"stub-token"}""")
            : Json(HttpStatusCode.BadGateway, "upstream error"));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.Equal(GoogleDriveErrorCodes.Unavailable, ex.ErrorCode);
    }

    // ── Configuration, before anything is asked of Google ─────────────────────

    [Fact]
    public async Task A_missing_refresh_token_is_a_configuration_fault()
    {
        var service = Create(
            _ => throw new InvalidOperationException("The token endpoint must not be called."),
            options => options.RefreshToken = null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "bao-cao.pdf", "application/pdf", "folder-1"));

        Assert.Equal(GoogleDriveErrorCodes.ConfigMissing, ex.ErrorCode);
    }

    /// <summary>
    /// The property the whole exercise turns on: every distinguishable cause has a distinguishable
    /// code. A future contributor who reaches for a convenient existing code will fail this.
    /// </summary>
    [Fact]
    public async Task The_causes_are_told_apart_by_code()
    {
        var codes = new[]
        {
            await CodeOf(Create(_ => throw new HttpRequestException("no route"))),
            await CodeOf(Create(_ => Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""))),
            await CodeOf(Create(_ => Json(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}"""))),
            await CodeOf(Create(
                _ => throw new InvalidOperationException("unreachable"),
                o => o.RefreshToken = null)),
        };

        Assert.Equal(codes.Length, codes.Distinct().Count());
    }

    // ── Harness ───────────────────────────────────────────────────────────────

    private static async Task<string?> CodeOf(GoogleDriveStorageService service)
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UploadFileAsync(Bytes(), "f.pdf", "application/pdf", "folder-1"));
        return ex.ErrorCode;
    }

    private static byte[] Bytes() => "%PDF-1.4"u8.ToArray();

    private static bool IsTokenRequest(HttpRequestMessage request)
        => request.RequestUri?.Host == TokenEndpointHost;

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static GoogleDriveStorageService Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        Action<GoogleDriveOptions>? configure = null)
    {
        var options = new GoogleDriveOptions
        {
            ClientId = "stub-client-id",
            ClientSecret = "stub-client-secret",
            RefreshToken = "stub-refresh-token",
            RootFolderId = "stub-root-folder",
            AvatarFolderId = "stub-avatar-folder",
        };
        configure?.Invoke(options);

        return new GoogleDriveStorageService(
            Options.Create(options),
            new StubHttpClientFactory(respond),
            NullLogger<GoogleDriveStorageService>.Instance);
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
