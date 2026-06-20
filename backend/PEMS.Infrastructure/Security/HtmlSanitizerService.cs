using Ganss.Xss;
using PEMS.Application.Common.Security;

namespace PEMS.Infrastructure.Security;

/// <summary>
/// <see cref="IHtmlSanitizerService"/> implementation backed by Ganss.Xss. The allow-list
/// configuration is kept explicit (even where the library already blocks a tag/attribute)
/// so the security posture is easy to audit. Registered as a singleton — the underlying
/// <see cref="HtmlSanitizer"/> is thread-safe for the <c>Sanitize</c> call.
/// </summary>
public sealed class HtmlSanitizerService : IHtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();

        // Disallow tags that can execute script or embed external content.
        foreach (var tag in new[] { "script", "iframe", "object", "embed", "form", "input", "button", "style", "noscript", "base", "link", "meta" })
        {
            _sanitizer.AllowedTags.Remove(tag);
        }

        // Disallow inline event handlers.
        foreach (var attr in new[] { "onclick", "onerror", "onload", "onmouseover", "onfocus", "onblur", "onchange", "onsubmit", "style" })
        {
            _sanitizer.AllowedAttributes.Remove(attr);
        }

        // Only permit safe URL schemes (blocks javascript:, vbscript:, data:text/html, ...).
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");
        _sanitizer.AllowedSchemes.Add("tel");
    }

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return _sanitizer.Sanitize(html);
    }
}
