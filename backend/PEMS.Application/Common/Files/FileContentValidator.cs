using PEMS.Application.Common.Exceptions;

namespace PEMS.Application.Common.Files;

/// <summary>
/// Stateless validation of an uploaded file against a <see cref="FileValidationRule"/>: size,
/// extension allowlist, MIME allowlist, and (for image rules) magic-byte sniffing that blocks SVG and
/// spoofed content types. Throws <see cref="BusinessRuleException"/> with the standardized
/// <c>FILE_*</c> error codes. Returns the canonical MIME type + a display-safe original filename.
/// </summary>
public static class FileContentValidator
{
    public sealed record ValidatedFile(string MimeType, string SafeOriginalName);

    public static ValidatedFile Validate(
        string? originalFileName, string? contentType, byte[] content, FileValidationRule rule)
    {
        if (content is null || content.Length == 0)
            throw new BusinessRuleException("Tệp tải lên rỗng.", "FILE_EMPTY");

        if (content.LongLength > rule.MaxSizeBytes)
            throw new BusinessRuleException(
                $"Tệp vượt quá kích thước tối đa {rule.MaxSizeBytes / (1024 * 1024)}MB.", "FILE_TOO_LARGE");

        var ext = Path.GetExtension(originalFileName ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !rule.AllowedExtensions.Contains(ext))
            throw new BusinessRuleException("Định dạng tệp không được hỗ trợ.", "FILE_INVALID_EXTENSION");

        var declared = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        // Normalize the common "image/jpg" alias before checking the allowlist.
        if (declared == "image/jpg") declared = "image/jpeg";

        if (rule.RequireImageMagicBytes)
        {
            // For images, the bytes are the source of truth — derive the canonical MIME from them and
            // require that the extension and declared content type agree (rejects SVG outright).
            var sniffed = SniffImageMime(content)
                ?? throw new BusinessRuleException(
                    "Nội dung tệp không phải ảnh hợp lệ (JPG/PNG/WEBP).", "FILE_MAGIC_BYTES_MISMATCH");

            if (!rule.AllowedMimeTypes.Contains(sniffed))
                throw new BusinessRuleException("Định dạng ảnh không được hỗ trợ.", "FILE_INVALID_TYPE");

            if (!ExtensionMatchesMime(ext, sniffed) || (declared.Length > 0 && declared != sniffed))
                throw new BusinessRuleException(
                    "Phần mở rộng hoặc kiểu nội dung không khớp với nội dung ảnh.", "FILE_MAGIC_BYTES_MISMATCH");

            return new ValidatedFile(sniffed, SafeOriginalName(originalFileName, ext));
        }

        // Non-image purposes: trust the extension allowlist; the declared MIME must be in the allowlist
        // when present, otherwise fall back to the extension's canonical MIME.
        if (declared.Length > 0 && !rule.AllowedMimeTypes.Contains(declared))
            throw new BusinessRuleException("Kiểu nội dung tệp không được hỗ trợ.", "FILE_INVALID_TYPE");

        var mime = declared.Length > 0 ? declared : MimeForExtension(ext);
        return new ValidatedFile(mime, SafeOriginalName(originalFileName, ext));
    }

    private static string? SniffImageMime(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return "image/png";
        if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46  // "RIFF"
            && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)              // "WEBP"
            return "image/webp";
        return null;
    }

    private static bool ExtensionMatchesMime(string ext, string mime) => mime switch
    {
        "image/jpeg" => ext is ".jpg" or ".jpeg",
        "image/png" => ext == ".png",
        "image/webp" => ext == ".webp",
        _ => false,
    };

    private static string MimeForExtension(string ext) => ext switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        _ => "application/octet-stream",
    };

    /// <summary>Strips any path, keeps a sane filename for display/audit (never used as a disk path).</summary>
    private static string SafeOriginalName(string? fileName, string ext)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) name = $"file{ext}";
        return name.Length > 255 ? name[^255..] : name;
    }
}
