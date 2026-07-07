namespace PEMS.Application.Galleries.Tts;

/// <summary>
/// Deterministic fingerprint of "what a narration audio was generated from": the normalized
/// description PLUS every voice/audio setting. If the Staff Leader edits the description or the
/// configured voice/audio settings change, the hash changes and older READY audio silently stops
/// matching — the public player then lazy-generates a fresh one instead of playing stale narration.
/// </summary>
public interface IGalleryTtsHashService
{
    /// <summary>
    /// Trims and collapses internal whitespace runs to single spaces. Keeps Vietnamese diacritics and
    /// letter case untouched (the narration is case/diacritic sensitive).
    /// </summary>
    string NormalizeDescription(string? description);

    /// <summary>SHA-256 (lowercase hex, 64 chars) over the canonical text+settings string.</summary>
    string ComputeHash(
        string normalizedDescription,
        string voiceCode,
        string audioType,
        int? bitrate,
        decimal speedRate,
        decimal pitchRate,
        int volume);
}
