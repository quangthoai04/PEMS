using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.BusinessCardOcr.Services;

namespace PEMS.Infrastructure.Ocr;

/// <summary>
/// Google Document AI OCR processor client (REST v1). Called exclusively from the
/// backend; the frontend never reaches this endpoint. Only sanitized metadata may
/// be logged by callers — this class never logs payloads or tokens.
/// </summary>
public sealed class GoogleDocumentAiBusinessCardOcrProvider : IBusinessCardOcrProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleServiceAccountTokenProvider _tokenProvider;
    private readonly IBusinessCardTextParser _parser;

    public GoogleDocumentAiBusinessCardOcrProvider(
        IHttpClientFactory httpClientFactory,
        GoogleServiceAccountTokenProvider tokenProvider,
        IBusinessCardTextParser parser)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _parser = parser;
    }

    public async Task<BusinessCardOcrProviderResult> ExtractAsync(
        BusinessCardOcrProviderInput input,
        OcrProviderRuntimeConfig config,
        CancellationToken cancellationToken)
    {
        var settings = config.Settings;
        var url = $"https://{settings.Endpoint}/v1/projects/{settings.ProjectId}/locations/{settings.Location}"
                  + $"/processors/{settings.ProcessorId}:process";

        var accessToken = await _tokenProvider.GetAccessTokenAsync(config.CredentialJson, cancellationToken);

        var payload = JsonSerializer.Serialize(new
        {
            rawDocument = new
            {
                content = Convert.ToBase64String(input.FileBytes),
                mimeType = input.MimeType,
            },
            skipHumanReview = true,
        });

        var http = _httpClientFactory.CreateClient(nameof(GoogleDocumentAiBusinessCardOcrProvider));
        http.Timeout = TimeSpan.FromSeconds(Math.Clamp(config.TimeoutSeconds, 5, 120));

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Google Document AI trả về lỗi HTTP {(int)response.StatusCode}: {ExtractGoogleError(body)}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var requestId = root.TryGetProperty("name", out var name) ? name.GetString() : null;

        var text = string.Empty;
        decimal confidence = 0m;
        if (root.TryGetProperty("document", out var document))
        {
            if (document.TryGetProperty("text", out var textEl))
                text = textEl.GetString() ?? string.Empty;
            confidence = ComputeAveragePageConfidence(document);
        }

        var fields = _parser.Parse(text);

        return new BusinessCardOcrProviderResult
        {
            ProviderName = "GOOGLE_DOCUMENT_AI",
            ProviderRequestId = requestId,
            RawText = text,
            ConfidenceScore = confidence,
            FullName = fields.FullName,
            Email = fields.Email,
            Phone = fields.Phone,
            JobTitle = fields.JobTitle,
            DepartmentName = fields.DepartmentName,
            Organization = fields.Organization,
            WebsiteUrl = fields.WebsiteUrl,
            Address = fields.Address,
        };
    }

    public async Task<BusinessCardOcrConnectionTestResult> TestConnectionAsync(
        OcrProviderRuntimeConfig config,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = config.Settings;
            if (string.IsNullOrWhiteSpace(settings.ProjectId)
                || string.IsNullOrWhiteSpace(settings.ProcessorId)
                || string.IsNullOrWhiteSpace(settings.Location))
                return new BusinessCardOcrConnectionTestResult
                {
                    Success = false,
                    Message = "Thiếu projectId/location/processorId trong cấu hình.",
                    ErrorCode = "OCR_SETTINGS_INCOMPLETE",
                };

            var accessToken = await _tokenProvider.GetAccessTokenAsync(config.CredentialJson, cancellationToken);

            var url = $"https://{settings.Endpoint}/v1/projects/{settings.ProjectId}/locations/{settings.Location}"
                      + $"/processors/{settings.ProcessorId}";
            var http = _httpClientFactory.CreateClient(nameof(GoogleDocumentAiBusinessCardOcrProvider));
            http.Timeout = TimeSpan.FromSeconds(Math.Clamp(config.TimeoutSeconds, 5, 120));

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return new BusinessCardOcrConnectionTestResult
                {
                    Success = false,
                    Message = $"GetProcessor thất bại (HTTP {(int)response.StatusCode}): {ExtractGoogleError(body)}",
                    ErrorCode = $"HTTP_{(int)response.StatusCode}",
                };

            using var doc = JsonDocument.Parse(body);
            var state = doc.RootElement.TryGetProperty("state", out var s) ? s.GetString() : null;
            var displayName = doc.RootElement.TryGetProperty("displayName", out var d) ? d.GetString() : null;
            return new BusinessCardOcrConnectionTestResult
            {
                Success = true,
                Message = $"Kết nối thành công. Processor: {displayName ?? settings.ProcessorId} ({state ?? "UNKNOWN"}).",
            };
        }
        catch (Exception ex)
        {
            return new BusinessCardOcrConnectionTestResult
            {
                Success = false,
                Message = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message,
                ErrorCode = "OCR_TEST_EXCEPTION",
            };
        }
    }

    /// <summary>Average page-level confidence (0–100). Document AI gives 0–1 per page.</summary>
    private static decimal ComputeAveragePageConfidence(JsonElement document)
    {
        if (!document.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
            return 0m;

        decimal sum = 0m;
        var count = 0;
        foreach (var page in pages.EnumerateArray())
        {
            // Prefer layout confidence; fall back to token average.
            if (page.TryGetProperty("layout", out var layout)
                && layout.TryGetProperty("confidence", out var c)
                && c.TryGetDecimal(out var value))
            {
                sum += value;
                count++;
                continue;
            }
            if (page.TryGetProperty("tokens", out var tokens) && tokens.ValueKind == JsonValueKind.Array)
            {
                decimal tokenSum = 0m;
                var tokenCount = 0;
                foreach (var token in tokens.EnumerateArray())
                {
                    if (token.TryGetProperty("layout", out var tl)
                        && tl.TryGetProperty("confidence", out var tc)
                        && tc.TryGetDecimal(out var tv))
                    {
                        tokenSum += tv;
                        tokenCount++;
                    }
                }
                if (tokenCount > 0)
                {
                    sum += tokenSum / tokenCount;
                    count++;
                }
            }
        }

        return count == 0 ? 0m : Math.Round(sum / count * 100m, 2);
    }

    /// <summary>Extracts google error.message only — never echoes the raw body into logs.</summary>
    private static string ExtractGoogleError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message))
            {
                var text = message.GetString() ?? "unknown";
                return text.Length > 300 ? text[..300] : text;
            }
        }
        catch (JsonException) { }
        return "unknown";
    }
}
