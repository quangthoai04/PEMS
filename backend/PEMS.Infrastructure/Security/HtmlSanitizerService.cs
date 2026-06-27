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
    private readonly HtmlSanitizer _emailSanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = BuildBase();

        // Email bodies additionally allow inline-image references (cid:) + the data attributes the
        // editor uses to map an <img> back to its file/content id, so inline images survive sanitize
        // and resolve to MIME linked resources when sent.
        _emailSanitizer = BuildBase();
        _emailSanitizer.AllowedSchemes.Add("cid");
        _emailSanitizer.AllowedAttributes.Add("data-content-id");
        _emailSanitizer.AllowedAttributes.Add("data-file-id");
    }

    private static HtmlSanitizer BuildBase()
    {
        var sanitizer = new HtmlSanitizer();

        // Disallow tags that can execute script or embed external content.
        foreach (var tag in new[] { "script", "iframe", "object", "embed", "form", "input", "button", "style", "noscript", "base", "link", "meta" })
        {
            sanitizer.AllowedTags.Remove(tag);
        }

        // Disallow inline event handlers.
        foreach (var attr in new[] { "onclick", "onerror", "onload", "onmouseover", "onfocus", "onblur", "onchange", "onsubmit", "style" })
        {
            sanitizer.AllowedAttributes.Remove(attr);
        }

        // Only permit safe URL schemes (blocks javascript:, vbscript:, data:text/html, ...).
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.AllowedSchemes.Add("tel");

        return sanitizer;
    }

    public string Sanitize(string? html)
        => string.IsNullOrWhiteSpace(html) ? string.Empty : _sanitizer.Sanitize(html);

    public string SanitizeEmailHtml(string? html)
        => string.IsNullOrWhiteSpace(html) ? string.Empty : _emailSanitizer.Sanitize(html);
}
