using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Services;
using PEMS.Infrastructure.Ocr;
using SixLabors.ImageSharp;

namespace PEMS.Infrastructure.FaceDetection;

/// <summary>
/// Google Cloud Vision FACE_DETECTION client (REST v1). Called exclusively from the backend; the
/// frontend never reaches this endpoint. Only sanitized metadata may be logged by callers — this
/// class never logs image bytes, the raw Vision response, landmarks or emotion/likelihood fields.
/// </summary>
public sealed class GoogleVisionFaceDetectionProvider : IFaceDetectionProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleServiceAccountTokenProvider _tokenProvider;

    public GoogleVisionFaceDetectionProvider(
        IHttpClientFactory httpClientFactory, GoogleServiceAccountTokenProvider tokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
    }

    public async Task<FaceDetectionProviderResult> DetectFacesAsync(
        FaceDetectionProviderInput input, FaceDetectionRuntimeConfig config, CancellationToken cancellationToken)
    {
        var (imageWidth, imageHeight) = await ReadDimensionsAsync(input.FileBytes, cancellationToken);

        var settings = config.Settings;
        var url = $"https://{settings.Endpoint}/v1/images:annotate";
        var accessToken = await _tokenProvider.GetAccessTokenAsync(config.CredentialJson, cancellationToken);

        var payload = JsonSerializer.Serialize(new
        {
            requests = new[]
            {
                new
                {
                    image = new { content = Convert.ToBase64String(input.FileBytes) },
                    features = new[] { new { type = "FACE_DETECTION", maxResults = settings.MaxFacesPerImage } },
                },
            },
        });

        var http = _httpClientFactory.CreateClient(nameof(GoogleVisionFaceDetectionProvider));
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
                $"Google Cloud Vision trả về lỗi HTTP {(int)response.StatusCode}: {ExtractGoogleError(body)}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("responses", out var responses)
            || responses.ValueKind != JsonValueKind.Array || responses.GetArrayLength() == 0)
            throw new InvalidOperationException("Google Cloud Vision trả về phản hồi rỗng.");

        var imageResponse = responses[0];
        if (imageResponse.TryGetProperty("error", out var errorEl)
            && errorEl.TryGetProperty("message", out var errorMsgEl))
        {
            var msg = errorMsgEl.GetString() ?? "unknown";
            throw new InvalidOperationException(
                $"Google Cloud Vision báo lỗi: {(msg.Length > 300 ? msg[..300] : msg)}");
        }

        var faces = new List<DetectedFaceDto>();
        if (imageResponse.TryGetProperty("faceAnnotations", out var annotations)
            && annotations.ValueKind == JsonValueKind.Array)
        {
            foreach (var face in annotations.EnumerateArray())
            {
                var box = ExtractNormalizedBox(face, imageWidth, imageHeight);
                if (box is null) continue;

                var confidence = face.TryGetProperty("detectionConfidence", out var confEl)
                    && confEl.TryGetDecimal(out var confValue)
                    ? confValue
                    : 0m;

                faces.Add(new DetectedFaceDto
                {
                    BoundingBoxX = box.Value.X,
                    BoundingBoxY = box.Value.Y,
                    BoundingBoxWidth = box.Value.Width,
                    BoundingBoxHeight = box.Value.Height,
                    DetectionConfidence = Math.Clamp(Math.Round(confidence, 5), 0m, 1m),
                });
            }
        }

        return new FaceDetectionProviderResult
        {
            ProviderName = FaceDetectionConstants.ProviderName,
            ProviderRequestId = null, // Vision v1 images:annotate does not return a request id.
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
            Faces = faces,
        };
    }

    public async Task<FaceDetectionConnectionTestResult> TestConnectionAsync(
        FaceDetectionRuntimeConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var settings = config.Settings;
            if (string.IsNullOrWhiteSpace(settings.Endpoint))
                return new FaceDetectionConnectionTestResult
                {
                    Success = false,
                    Message = "Thiếu endpoint trong cấu hình.",
                    ErrorCode = "FACE_DETECTION_SETTINGS_INCOMPLETE",
                };

            var accessToken = await _tokenProvider.GetAccessTokenAsync(config.CredentialJson, cancellationToken);

            // Minimal 1x1 white pixel — verifies auth + endpoint + project permission, not a real photo.
            var probeImage = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

            var url = $"https://{settings.Endpoint}/v1/images:annotate";
            var payload = JsonSerializer.Serialize(new
            {
                requests = new[]
                {
                    new
                    {
                        image = new { content = Convert.ToBase64String(probeImage) },
                        features = new[] { new { type = "FACE_DETECTION", maxResults = 1 } },
                    },
                },
            });

            var http = _httpClientFactory.CreateClient(nameof(GoogleVisionFaceDetectionProvider));
            http.Timeout = TimeSpan.FromSeconds(Math.Clamp(config.TimeoutSeconds, 5, 120));

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return new FaceDetectionConnectionTestResult
                {
                    Success = false,
                    Message = $"Gọi Vision API thất bại (HTTP {(int)response.StatusCode}): {ExtractGoogleError(body)}",
                    ErrorCode = $"HTTP_{(int)response.StatusCode}",
                };

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("responses", out var responses)
                && responses.ValueKind == JsonValueKind.Array && responses.GetArrayLength() > 0
                && responses[0].TryGetProperty("error", out var errorEl)
                && errorEl.TryGetProperty("message", out var errorMsgEl))
            {
                var msg = errorMsgEl.GetString() ?? "unknown";
                return new FaceDetectionConnectionTestResult
                {
                    Success = false,
                    Message = $"Google Cloud Vision báo lỗi: {(msg.Length > 300 ? msg[..300] : msg)}",
                    ErrorCode = "FACE_DETECTION_PROVIDER_ERROR",
                };
            }

            return new FaceDetectionConnectionTestResult
            {
                Success = true,
                Message = "Kết nối thành công tới Google Cloud Vision (FACE_DETECTION).",
            };
        }
        catch (Exception ex)
        {
            return new FaceDetectionConnectionTestResult
            {
                Success = false,
                Message = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message,
                ErrorCode = "FACE_DETECTION_TEST_EXCEPTION",
            };
        }
    }

    private static async Task<(uint Width, uint Height)> ReadDimensionsAsync(
        byte[] fileBytes, CancellationToken cancellationToken)
    {
        await using var stream = new System.IO.MemoryStream(fileBytes, writable: false);
        var info = await Image.IdentifyAsync(stream, cancellationToken)
            ?? throw new InvalidOperationException("Không đọc được kích thước ảnh.");
        return ((uint)info.Width, (uint)info.Height);
    }

    /// <summary>
    /// Converts a Vision boundingPoly (pixel vertices, possibly omitting a 0 coordinate) into a
    /// clamped, normalized axis-aligned box. Prefers fdBoundingPoly (tighter, skin-only box) and
    /// falls back to boundingPoly. Returns null when the image has no usable dimensions.
    /// </summary>
    private static (decimal X, decimal Y, decimal Width, decimal Height)? ExtractNormalizedBox(
        JsonElement face, uint imageWidth, uint imageHeight)
    {
        if (imageWidth == 0 || imageHeight == 0) return null;

        JsonElement poly;
        if (face.TryGetProperty("fdBoundingPoly", out var fd) && fd.TryGetProperty("vertices", out _))
            poly = fd;
        else if (face.TryGetProperty("boundingPoly", out var bp) && bp.TryGetProperty("vertices", out _))
            poly = bp;
        else
            return null;

        if (!poly.TryGetProperty("vertices", out var vertices) || vertices.ValueKind != JsonValueKind.Array)
            return null;

        var xs = new List<int>();
        var ys = new List<int>();
        foreach (var v in vertices.EnumerateArray())
        {
            xs.Add(v.TryGetProperty("x", out var xEl) && xEl.TryGetInt32(out var xv) ? xv : 0);
            ys.Add(v.TryGetProperty("y", out var yEl) && yEl.TryGetInt32(out var yv) ? yv : 0);
        }
        if (xs.Count == 0) return null;

        var xMin = Math.Clamp(xs.Min(), 0, (int)imageWidth - 1);
        var xMax = Math.Clamp(xs.Max(), xMin + 1, (int)imageWidth);
        var yMin = Math.Clamp(ys.Min(), 0, (int)imageHeight - 1);
        var yMax = Math.Clamp(ys.Max(), yMin + 1, (int)imageHeight);

        var nx = Math.Round(xMin / (decimal)imageWidth, 8);
        var ny = Math.Round(yMin / (decimal)imageHeight, 8);
        var nw = Math.Round((xMax - xMin) / (decimal)imageWidth, 8);
        var nh = Math.Round((yMax - yMin) / (decimal)imageHeight, 8);

        // Guard against rounding pushing the box past the 0..1 edge (CHECK constraints require
        // x + width <= 1 and y + height <= 1).
        if (nx + nw > 1m) nw = 1m - nx;
        if (ny + nh > 1m) nh = 1m - ny;
        if (nw <= 0 || nh <= 0) return null;

        return (nx, ny, nw, nh);
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
