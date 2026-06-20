namespace PEMS.Application.Common.Security;

/// <summary>
/// Server-side HTML sanitizer used as a defence-in-depth layer before persisting
/// user-supplied rich text (e.g. News content). Strips scripts, event handlers and
/// dangerous URL schemes so stored content is safe to render later. The frontend
/// render-time sanitize (<c>sanitizeHtml.ts</c>) remains in place as a second layer.
/// </summary>
public interface IHtmlSanitizerService
{
    /// <summary>
    /// Returns a sanitized copy of <paramref name="html"/>. Null or whitespace input
    /// returns <see cref="string.Empty"/>.
    /// </summary>
    string Sanitize(string? html);
}
