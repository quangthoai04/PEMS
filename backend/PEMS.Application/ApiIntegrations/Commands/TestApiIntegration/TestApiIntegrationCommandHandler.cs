using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.BusinessCardOcr.Services;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Services;
using PEMS.Domain.Entities.ApiIntegrations;

namespace PEMS.Application.ApiIntegrations.Commands.TestApiIntegration;

public sealed class TestApiIntegrationCommandHandler
    : IRequestHandler<TestApiIntegrationCommand, ApiConnectionTestResultDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IBusinessCardOcrProvider _provider;
    private readonly IOcrCredentialResolver _credentialResolver;
    private readonly PEMS.Application.News.Services.INewsTranslationService _translationService;
    private readonly IFaceDetectionProvider _faceDetectionProvider;
    private readonly ISecretProtector _secretProtector;
    private readonly IGoogleDriveStorageService _googleDrive;
    private readonly IHttpClientFactory _httpClientFactory;

    public TestApiIntegrationCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IBusinessCardOcrProvider provider, IOcrCredentialResolver credentialResolver,
        PEMS.Application.News.Services.INewsTranslationService translationService,
        IFaceDetectionProvider faceDetectionProvider,
        ISecretProtector secretProtector,
        IGoogleDriveStorageService googleDrive,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _provider = provider;
        _credentialResolver = credentialResolver;
        _translationService = translationService;
        _faceDetectionProvider = faceDetectionProvider;
        _secretProtector = secretProtector;
        _googleDrive = googleDrive;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ApiConnectionTestResultDto> Handle(
        TestApiIntegrationCommand request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureManage(_currentUser);

        var config = await _db.ApiConfigurations
            .FirstOrDefaultAsync(c => c.ApiConfigId == request.ApiConfigId && c.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("ApiConfiguration", request.ApiConfigId);

        // Drive is matched on api_code because its purpose column carries a description, not a constant.
        var isGoogleDrive = config.ApiCode == GoogleDriveIntegrationConstants.ApiCode;

        if (!isGoogleDrive
            && config.Purpose != BusinessCardOcrConstants.Purpose
            && config.Purpose != NewsTranslationConstants.Purpose
            && config.Purpose != FaceDetectionConstants.Purpose
            && config.Purpose != ResendEmailConstants.Purpose)
            throw new BusinessRuleException(
                "Chỉ hỗ trợ test kết nối cho cấu hình BUSINESS_CARD_OCR, NEWS_TRANSLATION, FACE_DETECTION, "
                + "EMAIL_DELIVERY hoặc GOOGLE_DRIVE_STORAGE.",
                ApiIntegrationErrorCodes.InvalidPurpose);

        var now = _clock.VietnamNow;
        var stopwatch = Stopwatch.StartNew();
        bool success;
        string message;
        string? errorCode = null;

        if (isGoogleDrive)
        {
            var testResult = await ExecuteGoogleDriveTestAsync(cancellationToken);
            success = testResult.Success;
            message = testResult.Message;
            errorCode = testResult.ErrorCode;
        }
        else if (config.Purpose == ResendEmailConstants.Purpose)
        {
            if (string.IsNullOrEmpty(config.BearerTokenEncrypted))
            {
                success = false;
                message = "Chưa cấu hình Resend API Key.";
                errorCode = ApiIntegrationErrorCodes.CredentialRequired;
            }
            else
            {
                var adminEmail = _currentUser.Email;
                if (string.IsNullOrWhiteSpace(adminEmail) && _currentUser.UserId.HasValue)
                {
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId, cancellationToken);
                    adminEmail = user?.Email;
                }

                if (string.IsNullOrWhiteSpace(adminEmail))
                {
                    success = false;
                    message = "Không tìm thấy địa chỉ email của tài khoản ADMIN đang thao tác.";
                    errorCode = "ADMIN_EMAIL_REQUIRED";
                }
                else
                {
                    var testResult = await ExecuteResendTestAsync(config, adminEmail, cancellationToken);
                    success = testResult.Success;
                    message = testResult.Message;
                    errorCode = testResult.ErrorCode;
                }
            }
        }
        else
        {
            var credential = _credentialResolver.Resolve(config);
            if (string.IsNullOrEmpty(credential))
            {
                success = false;
                message = "Chưa cấu hình credential (serviceAccountJson/secretRef).";
                errorCode = ApiIntegrationErrorCodes.CredentialRequired;
            }
            else if (config.Purpose == NewsTranslationConstants.Purpose)
            {
                var settings = OcrProviderSettings.Parse(config.SettingsJson);
                var result = await _translationService.TestConnectionAsync(
                    settings.ProjectId, settings.Location, credential, config.TimeoutSeconds, cancellationToken);
                success = result.Success;
                message = result.Message;
                errorCode = result.ErrorCode;
            }
            else if (config.Purpose == FaceDetectionConstants.Purpose)
            {
                var runtime = new FaceDetectionRuntimeConfig
                {
                    ApiConfigId = config.ApiConfigId,
                    Settings = FaceDetectionProviderSettings.Parse(config.SettingsJson),
                    CredentialJson = credential,
                    TimeoutSeconds = config.TimeoutSeconds,
                };
                var result = await _faceDetectionProvider.TestConnectionAsync(runtime, cancellationToken);
                success = result.Success;
                message = result.Message;
                errorCode = result.ErrorCode;
            }
            else
            {
                var runtime = new OcrProviderRuntimeConfig
                {
                    ApiConfigId = config.ApiConfigId,
                    Settings = OcrProviderSettings.Parse(config.SettingsJson),
                    CredentialJson = credential,
                    TimeoutSeconds = config.TimeoutSeconds,
                };
                var result = await _provider.TestConnectionAsync(runtime, cancellationToken);
                success = result.Success;
                message = result.Message;
                errorCode = result.ErrorCode;
            }
        }
        stopwatch.Stop();

        config.LastTestStatus = success ? "SUCCESS" : "FAILED";
        config.LastTestedAt = now;
        config.LastTestMessage = message;
        config.UpdatedAt = now;
        config.UpdatedBy = _currentUser.UserId;

        // Sanitized log — endpoint + outcome only, never credentials/headers.
        _db.ApiRequestLogs.Add(new ApiRequestLog
        {
            ApiConfigId = config.ApiConfigId,
            RequestedBy = _currentUser.UserId,
            RelatedType = "CONNECTION_TEST",
            Endpoint = config.Purpose switch
            {
                _ when isGoogleDrive => $"{config.BaseUrl}/files/(root folder) (test)",
                _ when config.Purpose == ResendEmailConstants.Purpose => $"{config.BaseUrl}/emails (test)",
                _ when config.Purpose == NewsTranslationConstants.Purpose => $"{config.BaseUrl}/v3/.../:translateText (test)",
                _ when config.Purpose == FaceDetectionConstants.Purpose => $"{config.BaseUrl}/v1/images:annotate (test)",
                _ => $"{config.BaseUrl}/v1/.../processors/(get)",
            },
            Method = "POST",
            HttpStatus = success ? 200 : null,
            ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
            Success = success,
            ErrorCode = errorCode,
            ErrorMessage = success ? null : message,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new ApiConnectionTestResultDto
        {
            Success = success,
            Message = message,
            ErrorCode = errorCode,
            ResponseTimeMs = stopwatch.ElapsedMilliseconds,
            TestedAt = now,
        };
    }

    /// <summary>
    /// Exercises the whole Drive chain — resolve the stored refresh token, mint an access token, read the
    /// configured root folder — rather than checking that a credential column is non-empty. A stored token
    /// that Google has since revoked would pass the cheap check and fail every upload, which is precisely
    /// the state this button exists to detect.
    ///
    /// <para>
    /// Failures keep the code the storage client classified them as, so the console can distinguish
    /// "kết nối đã hết hạn — reconnect" from "sai client secret — fix the deployment" from "Google is
    /// down — wait". Collapsing them into one message is the bug <c>GoogleDriveErrorCodes</c> was written
    /// to end, and it must not come back at the one screen whose entire job is diagnosis.
    /// </para>
    /// </summary>
    private async Task<(bool Success, string Message, string? ErrorCode)> ExecuteGoogleDriveTestAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var rootFolderName = await _googleDrive.CheckConnectionAsync(cancellationToken);
            return (true, $"Kết nối Google Drive thành công. Đọc được thư mục gốc \"{rootFolderName}\".", null);
        }
        catch (BusinessRuleException ex)
        {
            // Already classified and already safe to show: these messages name the repair, and none of
            // them is built from a token, a secret or a Google payload.
            return (false, ex.Message, ex.ErrorCode);
        }
    }

    /// <summary>
    /// Exercises exactly the same HTTP call <see cref="PEMS.Infrastructure.Email.ResendEmailService"/>
    /// makes for a real send — same base URL, same auth header, same
    /// <see cref="ResendDeliveryClassifier"/> — so this button can never disagree with what a real send
    /// would report. It does not retry: Test Connection is a diagnostic the admin presses once and reads
    /// once, and auto-retrying here would mask exactly the causes (quota, auth) this button exists to
    /// surface immediately.
    /// </summary>
    private async Task<(bool Success, string Message, string? ErrorCode)> ExecuteResendTestAsync(
        ApiConfiguration config, string adminEmail, CancellationToken cancellationToken)
    {
        string apiKey;
        try
        {
            apiKey = _secretProtector.Unprotect(config.BearerTokenEncrypted!);
        }
        catch (Exception)
        {
            return (false, "Không thể giải mã Resend API key.", EmailDeliveryCodes.ResendCredentialError);
        }

        var settings = ResendProviderSettings.Parse(config.SettingsJson);
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? ResendEmailConstants.BaseUrl : config.BaseUrl.TrimEnd('/');

        var fromEmail = !string.IsNullOrWhiteSpace(settings.FromEmail) ? settings.FromEmail : "no-reply@mail.pems-fpt.site";
        var fromName = !string.IsNullOrWhiteSpace(settings.FromName) ? settings.FromName : "PEMS";
        var from = !string.IsNullOrWhiteSpace(fromName) ? $"{fromName} <{fromEmail}>" : fromEmail;

        var payload = new
        {
            from,
            to = new[] { adminEmail },
            subject = "PEMS — Kiểm tra kết nối Resend",
            html = "<p>Email kiểm tra kết nối từ hệ thống PEMS tới Resend API thành công.</p>"
        };

        try
        {
            using var client = _httpClientFactory.CreateClient("ResendEmailService");
            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 30);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/emails");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(httpRequest, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseText);
                if (doc.RootElement.TryGetProperty("id", out var idProp) && !string.IsNullOrEmpty(idProp.GetString()))
                {
                    return (true, $"Kết nối Resend thành công. Email test đã gửi tới {adminEmail} (ID: {idProp.GetString()}).", null);
                }

                // A 2xx with no usable id is still not proof of acceptance — classify it rather than
                // reporting a success PEMS cannot back up.
                var unclassified = ResendDeliveryClassifier.ClassifyResponse((int)response.StatusCode, responseText, null);
                return (false, unclassified.SafeMessage, unclassified.Code);
            }

            var retryAfterRaw = response.Headers.TryGetValues("Retry-After", out var values)
                ? values.FirstOrDefault()
                : null;
            var classified = ResendDeliveryClassifier.ClassifyResponse(
                (int)response.StatusCode, responseText, retryAfterRaw);

            return (false, classified.SafeMessage, classified.Code);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var classified = ResendDeliveryClassifier.ClassifyException(ex);
            return (false, classified.SafeMessage, classified.Code);
        }
    }
}
