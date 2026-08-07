using System.Text.Json;
using System.Text.Json.Serialization;

namespace PEMS.Application.Common.Storage;

/// <summary>
/// What <c>api_configurations.credentials_json_encrypted</c> holds for the Google Drive row, before
/// <see cref="PEMS.Application.Common.Interfaces.ISecretProtector"/> encrypts it and after it decrypts it.
///
/// <para>
/// A JSON envelope rather than the bare token, so the column keeps the shape every other provider uses
/// (a credential document) and so a second field can be added later without a migration or a format sniff.
/// It never holds the client secret: that stays in configuration, where the deployment owns it, and a
/// credential this module rewrites on every reconnect must not be able to lose it.
/// </para>
/// </summary>
public sealed class GoogleDriveCredentialEnvelope
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>
    /// Reads an envelope back, or <c>null</c> when the plaintext is not one. Returning null rather than
    /// throwing keeps "decrypted into something unexpected" in the same bucket as "did not decrypt" for the
    /// caller, which is the honest reading: both mean the stored credential is unusable.
    /// </summary>
    public static GoogleDriveCredentialEnvelope? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<GoogleDriveCredentialEnvelope>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
