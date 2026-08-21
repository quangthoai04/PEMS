using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.ApiIntegrations.Commands.TestApiIntegration;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.BusinessCardOcr.Services;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Services;
using PEMS.Application.News.Services;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.ApiIntegrations;

/// <summary>
/// "Test Connection" for a Resend configuration must report the SAME classified cause a real send would
/// (<see cref="ResendDeliveryClassifier"/>) — never the generic <c>RESEND_TEST_FAILED</c>/
/// <c>RESEND_TEST_EXCEPTION</c> the button used to collapse every failure into. Driven through a stub
/// <see cref="IHttpClientFactory"/>; no real Resend call.
/// </summary>
public sealed class TestApiIntegrationResendClassificationTests
{
    private const ulong AdminUserId = 1;
    private const ulong ConfigId = 900;

    [Fact]
    public async Task A_rate_limited_test_reports_the_rate_limited_code_not_a_generic_failure()
    {
        var result = await Handle(_ => Json(HttpStatusCode.TooManyRequests, """{"name":"rate_limit_exceeded"}"""));

        Assert.False(result.Success);
        Assert.Equal(ResendDeliveryCodes.RateLimited, result.ErrorCode);
        Assert.NotEqual("RESEND_TEST_FAILED", result.ErrorCode);
    }

    [Fact]
    public async Task A_daily_quota_test_reports_the_daily_quota_code()
    {
        var result = await Handle(_ => Json(HttpStatusCode.TooManyRequests, """{"name":"daily_quota_exceeded"}"""));

        Assert.False(result.Success);
        Assert.Equal(ResendDeliveryCodes.DailyQuotaExceeded, result.ErrorCode);
    }

    [Fact]
    public async Task A_monthly_quota_test_reports_the_monthly_quota_code()
    {
        var result = await Handle(_ => Json(HttpStatusCode.TooManyRequests, """{"name":"monthly_quota_exceeded"}"""));

        Assert.False(result.Success);
        Assert.Equal(ResendDeliveryCodes.MonthlyQuotaExceeded, result.ErrorCode);
    }

    [Fact]
    public async Task An_auth_failure_test_reports_the_auth_failed_code()
    {
        var result = await Handle(_ => Json(HttpStatusCode.Unauthorized, """{"name":"invalid_api_key"}"""));

        Assert.False(result.Success);
        Assert.Equal(ResendDeliveryCodes.AuthFailed, result.ErrorCode);
    }

    [Fact]
    public async Task A_sender_rejection_test_reports_the_sender_rejected_code()
    {
        var result = await Handle(_ => Json(HttpStatusCode.Forbidden, """{"name":"invalid_from_address"}"""));

        Assert.False(result.Success);
        Assert.Equal(ResendDeliveryCodes.SenderRejected, result.ErrorCode);
    }

