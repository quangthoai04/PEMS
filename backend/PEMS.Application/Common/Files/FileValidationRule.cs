namespace PEMS.Application.Common.Files;

/// <summary>
/// The size / type / extension constraints for one <see cref="FilePurpose"/>. Resolved by
/// <see cref="Interfaces.IFileValidationPolicy"/> and enforced server-side by the shared upload
/// service (the frontend validates the same rules only for UX).
/// </summary>
public sealed class FileValidationRule
{
    public long MaxSizeBytes { get; init; }
    public IReadOnlySet<string> AllowedMimeTypes { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> AllowedExtensions { get; init; } = new HashSet<string>();

    /// <summary>When true, the leading bytes must match JPEG/PNG/WEBP (SVG and spoofed images rejected).</summary>
    public bool RequireImageMagicBytes { get; init; }
}
