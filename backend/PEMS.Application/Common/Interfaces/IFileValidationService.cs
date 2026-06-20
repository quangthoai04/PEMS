namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Validates uploaded files before they are stored. Blocks scriptable / executable
/// content (SVG, HTML, JS, executables, ...) that could lead to stored XSS or RCE,
/// regardless of the client-supplied content type. Framework-agnostic on purpose so it
/// can be called from any layer; the caller passes the file name, content type and size.
/// </summary>
public interface IFileValidationService
{
    /// <summary>
    /// Throws <see cref="PEMS.Application.Common.Exceptions.BusinessRuleException"/> when the
    /// file is empty, exceeds the size limit, or has a blocked extension / MIME type.
    /// </summary>
    void Validate(string fileName, string? contentType, long sizeBytes);

    /// <summary>True when the file passes every check (i.e. <see cref="Validate"/> would not throw).</summary>
    bool IsAllowed(string fileName, string? contentType, long sizeBytes);
}
