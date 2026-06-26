using System.Net.Mime;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using PEMS.Infrastructure.FileStorage.GoogleDrive;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// DEV-ONLY utility to obtain (or refresh) the long-lived Google Drive <c>RefreshToken</c> used
    /// by <see cref="GoogleDriveStorageService"/>. <c>/connect</c> sends the operator through Google's
    /// consent screen; <c>/callback</c> exchanges the returned code for tokens and shows the refresh
    /// token so it can be pasted into <c>appsettings.Development.json</c> by hand. The endpoints are
    /// blocked outside Development, never expose the client secret, and never write any config file.
    /// </summary>
    [ApiController]
    [Route("api/google-drive/oauth")]
    public sealed class GoogleDriveOAuthController : ControllerBase
    {
        private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string DriveScope = "https://www.googleapis.com/auth/drive";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly GoogleDriveOptions _options;
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpClientFactory _httpClientFactory;

        public GoogleDriveOAuthController(
            IOptions<GoogleDriveOptions> options,
            IWebHostEnvironment environment,
            IHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            _environment = environment;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>Redirects to Google's consent screen, requesting offline access to Drive.</summary>
        [HttpGet("connect")]
        public IActionResult Connect()
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.RedirectUri))
                return BadRequest("Thiếu GoogleDrive:ClientId hoặc GoogleDrive:RedirectUri trong cấu hình.");

            var query = new Dictionary<string, string?>
            {
                ["client_id"] = _options.ClientId,
                ["redirect_uri"] = _options.RedirectUri,
                ["response_type"] = "code",
                ["scope"] = DriveScope,
                ["access_type"] = "offline",
                // Force the consent screen so Google reliably returns a refresh_token, even if this
                // Google account has already granted access before.
                ["prompt"] = "consent",
            };

            var authorizationUrl = QueryHelpers.AddQueryString(AuthorizationEndpoint, query);
            return Redirect(authorizationUrl);
        }

        /// <summary>Exchanges the authorization code for tokens and shows the refresh token to copy.</summary>
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(
            [FromQuery] string? code,
            [FromQuery] string? error,
            CancellationToken cancellationToken)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            if (!string.IsNullOrWhiteSpace(error))
                return HtmlPage("Google OAuth bị từ chối", $"<p>Google trả về lỗi: <code>{Encode(error)}</code></p>");

            if (string.IsNullOrWhiteSpace(code))
                return HtmlPage("Thiếu authorization code", "<p>Không nhận được <code>code</code> từ Google.</p>");

            if (string.IsNullOrWhiteSpace(_options.ClientId)
                || string.IsNullOrWhiteSpace(_options.ClientSecret)
                || string.IsNullOrWhiteSpace(_options.RedirectUri))
            {
                return BadRequest("Thiếu cấu hình GoogleDrive OAuth (ClientId/ClientSecret/RedirectUri).");
            }

            var httpClient = _httpClientFactory.CreateClient();
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["redirect_uri"] = _options.RedirectUri,
                ["grant_type"] = "authorization_code",
            });

            using var response = await httpClient.PostAsync(TokenEndpoint, form, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Surface Google's error verbatim (it does not contain our client secret).
                return HtmlPage(
                    "Đổi token thất bại",
                    $"<p>Google token endpoint trả về <code>{(int)response.StatusCode}</code>:</p><pre>{Encode(body)}</pre>");
            }

            GoogleOAuthTokenResponse? token;
            try
            {
                token = JsonSerializer.Deserialize<GoogleOAuthTokenResponse>(body, JsonOptions);
            }
            catch
            {
                token = null;
            }

            if (token is null || string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                return HtmlPage(
                    "Không nhận được refresh_token",
                    "<p>Google không trả về <code>refresh_token</code>. Hãy thử:</p>"
                    + "<ol>"
                    + "<li>Đảm bảo connect dùng <code>prompt=consent</code> và <code>access_type=offline</code>.</li>"
                    + "<li>Vào Google Account → Security → Third-party access → gỡ quyền app PEMS.</li>"
                    + "<li>Chạy lại <code>/api/google-drive/oauth/connect</code>.</li>"
                    + "</ol>");
            }

            // Only the refresh token is shown — never the client secret.
            var html =
                "<p>Google Drive đã kết nối thành công.</p>"
                + "<p>Copy <strong>RefreshToken</strong> dưới đây vào "
                + "<code>backend/PEMS.Api/appsettings.Development.json</code> tại "
                + "<code>GoogleDrive:RefreshToken</code>, rồi <strong>restart backend</strong> và test upload avatar.</p>"
                + $"<p><strong>GoogleDrive:RefreshToken</strong></p><pre>{Encode(token.RefreshToken)}</pre>"
                + "<p style=\"color:#a00\">Lưu ý: KHÔNG commit file appsettings.Development.json chứa token này lên Git.</p>";

            return HtmlPage("Google Drive connected", html);
        }

        private static string Encode(string? value) => HtmlEncoder.Default.Encode(value ?? string.Empty);

        private ContentResult HtmlPage(string title, string bodyHtml)
        {
            var page =
                "<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\">"
                + $"<title>{Encode(title)}</title>"
                + "<style>body{font-family:system-ui,Segoe UI,Arial,sans-serif;max-width:760px;margin:40px auto;padding:0 16px;line-height:1.5;color:#222}"
                + "pre{background:#f5f5f5;border:1px solid #ddd;border-radius:8px;padding:12px;white-space:pre-wrap;word-break:break-all}"
                + "code{background:#f5f5f5;padding:1px 4px;border-radius:4px}h1{font-size:1.3rem}</style></head><body>"
                + $"<h1>{Encode(title)}</h1>{bodyHtml}</body></html>";

            return new ContentResult { Content = page, ContentType = MediaTypeNames.Text.Html, StatusCode = StatusCodes.Status200OK };
        }

        private sealed class GoogleOAuthTokenResponse
        {
            [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
            [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
            [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
            [JsonPropertyName("scope")] public string? Scope { get; set; }
            [JsonPropertyName("token_type")] public string? TokenType { get; set; }
        }
    }
}
