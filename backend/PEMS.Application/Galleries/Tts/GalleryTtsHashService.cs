using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PEMS.Application.Galleries.Tts;

/// <inheritdoc cref="IGalleryTtsHashService"/>
public sealed class GalleryTtsHashService : IGalleryTtsHashService
{
    public string NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;
        return Regex.Replace(description.Trim(), @"\s+", " ");
    }

    public string ComputeHash(
        string normalizedDescription,
        string voiceCode,
        string audioType,
        int? bitrate,
        decimal speedRate,
        decimal pitchRate,
        int volume)
    {
        // Canonical string — one setting per line, invariant formatting ("0.0" mirrors the DECIMAL(3,1)
        // columns), bitrate NULL → 0 (mirrors the DB running_key's IFNULL). Any change here invalidates
        // every stored hash, so only extend it, never reorder.
        var canonical = new StringBuilder()
            .Append("text=").Append(normalizedDescription).Append('\n')
            .Append("voice_code=").Append(voiceCode).Append('\n')
            .Append("audio_type=").Append(audioType).Append('\n')
            .Append("bitrate=").Append((bitrate ?? 0).ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("speed_rate=").Append(speedRate.ToString("0.0", CultureInfo.InvariantCulture)).Append('\n')
            .Append("pitch_rate=").Append(pitchRate.ToString("0.0", CultureInfo.InvariantCulture)).Append('\n')
            .Append("volume=").Append(volume.ToString(CultureInfo.InvariantCulture))
            .ToString();

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
