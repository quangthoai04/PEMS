using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Common.Files;

/// <summary>
/// Builds keys of the form <c>{prefix}/{yyyy}/{MM}/{dd}/{uploadedBy}_{yyyyMMddHHmmss}_{guid}{.ext}</c>,
/// e.g. <c>avatars/2026/06/27/15_20260627153022_0b2a6c9f.jpg</c>. Uses the Vietnam wall clock for the
/// date segments so keys line up with the rest of the system's timestamps.
/// </summary>
public sealed class FileObjectKeyBuilder : IFileObjectKeyBuilder
{
    private readonly IDateTimeService _clock;

    public FileObjectKeyBuilder(IDateTimeService clock) => _clock = clock;

    public string Build(FilePurpose purpose, long uploadedBy, string originalFileName)
    {
        var prefix = purpose.ToObjectKeyPrefix();
        var now = _clock.VietnamNow;
        var ext = SanitizeExtension(originalFileName);
        var guid = Guid.NewGuid().ToString("N")[..8];

        return $"{prefix}/{now:yyyy}/{now:MM}/{now:dd}/{uploadedBy}_{now:yyyyMMddHHmmss}_{guid}{ext}";
    }

    /// <summary>
    /// Lowercased extension including the dot, limited to a safe charset and length; empty when the
    /// filename has no usable extension. Guards against path traversal / odd characters.
    /// </summary>
    private static string SanitizeExtension(string? originalFileName)
    {
        var ext = Path.GetExtension(originalFileName ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 12)
            return string.Empty;

        // Keep only ".xxxx" of letters/digits — drop anything weird (e.g. "../", spaces).
        foreach (var c in ext[1..])
        {
            if (!char.IsLetterOrDigit(c))
                return string.Empty;
        }

        return ext;
    }
}
