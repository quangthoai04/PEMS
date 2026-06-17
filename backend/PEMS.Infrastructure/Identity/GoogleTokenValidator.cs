using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Identity;

/// <summary>
/// Validates Google Sign-In ID tokens using Google's published JWKS. No third
/// party package required — signature/issuer/audience/expiry are checked with the
/// standard <see cref="JwtSecurityTokenHandler"/>.
/// </summary>
public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private const string CertsUrl = "https://www.googleapis.com/oauth2/v3/certs";
    private static readonly string[] ValidIssuers = { "accounts.google.com", "https://accounts.google.com" };

    // Simple process-wide cache of Google's signing keys.
    private static IList<SecurityKey>? _cachedKeys;
    private static DateTime _cacheExpiresUtc = DateTime.MinValue;
    private static readonly SemaphoreSlim CacheLock = new(1, 1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleTokenValidator> _logger;
    private readonly string[] _allowedAudiences;
    private readonly string[] _allowedDomains;
    private readonly bool _requireEmailVerified;

    public GoogleTokenValidator(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GoogleTokenValidator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var section = configuration.GetSection("GoogleAuth");
        _allowedAudiences = (section["ClientId"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _allowedDomains = (section["AllowedDomains"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _requireEmailVerified = !bool.TryParse(section["RequireEmailVerified"], out var r) || r;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        if (_allowedAudiences.Length == 0)
        {
            _logger.LogWarning("GoogleAuth:ClientId is not configured; Google sign-in is disabled.");
            return null;
        }

        try
        {
            var signingKeys = await GetSigningKeysAsync(cancellationToken);

            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = ValidIssuers,
                ValidateAudience = true,
                ValidAudiences = _allowedAudiences,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(idToken, parameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwt)
                return null;

            var subject = jwt.Subject;
            var email = GetClaim(jwt, "email");
            var emailVerified = string.Equals(GetClaim(jwt, "email_verified"), "true", StringComparison.OrdinalIgnoreCase);
            var name = GetClaim(jwt, "name");
            var picture = GetClaim(jwt, "picture");

            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email))
                return null;

            if (_requireEmailVerified && !emailVerified)
            {
                _logger.LogInformation("Rejected Google sign-in: email not verified.");
                return null;
            }

            if (_allowedDomains.Length > 0)
            {
                var domain = email.Contains('@') ? email[(email.IndexOf('@') + 1)..] : string.Empty;
                if (!_allowedDomains.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogInformation("Rejected Google sign-in: domain {Domain} not allowed.", domain);
                    return null;
                }
            }

            return new GoogleUserInfo(subject, email, emailVerified, name, picture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google ID token validation failed.");
            return null;
        }
    }

    private static string? GetClaim(JwtSecurityToken jwt, string type)
        => jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;

    private async Task<IList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken)
    {
        if (_cachedKeys is not null && DateTime.UtcNow < _cacheExpiresUtc)
            return _cachedKeys;

        await CacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedKeys is not null && DateTime.UtcNow < _cacheExpiresUtc)
                return _cachedKeys;

            var client = _httpClientFactory.CreateClient();
            var json = await client.GetStringAsync(CertsUrl, cancellationToken);
            var keySet = new JsonWebKeySet(json);
            var keys = keySet.GetSigningKeys();

            _cachedKeys = keys;
            _cacheExpiresUtc = DateTime.UtcNow.AddHours(6);
            return keys;
        }
        finally
        {
            CacheLock.Release();
        }
    }
}
