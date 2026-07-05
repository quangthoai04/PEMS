using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.BusinessCardOcr.Services;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.News.Services;
using PEMS.Infrastructure.Ocr;

namespace PEMS.Infrastructure.Translation;

/// <summary>
/// Google Cloud Translation v3 (REST) provider for News auto-translation.
/// Configuration comes from the api_configurations row with api_code
/// <see cref="NewsTranslationConstants.ApiCode"/> (settings_json: projectId/location,
/// credentials via encrypted column → secret_ref env → GOOGLE_APPLICATION_CREDENTIALS),
/// falling back to the GOOGLE_TRANSLATION_PROJECT_ID env var when no row exists.
/// Never logs payloads or credentials.
/// </summary>
public sealed class GoogleNewsTranslationService : INewsTranslationService
{
    private const string TranslateEndpoint = "https://translation.googleapis.com/v3";

    private readonly IApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleServiceAccountTokenProvider _tokenProvider;
    // Credential resolution order is identical to OCR's (encrypted column → secret_ref env
    // → GOOGLE_APPLICATION_CREDENTIALS), so the same resolver is reused on purpose.
    private readonly IOcrCredentialResolver _credentialResolver;

    public GoogleNewsTranslationService(
        IApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        GoogleServiceAccountTokenProvider tokenProvider,
        IOcrCredentialResolver credentialResolver)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _credentialResolver = credentialResolver;
    }

    public Task<IReadOnlyList<string>> TranslateTextAsync(
        IReadOnlyList<string> contents, string sourceLanguage, string targetLanguage,
        CancellationToken cancellationToken)
        => TranslateAsync(contents, sourceLanguage, targetLanguage, "text/plain", cancellationToken);

    public Task<IReadOnlyList<string>> TranslateHtmlAsync(
        IReadOnlyList<string> contents, string sourceLanguage, string targetLanguage,
        CancellationToken cancellationToken)
        => TranslateAsync(contents, sourceLanguage, targetLanguage, "text/html", cancellationToken);

    public async Task<NewsTranslationConnectionTestResult> TestConnectionAsync(
        string projectId, string location, string credentialJson, int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectId))
                return new NewsTranslationConnectionTestResult
                {
                    Success = false,
                    Message = "Thiếu projectId trong cấu hình.",
                    ErrorCode = "TRANSLATION_SETTINGS_INCOMPLETE",
                };

            var result = await TranslateCoreAsync(
                new[] { "Xin chào" }, "vi", "en", "text/plain",
                projectId,
                string.IsNullOrWhiteSpace(location) ? "global" : location,
                credentialJson, timeoutSeconds, cancellationToken);

            return new NewsTranslationConnectionTestResult
            {
                Success = true,
                Message = $"Kết nối Google Cloud Translation thành công (\"Xin chào\" → \"{result[0]}\").",
            };
        }
        catch (BusinessRuleException ex)
        {
            return new NewsTranslationConnectionTestResult
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = ex.ErrorCode,
            };
        }
        catch (Exception)
        {
            return new NewsTranslationConnectionTestResult
            {
                Success = false,
                Message = "Không thể kết nối Google Cloud Translation (network/credential).",
                ErrorCode = "TRANSLATION_PROVIDER_ERROR",
            };
        }
    }

    private async Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> contents, string sourceLanguage, string targetLanguage,
        string mimeType, CancellationToken cancellationToken)
    {
        if (contents.Count == 0) return Array.Empty<string>();

        var (projectId, location, credentialJson, timeoutSeconds) =
            await ResolveConfigAsync(cancellationToken);

        return await TranslateCoreAsync(
            contents, sourceLanguage, targetLanguage, mimeType,
            projectId, location, credentialJson, timeoutSeconds, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> TranslateCoreAsync(
        IReadOnlyList<string> contents, string sourceLanguage, string targetLanguage,
        string mimeType, string projectId, string location, string credentialJson,
        int timeoutSeconds, CancellationToken cancellationToken)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(credentialJson, cancellationToken);

        var url = $"{TranslateEndpoint}/projects/{projectId}/locations/{location}:translateText";
        var payload = JsonSerializer.Serialize(new
        {
            contents,
            mimeType,
            sourceLanguageCode = sourceLanguage,
            targetLanguageCode = targetLanguage,
        });

        var http = _httpClientFactory.CreateClient(nameof(GoogleNewsTranslationService));
        http.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 120));

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new BusinessRuleException(
                $"Dịch tự động thất bại (Google Translation trả về HTTP {(int)response.StatusCode}). Vui lòng kiểm tra cấu hình API dịch.",
                "TRANSLATION_PROVIDER_ERROR");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("translations", out var translationsEl)
            || translationsEl.ValueKind != JsonValueKind.Array)
            throw new BusinessRuleException(
                "Google Translation không trả về kết quả dịch hợp lệ.", "TRANSLATION_PROVIDER_ERROR");

        var results = translationsEl.EnumerateArray()
            .Select(t => t.TryGetProperty("translatedText", out var txt) ? txt.GetString() ?? string.Empty : string.Empty)
            .ToList();

        if (results.Count != contents.Count)
            throw new BusinessRuleException(
                "Số lượng kết quả dịch không khớp với nội dung gửi đi.", "TRANSLATION_PROVIDER_ERROR");

        return results;
    }

    private async Task<(string ProjectId, string Location, string CredentialJson, int TimeoutSeconds)>
        ResolveConfigAsync(CancellationToken cancellationToken)
    {
        var config = await _db.ApiConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.ApiCode == NewsTranslationConstants.ApiCode
                && c.DeletedAt == null
                && c.Status == ApiIntegrationStatuses.Active, cancellationToken);

        string? projectId = null;
        var location = "global";
        string? credentialJson = null;
        var timeoutSeconds = 30;

        if (config is not null)
        {
            timeoutSeconds = config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 30;
            if (!string.IsNullOrWhiteSpace(config.SettingsJson))
            {
                try
                {
                    using var settings = JsonDocument.Parse(config.SettingsJson);
                    var root = settings.RootElement;
                    // Admin module writes snake_case (project_id); accept camelCase too.
                    if (root.TryGetProperty("project_id", out var ps)) projectId = ps.GetString();
                    else if (root.TryGetProperty("projectId", out var pc)) projectId = pc.GetString();
                    if (root.TryGetProperty("location", out var l) && !string.IsNullOrWhiteSpace(l.GetString()))
                        location = l.GetString()!;
                }
                catch (JsonException)
                {
                    throw new BusinessRuleException(
                        "Cấu hình API dịch ngôn ngữ (settings_json) không hợp lệ.", "TRANSLATION_CONFIG_INVALID");
                }
            }
            credentialJson = _credentialResolver.Resolve(config);
        }
        else
        {
            projectId = Environment.GetEnvironmentVariable(NewsTranslationConstants.ProjectIdEnvVar);

            var fromEnv = Environment.GetEnvironmentVariable(NewsTranslationConstants.DefaultSecretRef);
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                var value = fromEnv.Trim();
                credentialJson = value.StartsWith('{')
                    ? value
                    : (System.IO.File.Exists(value) ? System.IO.File.ReadAllText(value) : null);
            }
            if (credentialJson is null)
            {
                var credentialPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
                if (!string.IsNullOrWhiteSpace(credentialPath) && System.IO.File.Exists(credentialPath))
                    credentialJson = System.IO.File.ReadAllText(credentialPath);
            }
        }

        if (string.IsNullOrWhiteSpace(projectId))
            throw new BusinessRuleException(
                "Chưa cấu hình API dịch ngôn ngữ (thiếu projectId). Vui lòng liên hệ quản trị viên.",
                "TRANSLATION_CONFIG_MISSING");
        if (string.IsNullOrWhiteSpace(credentialJson))
            throw new BusinessRuleException(
                "Chưa cấu hình thông tin xác thực cho API dịch ngôn ngữ. Vui lòng liên hệ quản trị viên.",
                "TRANSLATION_CONFIG_MISSING");

        return (projectId!, location, credentialJson!, timeoutSeconds);
    }
}
