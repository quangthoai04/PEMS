using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Security;

/// <summary>
/// Cloudflare Turnstile server-side verification (<c>siteverify</c>).
///
/// Security posture:
///  * The secret key lives only in backend configuration/environment — never in the client.
///  * Production FAILS CLOSED: enabled-but-unconfigured → every verification is rejected.
///  * Expired/replayed tokens are rejected (Cloudflare returns timeout-or-duplicate).
///  * <c>action</c> and <c>hostname</c> from the provider response are validated against
///    the configured expectations.
///  * Development/Testing bypass is EXPLICIT (Turnstile:DevBypassToken must be configured
///    and the presented token must match exactly) and is hard-disabled in Production.
/// </summary>
public sealed class TurnstileHumanVerificationService : IHumanVerificationService
{
    private const string SiteVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TurnstileHumanVerificationService> _logger;

    private readonly bool _enabled;
    private readonly string? _secretKey;
    private readonly string? _expectedAction;
    private readonly HashSet<string> _allowedHostnames;
    private readonly string? _devBypassToken;

    public TurnstileHumanVerificationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<TurnstileHumanVerificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _environment       = environment;
        _logger            = logger;

        _enabled        = bool.TryParse(configuration["Turnstile:Enabled"], out var e) && e;
        _secretKey      = configuration["Turnstile:SecretKey"];
        _expectedAction = configuration["Turnstile:ExpectedAction"];
        _devBypassToken = configuration["Turnstile:DevBypassToken"];
        _allowedHostnames = configuration.GetSection("Turnstile:AllowedHostnames")
            .Get<string[]>()?.Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim().ToLowerInvariant())
            .ToHashSet() ?? new HashSet<string>();
    }

    public async Task<HumanVerificationResult> VerifyAsync(
        string token, string? ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new HumanVerificationResult(false, "missing_token");

        if (!_enabled)
        {
            // Explicit non-production bypass only: the configured bypass token must match
            // exactly. In Production a disabled/unconfigured provider always fails closed.
            if (!_environment.IsProduction()
                && !string.IsNullOrEmpty(_devBypassToken)
                && string.Equals(token, _devBypassToken, StringComparison.Ordinal))
            {
                return new HumanVerificationResult(true, null);
            }

            return new HumanVerificationResult(false, "verification_unavailable");
        }

        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            // Enabled but no secret — misconfiguration. Fail closed (especially Production).
            _logger.LogError("Turnstile is enabled but Turnstile:SecretKey is not configured — failing closed.");
            return new HumanVerificationResult(false, "verification_unavailable");
        }

        try
        {
            var form = new Dictionary<string, string>
            {
                ["secret"]   = _secretKey,
                ["response"] = token
            };
            if (!string.IsNullOrWhiteSpace(ipAddress))
                form["remoteip"] = ipAddress;

            var client   = _httpClientFactory.CreateClient(nameof(TurnstileHumanVerificationService));
            using var response = await client.PostAsync(
                SiteVerifyUrl, new FormUrlEncodedContent(form), cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(
                cancellationToken: cancellationToken);

            if (payload is null || !payload.Success)
            {
                var codes = payload?.ErrorCodes is { Length: > 0 } ec ? string.Join(',', ec) : "unknown";
                _logger.LogInformation("Turnstile verification failed ({Codes}).", codes);
                return new HumanVerificationResult(false, codes);
            }

            if (!string.IsNullOrEmpty(_expectedAction)
                && !string.Equals(payload.Action, _expectedAction, StringComparison.Ordinal))
            {
                _logger.LogWarning("Turnstile action mismatch: expected {Expected}, got {Actual}.",
                    _expectedAction, payload.Action);
                return new HumanVerificationResult(false, "action_mismatch");
            }

            if (_allowedHostnames.Count > 0
                && (payload.Hostname is null
                    || !_allowedHostnames.Contains(payload.Hostname.Trim().ToLowerInvariant())))
            {
                _logger.LogWarning("Turnstile hostname not allowed: {Hostname}.", payload.Hostname);
                return new HumanVerificationResult(false, "hostname_not_allowed");
            }

            return new HumanVerificationResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Turnstile siteverify call failed — failing closed.");
            return new HumanVerificationResult(false, "provider_error");
        }
    }

    private sealed class SiteVerifyResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("action")] public string? Action { get; set; }
        [JsonPropertyName("hostname")] public string? Hostname { get; set; }
        [JsonPropertyName("error-codes")] public string[]? ErrorCodes { get; set; }
    }
}
