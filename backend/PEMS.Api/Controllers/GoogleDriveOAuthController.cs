using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Storage;

namespace PEMS.Api.Controllers;

/// <summary>
/// DEV-ONLY utility to (re)obtain a Google Drive <c>RefreshToken</c> for the shared team account,
/// so avatar/profile uploads can run end-to-end without fetching the token by hand.
///
/// Flow: <c>GET connect</c> redirects to Google's consent screen → user allows → Google redirects to
/// <c>GET callback?code=…</c> → we exchange the code for tokens and render an HTML page showing ONLY
/// the <c>refresh_token</c> for the operator to paste into <c>appsettings.Development.json</c>.
///
/// Hard rules (see PEMS_GOOGLE_DRIVE_OAUTH_REFRESH_TOKEN_FLOW.md): Development-only (404 otherwise),
/// never expose the ClientSecret, never auto-write the config file, never commit the token.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/google-drive/oauth")]
public sealed class GoogleDriveOAuthController : ControllerBase
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string DriveScope = "https://www.googleapis.com/auth/drive";

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

    /// <summary>Builds the Google consent URL and redirects to it.</summary>
    [HttpGet("connect")]
    public IActionResult Connect()
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.RedirectUri))
            return BadRequest("Missing GoogleDrive ClientId or RedirectUri in configuration.");

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = DriveScope,
            ["access_type"] = "offline", // ask Google for a refresh_token
            ["prompt"] = "consent",      // force the consent screen so a refresh_token is reliably returned
        };

        var authorizationUrl = QueryHelpers.AddQueryString(AuthorizationEndpoint, query);
        return Redirect(authorizationUrl);
    }

    /// <summary>Exchanges the authorization code for tokens and renders the refresh_token for copying.</summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        if (!string.IsNullOrWhiteSpace(error))
            return ContentHtml(ErrorPage($"Google OAuth error: {WebUtility.HtmlEncode(error)}"));

        if (string.IsNullOrWhiteSpace(code))
            return ContentHtml(ErrorPage("Missing authorization code."));

        if (string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret)
            || string.IsNullOrWhiteSpace(_options.RedirectUri))
            return ContentHtml(ErrorPage("Missing GoogleDrive OAuth configuration (ClientId / ClientSecret / RedirectUri)."));

        var httpClient = _httpClientFactory.CreateClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId!,
            ["client_secret"] = _options.ClientSecret!,
            ["redirect_uri"] = _options.RedirectUri!,
            ["grant_type"] = "authorization_code",
        });

        var response = await httpClient.PostAsync(TokenEndpoint, form, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = TryGetJsonString(body, "error_description")
                ?? TryGetJsonString(body, "error")
                ?? $"HTTP {(int)response.StatusCode}";
            return ContentHtml(ErrorPage($"Token exchange failed: {WebUtility.HtmlEncode(detail)}"));
        }

        var refreshToken = TryGetJsonString(body, "refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken))
            return ContentHtml(NoRefreshTokenPage());

        return ContentHtml(SuccessPage(refreshToken!));
    }

    private ContentResult ContentHtml(string html) => Content(html, "text/html");

    private static string? TryGetJsonString(string json, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string Layout(string title, string bodyHtml) => $@"<!doctype html>
<html lang=""vi"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>{WebUtility.HtmlEncode(title)}</title>
<style>
  body {{ font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif; background:#f3f4f6; margin:0; padding:32px; color:#111827; }}
  .card {{ max-width:760px; margin:0 auto; background:#fff; border:1px solid #e5e7eb; border-radius:16px; padding:28px 32px; box-shadow:0 1px 3px rgba(0,0,0,.06); }}
  h1 {{ font-size:20px; margin:0 0 16px; color:#004c91; }}
  code, pre {{ font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }}
  .token {{ display:block; word-break:break-all; background:#0b1f33; color:#7CFC98; padding:14px 16px; border-radius:10px; font-size:14px; margin:12px 0; }}
  ol {{ line-height:1.7; }} .muted {{ color:#6b7280; font-size:13px; }}
  .err {{ color:#b91c1c; }}
</style>
</head>
<body><div class=""card"">{bodyHtml}</div></body>
</html>";

    private static string SuccessPage(string refreshToken) => Layout(
        "Google Drive connected",
        $@"<h1>✅ Google Drive đã kết nối thành công</h1>
<p>Copy <strong>RefreshToken</strong> dưới đây vào <code>backend/PEMS.Api/appsettings.Development.json</code>:</p>
<p class=""muted"">GoogleDrive:RefreshToken</p>
<code class=""token"">{WebUtility.HtmlEncode(refreshToken)}</code>
<ol>
  <li>Mở <code>appsettings.Development.json</code>.</li>
  <li>Dán giá trị trên vào <code>GoogleDrive:RefreshToken</code>.</li>
  <li>Lưu file rồi <strong>khởi động lại backend</strong>.</li>
  <li>Thử upload avatar trong màn Hồ sơ.</li>
</ol>
<p class=""muted"">Lưu ý: KHÔNG commit file này lên Git. Trang này không hiển thị ClientSecret.</p>");

    private static string NoRefreshTokenPage() => Layout(
        "No refresh_token returned",
        @"<h1 class=""err"">⚠️ Google không trả về refresh_token</h1>
<p>Hãy thử các bước sau rồi chạy lại:</p>
<ol>
  <li>Đảm bảo URL connect có <code>prompt=consent</code> và <code>access_type=offline</code>.</li>
  <li>Vào Google Account → Security → Third-party access → gỡ quyền app PEMS.</li>
  <li>Mở lại <code>/api/google-drive/oauth/connect</code> và bấm Allow.</li>
</ol>");

    private static string ErrorPage(string message) => Layout(
        "Google Drive OAuth error",
        $@"<h1 class=""err"">❌ Lỗi kết nối Google Drive</h1><p class=""err"">{message}</p>
<p><a href=""/api/google-drive/oauth/connect"">Thử kết nối lại</a></p>");
}
