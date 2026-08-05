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

        // The marker for a system block's POSITION (today: the action buttons). It has to survive the
        // sanitiser because an edited body makes its round trip through here, and the attribute is the
        // only thing distinguishing "the author moved the action area to this paragraph" from an ordinary
        // empty div. Stripping it would silently discard the position and send the buttons somewhere else.
        //
        // Safe to allow: it is an ATTRIBUTE NAME on a div, carrying no URL and no script. The markup it
        // stands for is never accepted from a client — the backend substitutes the real block over this
        // node at send time, and an author who writes the node by hand gets an empty div and no buttons.
        _emailSanitizer.AllowedAttributes.Add("data-system-block");

        // ── Inline style, on EMAIL bodies only, restricted to a named list of properties ──
        //
        // <b>What this fixes.</b> `BuildBase` strips `style`, and an email body is sanitised on its way
        // out — so every table the backend builds into it arrived at the recipient stripped of
        // `border`, `padding`, `background`, `width` and `table-layout:fixed`. The tables declare their
        // borders in CSS and carry `border="0"` as the attribute, so the message went out as a grid with
        // no lines, no cell padding and automatic layout: headers running together, columns crushed to
        // their longest syllable. It looked like a rendering bug in the mail client, and it was
        // invisible in the composer, because the FRONTEND sanitiser keeps `style` — so the preview and
        // the delivered mail were two different documents.
        //
        // <b>Why a list and not simply "allow style".</b> An open `style` attribute is a real surface:
        // `position`/`z-index` can lift attacker content over the rest of a message, and historic
        // `expression()`/`behavior:` are script by another name. Only the properties the email
        // renderers and the rich-text editor actually emit are permitted; anything else is dropped as
        // before. URL values inside a property (`background:url(...)`) are still checked against
        // `AllowedSchemes`, which is why that list stays restricted above.
        _emailSanitizer.AllowedAttributes.Add("style");
        _emailSanitizer.AllowedCssProperties.Clear();
        foreach (var property in new[]
        {
            // Box + table layout: what the setup-progress and contact tables depend on.
            //
            // The per-side longhands are listed as well as the shorthands, and they are not optional:
            // the CSS parser EXPANDS `border:1px solid #374151` into `border-top-width`,
            // `border-top-style`, `border-top-color` and their three siblings, then checks each of those
            // against this list. With only `border` on it the whole declaration was dropped and the
            // tables came out with no lines at all — the original defect, surviving the fix for it.
            "border", "border-collapse", "border-color", "border-style", "border-width",
            "border-top", "border-right", "border-bottom", "border-left", "border-radius",
            "border-top-width", "border-top-style", "border-top-color",
            "border-right-width", "border-right-style", "border-right-color",
            "border-bottom-width", "border-bottom-style", "border-bottom-color",
            "border-left-width", "border-left-style", "border-left-color",
            "border-top-left-radius", "border-top-right-radius",
            "border-bottom-left-radius", "border-bottom-right-radius",
            "border-spacing", "table-layout", "caption-side", "empty-cells",
            "padding", "padding-top", "padding-right", "padding-bottom", "padding-left",
            "margin", "margin-top", "margin-right", "margin-bottom", "margin-left",
            "width", "min-width", "max-width", "height", "min-height", "max-height",
            "display", "float", "clear", "overflow",

            // Text: wrapping is the other half of the fixed-layout fix.
            "text-align", "vertical-align", "white-space", "overflow-wrap", "word-break", "word-wrap",
            "text-decoration", "text-transform", "text-indent", "letter-spacing", "line-height",
            "font", "font-family", "font-size", "font-style", "font-weight", "font-variant",
            "direction", "list-style", "list-style-type", "list-style-position",

            // Colour. `background` keeps its shorthand form because that is what every template and
            // action block already emits (including the header's linear-gradient); a url() inside it
            // still has to pass the scheme allow-list.
            "color", "background", "background-color", "background-image", "background-position",
            "background-repeat", "background-size", "opacity", "box-shadow",
        })
        {
            _emailSanitizer.AllowedCssProperties.Add(property);
        }

        // Email links must open in a new tab and not leak the opener. Allow the attributes and force
        // them onto every <a href> after sanitize, so user-inserted links are safe by construction.
        _emailSanitizer.AllowedAttributes.Add("target");
        _emailSanitizer.AllowedAttributes.Add("rel");
        _emailSanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is not AngleSharp.Dom.IElement el) return;

            if (string.Equals(el.TagName, "A", System.StringComparison.OrdinalIgnoreCase)
                && el.HasAttribute("href"))
            {
                el.SetAttribute("target", "_blank");
                el.SetAttribute("rel", "noopener noreferrer");
            }

            StripHidingDeclarations(el);
        };
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

        // Allow data:image/* (ReactQuill embeds images as base64) but block all other data: payloads.
        sanitizer.RemovingAttribute += (_, args) =>
        {
            if (args.Reason == RemoveReason.NotAllowedUrlValue)
            {
                var v = args.Attribute.Value?.TrimStart() ?? string.Empty;
                if (v.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                    args.Cancel = true; // cancel removal → keep the attribute
            }
        };

        return sanitizer;
    }

    /// <summary>
    /// Declarations whose only effect is to make content unreadable while leaving it in the message.
    ///
    /// <para>
    /// <c>display</c> and <c>opacity</c> are on the allow-list because the templates need
    /// <c>display:block</c> and <c>display:inline-block</c>, and dropping the whole property to stop the
    /// hiding values would flatten every button in every action block. So the VALUES are matched instead:
    /// the property survives, the hiding form of it does not.
    /// </para>
    /// <para>
    /// Why it matters in mail specifically: invisible text is how a message is made to read one way to a
    /// recipient and another to a filter, and how a tracking marker is smuggled past somebody who looked
    /// at the message before approving it. A sender approving a final preview must be approving
    /// everything the mail carries — text they cannot see is text they did not approve.
    /// </para>
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex HidingDeclaration = new(
        @"(?:^|;)\s*(?:"
        + @"display\s*:\s*none"
        + @"|visibility\s*:\s*(?:hidden|collapse)"
        + @"|opacity\s*:\s*0*(?:\.0+)?(?=\s*(?:;|$))"
        + @"|font-size\s*:\s*0(?:\.0+)?(?:px|pt|em|rem|%)?(?=\s*(?:;|$))"
        + @"|text-indent\s*:\s*-\d"
        + @")[^;]*",
        System.Text.RegularExpressions.RegexOptions.Compiled
        | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// Removes the hiding declarations from one element's inline style, leaving the rest of it intact.
    /// An element left with an empty style attribute has it dropped rather than kept as <c>style=""</c>.
    /// </summary>
    private static void StripHidingDeclarations(AngleSharp.Dom.IElement el)
    {
        var style = el.GetAttribute("style");
        if (string.IsNullOrEmpty(style) || !HidingDeclaration.IsMatch(style)) return;

        var cleaned = HidingDeclaration.Replace(style, string.Empty).Trim().Trim(';').Trim();

        if (cleaned.Length == 0) el.RemoveAttribute("style");
        else el.SetAttribute("style", cleaned);
    }

    /// <summary>
    /// A fully opaque <c>rgba(r, g, b, 1)</c>, which is what the CSS parser rewrites every hex colour to.
    /// Translucent values are deliberately NOT matched — see <see cref="ToMailSafeColours"/>.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex OpaqueRgba = new(
        @"rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*1(?:\.0+)?\s*\)",
        System.Text.RegularExpressions.RegexOptions.Compiled
        | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// Puts opaque colours back into hex notation after sanitising.
    ///
    /// <para>
    /// The CSS parser normalises every colour it reparses, so <c>#374151</c> comes out as
    /// <c>rgba(55, 65, 81, 1)</c>. That is the same colour to a browser and to Gmail — and nothing at all
    /// to Outlook's Word rendering engine, which does not understand <c>rgba()</c> and drops the
    /// declaration. Left alone it would have quietly undone the border fix in the one client the tables
    /// are laid out for, and the symptom would have been identical to the original defect.
    /// </para>
    /// <para>
    /// Only alpha = 1 is converted, because only that is expressible as hex. A genuinely translucent
    /// value (the shell's <c>box-shadow: rgba(0,0,0,.08)</c>) keeps its notation: Outlook ignores
    /// box-shadow either way, and rewriting it to an opaque hex would turn a faint shadow into a solid
    /// black bar in the clients that DO support it.
    /// </para>
    /// </summary>
    private static string ToMailSafeColours(string html) =>
        OpaqueRgba.Replace(html, m =>
            $"#{int.Parse(m.Groups[1].Value):x2}{int.Parse(m.Groups[2].Value):x2}{int.Parse(m.Groups[3].Value):x2}");

    public string Sanitize(string? html)
        => string.IsNullOrWhiteSpace(html) ? string.Empty : _sanitizer.Sanitize(html);

    public string SanitizeEmailHtml(string? html)
        => string.IsNullOrWhiteSpace(html)
            ? string.Empty
            : ToMailSafeColours(_emailSanitizer.Sanitize(html));
}
