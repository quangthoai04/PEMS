using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;

namespace PEMS.Infrastructure.FileStorage.GoogleDrive;

/// <summary>
/// The <c>state</c> parameter of the Google Drive reconnect flow: an AES-GCM sealed payload naming the
/// ADMIN who started it and the moment it stops counting.
///
/// <para>
/// AES-GCM is what makes this work as a gate rather than a hint. Its authentication tag fails closed on any
/// edit, so a caller cannot promote themselves by changing the user id, and cannot extend the window by
/// changing the deadline — the two things a plain (or merely encoded) state would let anyone do to an
/// endpoint that must stay anonymous because Google redirects a browser to it.
/// </para>
/// <para>
/// The ciphertext is base64url-encoded on top of the protector's base64, so the value survives a query
/// string intact. Standard base64 carries <c>+</c>, <c>/</c> and <c>=</c>, and a <c>+</c> that any hop
/// decodes as a space returns a state that no longer authenticates — a failure that would look exactly like
/// tampering and would be reproducible only for some tokens.
/// </para>
/// </summary>
public sealed class GoogleDriveOAuthStateService : IGoogleDriveOAuthStateService
{
    /// <summary>
    /// How long an admin has between pressing reconnect and finishing Google's consent screen. Long enough
    /// to sign in and pick an account, short enough that a state left in browser history stops mattering.
    /// </summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ISecretProtector _secretProtector;
    private readonly IDateTimeService _clock;

    public GoogleDriveOAuthStateService(ISecretProtector secretProtector, IDateTimeService clock)
    {
        _secretProtector = secretProtector;
        _clock = clock;
    }

    public string Create(ulong adminUserId)
    {
        var payload = new StatePayload
        {
            AdminUserId = adminUserId,
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            ExpiresAtUtc = _clock.UtcNow.Add(Lifetime),
        };

        var sealedText = _secretProtector.Protect(JsonSerializer.Serialize(payload, Json));
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(sealedText));
    }

    public GoogleDriveOAuthStateValidation Validate(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return Invalid(GoogleDriveOAuthRedirectReasons.InvalidState);

        StatePayload? payload;
        try
        {
            var sealedText = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(state));
            payload = JsonSerializer.Deserialize<StatePayload>(_secretProtector.Unprotect(sealedText), Json);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or JsonException
                                       or InvalidOperationException or CryptographicException
                                       or DecoderFallbackException)
        {
            // Every way a state can be wrong — truncated, re-encoded, forged, or sealed with another
            // deployment's key — answers identically. Telling them apart would only help a forger.
            return Invalid(GoogleDriveOAuthRedirectReasons.InvalidState);
        }

        if (payload is null || payload.AdminUserId == 0 || string.IsNullOrWhiteSpace(payload.Nonce))
            return Invalid(GoogleDriveOAuthRedirectReasons.InvalidState);

        if (payload.ExpiresAtUtc <= _clock.UtcNow)
            return Invalid(GoogleDriveOAuthRedirectReasons.StateExpired);

        return new GoogleDriveOAuthStateValidation(
            true,
            new GoogleDriveOAuthState(payload.AdminUserId, payload.Nonce, payload.ExpiresAtUtc),
            null);
    }

    private static GoogleDriveOAuthStateValidation Invalid(string reason) => new(false, null, reason);

    private sealed class StatePayload
    {
        [JsonPropertyName("adminUserId")]
        public ulong AdminUserId { get; set; }

        [JsonPropertyName("nonce")]
        public string Nonce { get; set; } = string.Empty;

        [JsonPropertyName("expiresAtUtc")]
        public DateTime ExpiresAtUtc { get; set; }
    }
}