    [Fact]
    public async Task A_validation_failure_test_reports_the_request_invalid_code()
    {
        var result = await Handle(_ => Json((HttpStatusCode)422, """{"name":"validation_error"}"""));

        Assert.False(result.Success);
        Assert.Equal(ResendDeliveryCodes.RequestInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task A_network_exception_reports_network_unknown_not_a_generic_exception_code()
    {
        var result = await Handle(_ => throw new HttpRequestException("connection reset"));

        Assert.False(result.Success);
        Assert.Equal(ResendDeliveryCodes.NetworkUnknown, result.ErrorCode);
        Assert.NotEqual("RESEND_TEST_EXCEPTION", result.ErrorCode);
    }

    [Fact]
    public async Task A_successful_test_reports_success_with_the_provider_message_id()
    {
        var result = await Handle(_ => Json(HttpStatusCode.OK, """{"id":"resend-test-1"}"""));

        Assert.True(result.Success);
        Assert.Null(result.ErrorCode);
        Assert.Contains("resend-test-1", result.Message);
    }

    [Fact]
    public async Task Test_connection_never_retries_a_rate_limited_response()
    {
        var handler = new CountingHandler(_ => Json(HttpStatusCode.TooManyRequests, """{"name":"rate_limit_exceeded"}"""));
        await Handle(handler);

        Assert.Equal(1, handler.CallCount);
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static Task<ApiConnectionTestResultDto> Handle(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => Handle(new CountingHandler(respond));

    private static async Task<ApiConnectionTestResultDto> Handle(CountingHandler handler)
    {
        var db = ApiIntegrationsTestDbContext.Create();
        db.ApiConfigurations.Add(new ApiConfiguration
        {
            ApiConfigId = ConfigId,
            ApiCode = ResendEmailConstants.ApiCode,
            Name = "Resend - test",
            ProviderName = ResendEmailConstants.ProviderName,
            Purpose = ResendEmailConstants.Purpose,
            BaseUrl = "https://api.resend.test",
            AuthType = ResendEmailConstants.AuthType,
            BearerTokenEncrypted = "fake-api-key",
            Status = "INACTIVE",
            TimeoutSeconds = 30,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        var handlerObj = new TestApiIntegrationCommandHandler(
            db,
            new StubCurrentUser(),
            new StubClock(),
            new UnusedBusinessCardOcrProvider(),
            new PassthroughCredentialResolver(),
            new UnusedNewsTranslationService(),
            new UnusedFaceDetectionProvider(),
            new PassthroughSecretProtector(),
            new UnusedGoogleDriveStorageService(),
            new StubHttpClientFactory(handler));

        return await handlerObj.Handle(
            new TestApiIntegrationCommand(ConfigId), CancellationToken.None);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public int CallCount { get; private set; }
        public CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_respond(request));
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class PassthroughSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => ciphertext;
    }

    private sealed class PassthroughCredentialResolver : IOcrCredentialResolver
    {
        public string? Resolve(ApiConfiguration config) => null;
    }

    private sealed class StubCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId => AdminUserId;
        public string? Email => "admin@fpt.edu.vn";
        public ulong? RoleId => 1;
        public string? RoleCode => "ADMIN";
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => 1;
        public string? LoginPortal => "STAFF";
    }

    private sealed class StubClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => DateTime.UtcNow.AddHours(7);
    }

    // ── Unused for the Resend branch — every member throws if ever reached ────

    private sealed class UnusedBusinessCardOcrProvider : IBusinessCardOcrProvider
    {
        public Task<BusinessCardOcrProviderResult> ExtractAsync(
            BusinessCardOcrProviderInput input, OcrProviderRuntimeConfig config, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");

        public Task<BusinessCardOcrConnectionTestResult> TestConnectionAsync(
            OcrProviderRuntimeConfig config, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");
    }

    private sealed class UnusedNewsTranslationService : INewsTranslationService
    {
        public Task<IReadOnlyList<string>> TranslateTextAsync(
            IReadOnlyList<string> contents, string sourceLanguage, string targetLanguage,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");

        public Task<IReadOnlyList<string>> TranslateHtmlAsync(
            IReadOnlyList<string> contents, string sourceLanguage, string targetLanguage,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");

        public Task<NewsTranslationConnectionTestResult> TestConnectionAsync(
            string projectId, string location, string credentialJson, int timeoutSeconds,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");
    }

    private sealed class UnusedFaceDetectionProvider : IFaceDetectionProvider
    {
        public Task<FaceDetectionProviderResult> DetectFacesAsync(
            FaceDetectionProviderInput input, FaceDetectionRuntimeConfig config, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");

        public Task<FaceDetectionConnectionTestResult> TestConnectionAsync(
            FaceDetectionRuntimeConfig config, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");
    }

    private sealed class UnusedGoogleDriveStorageService : IGoogleDriveStorageService
    {
        public Task<GoogleDriveUploadResult> UploadAvatarAsync(
            byte[] content, string driveFileName, string contentType, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");

        public Task<GoogleDriveUploadResult> UploadFileAsync(
            byte[] content, string driveFileName, string contentType, string? folderId = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");

        public Task<Stream> DownloadAsync(string externalFileId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");

        public Task<GoogleDriveDownloadResult> DownloadRangeAsync(
            string externalFileId, long? from, long? to, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");

        public Task DeleteAsync(string externalFileId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");

        public Task<GoogleDriveFolderResult> EnsureChildFolderAsync(
            string folderName, string parentFolderId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");

        public Task<string> CheckConnectionAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Not used by the Resend test-connection path.");
    }
}
