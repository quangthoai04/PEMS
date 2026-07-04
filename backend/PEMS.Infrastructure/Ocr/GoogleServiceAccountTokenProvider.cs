using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Infrastructure.Ocr;

/// <summary>
/// OAuth2 access tokens for a Google service account via the JWT-bearer grant —
/// no Google SDK dependency. The private key never leaves this process and is
/// never logged. Tokens are cached per client_email until ~1 minute before expiry.
/// </summary>
public sealed class GoogleServiceAccountTokenProvider
{
    private const string TokenUri = "https://oauth2.googleapis.com/token";
    private const string Scope = "https://www.googleapis.com/auth/cloud-platform";

    private static readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> Cache = new();

    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleServiceAccountTokenProvider(IHttpClientFactory httpClientFactory)
        => _httpClientFactory = httpClientFactory;

    public async Task<string> GetAccessTokenAsync(string serviceAccountJson, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(serviceAccountJson);
        var root = doc.RootElement;
        var clientEmail = root.GetProperty("client_email").GetString()
            ?? throw new InvalidOperationException("Service account JSON thiếu client_email.");
        var privateKeyPem = root.GetProperty("private_key").GetString()
            ?? throw new InvalidOperationException("Service account JSON thiếu private_key.");
        var tokenUri = root.TryGetProperty("token_uri", out var t) ? t.GetString() ?? TokenUri : TokenUri;

        if (Cache.TryGetValue(clientEmail, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return cached.Token;

        var assertion = BuildSignedJwt(clientEmail, privateKeyPem, tokenUri);

        var http = _httpClientFactory.CreateClient(nameof(GoogleServiceAccountTokenProvider));
        using var response = await http.PostAsync(tokenUri, new FormUrlEncodedContent(new[]
        {
            new System.Collections.Generic.KeyValuePair<string, string>(
                "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
            new System.Collections.Generic.KeyValuePair<string, string>("assertion", assertion),
        }), cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Google OAuth token request failed ({(int)response.StatusCode}).");

        using var tokenDoc = JsonDocument.Parse(body);
        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Google OAuth response thiếu access_token.");
        var expiresIn = tokenDoc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;

        Cache[clientEmail] = (accessToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        return accessToken;
    }

    private static string BuildSignedJwt(string clientEmail, string privateKeyPem, string audience)
    {
        var now = DateTimeOffset.UtcNow;
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = clientEmail,
            scope = Scope,
            aud = audience,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddMinutes(30).ToUnixTimeSeconds(),
        }));

        var signingInput = $"{header}.{payload}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signature = rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
