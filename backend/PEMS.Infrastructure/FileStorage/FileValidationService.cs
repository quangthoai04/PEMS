using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.FileStorage;

/// <summary>
/// Default <see cref="IFileValidationService"/>. Uses a denylist of dangerous extensions
/// and MIME types (SVG/HTML/JS/executable/scriptable) so a file is rejected even if the
/// client lies about its content type or renames the extension. Business flows that need
/// a stricter allow-list (e.g. images only) should validate again on top of this.
/// </summary>
public sealed class FileValidationService : IFileValidationService
{
    /// <summary>Hard upper bound; individual flows may enforce a smaller limit before calling.</summary>
    private const long MaxFileSizeBytes = 25L * 1024 * 1024; // 25 MB

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Markup / scriptable (stored-XSS risk)
        ".svg", ".svgz", ".html", ".htm", ".xhtml", ".shtml", ".xml",
        ".js", ".mjs", ".cjs", ".jsx", ".ts", ".tsx",
        ".vbs", ".vbe", ".wsf", ".wsh", ".hta",
        // Server-side scripts
        ".php", ".php3", ".php4", ".php5", ".phtml",
        ".asp", ".aspx", ".ascx", ".jsp", ".jspx",
        // Executables / scripts (RCE risk)
        ".exe", ".com", ".scr", ".msi", ".dll",
        ".bat", ".cmd", ".ps1", ".psm1", ".sh", ".bash",
        ".jar", ".war",
    };

    private static readonly HashSet<string> BlockedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/svg+xml",
        "text/html",
        "application/xhtml+xml",
        "application/javascript",
        "text/javascript",
        "application/ecmascript",
        "text/ecmascript",
        "application/x-javascript",
        "application/x-msdownload",
        "application/x-sh",
        "application/x-httpd-php",
        "application/xml-dtd",
    };

    public bool IsAllowed(string fileName, string? contentType, long sizeBytes)
    {
        if (sizeBytes <= 0 || sizeBytes > MaxFileSizeBytes)
            return false;

        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (string.IsNullOrEmpty(extension) || BlockedExtensions.Contains(extension))
            return false;

        if (!string.IsNullOrWhiteSpace(contentType) && BlockedMimeTypes.Contains(contentType.Trim()))
            return false;

        return true;
    }

    public void Validate(string fileName, string? contentType, long sizeBytes)
    {
        if (sizeBytes <= 0)
            throw new BusinessRuleException("Tệp tải lên rỗng hoặc không hợp lệ.");

        if (sizeBytes > MaxFileSizeBytes)
            throw new BusinessRuleException($"Tệp vượt quá kích thước tối đa cho phép ({MaxFileSizeBytes / (1024 * 1024)}MB).");

        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (string.IsNullOrEmpty(extension))
            throw new BusinessRuleException("Tệp không có phần mở rộng hợp lệ.");

        if (BlockedExtensions.Contains(extension))
            throw new BusinessRuleException("Loại tệp này không được phép tải lên.");

        if (!string.IsNullOrWhiteSpace(contentType) && BlockedMimeTypes.Contains(contentType.Trim()))
            throw new BusinessRuleException("Loại tệp này không được phép tải lên.");
    }
}
