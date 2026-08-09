using System;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using PEMS.Application.Galleries.Common;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Api.PublicGallery;

/// <summary>
/// Open Graph metadata of one public gallery item, ready to be rendered into the crawler HTML.
/// Every URL is absolute HTTPS on the canonical frontend domain; the text fields are still RAW
/// (encoding happens once, in <see cref="PublicGallerySharePreviewBuilder.RenderHtml"/>).
/// </summary>
public sealed record PublicGalleryShareMetadata(
    string Title,
    string Description,
    string ImageUrl,
    string CanonicalUrl,
    string SiteName,
    string Locale);

/// <summary>
/// Turns a public gallery item detail into the Open Graph preview a social crawler
/// (facebookexternalhit / Facebot) receives when Vercel rewrites the canonical deep link to the
/// backend. Presentation only — visibility is already enforced by
/// <c>GetPublicGalleryItemDetailQuery</c>, so nothing here re-queries the database.
///
/// Canonical metadata is Vietnamese (the item's default language); the SPA still lets a real visitor
/// switch to English after the link is opened. Image selection never leaks a raw Google Drive URL: an
/// uploaded image is rebuilt from its trusted <c>FileId</c> onto the anonymous public media proxy, and
/// a YouTube reference uses its own https thumbnail.
/// </summary>
public static class PublicGallerySharePreviewBuilder
{
    public const string SiteName = "PEMS - VisitFPTU";
    public const string DefaultImagePath = "/og/gallery-default.jpg";
    private const string Locale = "vi_VN";
    private const string AlternateLocale = "en_US";

    /// <summary>gallery_item_media.media_type of a still image (the only kind Facebook can show).</summary>
    private const string ImageMediaType = "IMAGE";

    /// <summary>
    /// Escapes the HTML-significant characters (&lt; &gt; &amp; " ') but leaves Vietnamese letters as
    /// literal UTF-8 — the document declares charset=utf-8, and <see cref="HtmlEncoder.Default"/> would
    /// otherwise turn every accented character into a numeric entity, bloating the tags for no gain.
    /// </summary>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>Upper bound of og:description — Facebook truncates well before this anyway.</summary>
    private const int MaxDescriptionLength = 200;

    /// <summary>
    /// Builds the metadata for one item. <paramref name="campusCode"/>/<paramref name="locationId"/>
    /// are the values from the shared URL and are expected to have been validated against
    /// <paramref name="detail"/> by the caller (no cloaking — the crawler must see the same item the
    /// browser opens).
    /// </summary>
    public static PublicGalleryShareMetadata BuildMetadata(
        PublicGalleryItemDetailDto detail,
        string frontendBaseUrl,
        string campusCode,
        long locationId,
        long itemId)
    {
        var frontendBase = NormalizeBase(frontendBaseUrl);

        var title = string.IsNullOrWhiteSpace(detail.GalleryItem.Title)
            ? detail.Location.LocationName
            : detail.GalleryItem.Title.Trim();

        return new PublicGalleryShareMetadata(
            Title: title,
            Description: BuildDescription(detail),
            ImageUrl: SelectImageUrl(detail, frontendBase),
            CanonicalUrl: BuildCanonicalUrl(frontendBase, campusCode, locationId, itemId),
            SiteName: SiteName,
            Locale: Locale);
    }

    /// <summary>
    /// The one canonical URL both the visitor and the crawler use:
    /// <c>{frontendBase}/visit-fptu/{campusCode}?locationId={locationId}&amp;itemId={itemId}</c>.
    /// Built from configuration (never <c>Request.Host</c>, which on Railway is the internal origin)
    /// and from validated ids only — no free text is concatenated in.
    /// </summary>
    public static string BuildCanonicalUrl(string frontendBaseUrl, string campusCode, long locationId, long itemId)
    {
        var frontendBase = NormalizeBase(frontendBaseUrl);
        var code = Uri.EscapeDataString(campusCode.Trim().ToLowerInvariant());
        return $"{frontendBase}/visit-fptu/{code}?locationId={locationId}&itemId={itemId}";
    }

    /// <summary>
    /// Minimal crawler document: the Open Graph block plus a single link to the real page. It is NOT the
    /// SPA shell — no bundle, no script, no redirect (a real browser never reaches this endpoint, Vercel
    /// routes it by User-Agent). Every dynamic value is HTML-encoded exactly once, here.
    /// </summary>
    public static string RenderHtml(PublicGalleryShareMetadata metadata)
    {
        var title = Encoder.Encode(metadata.Title);
        var description = Encoder.Encode(metadata.Description);
        var image = Encoder.Encode(metadata.ImageUrl);
        var canonical = Encoder.Encode(metadata.CanonicalUrl);
        var siteName = Encoder.Encode(metadata.SiteName);
        var locale = Encoder.Encode(metadata.Locale);

        var html = new StringBuilder();
        html.Append("<!doctype html>\n<html lang=\"vi\">\n<head>\n");
        html.Append("  <meta charset=\"utf-8\" />\n");
        html.Append($"  <title>{title} | VisitFPTU</title>\n");
        html.Append($"  <meta name=\"description\" content=\"{description}\" />\n");
        html.Append($"  <link rel=\"canonical\" href=\"{canonical}\" />\n");
        html.Append("  <meta property=\"og:type\" content=\"website\" />\n");
        html.Append($"  <meta property=\"og:site_name\" content=\"{siteName}\" />\n");
        html.Append($"  <meta property=\"og:title\" content=\"{title}\" />\n");
        html.Append($"  <meta property=\"og:description\" content=\"{description}\" />\n");
        html.Append($"  <meta property=\"og:url\" content=\"{canonical}\" />\n");
        html.Append($"  <meta property=\"og:image\" content=\"{image}\" />\n");
        html.Append($"  <meta property=\"og:image:secure_url\" content=\"{image}\" />\n");
        html.Append($"  <meta property=\"og:image:alt\" content=\"{title}\" />\n");
        html.Append($"  <meta property=\"og:locale\" content=\"{locale}\" />\n");
        html.Append($"  <meta property=\"og:locale:alternate\" content=\"{AlternateLocale}\" />\n");
        html.Append("</head>\n<body>\n");
        html.Append($"  <p><a href=\"{canonical}\">{title}</a></p>\n");
        html.Append("</body>\n</html>\n");
        return html.ToString();
    }

    // ── Description ──────────────────────────────────────────────────────────

    /// <summary>
    /// og:description from the item's Vietnamese description: whitespace collapsed to single spaces and
    /// cut at a word boundary. Never empty — a blank description falls back to the item's place in the
    /// gallery, because a missing meta description makes Facebook render a bare card.
    /// </summary>
    private static string BuildDescription(PublicGalleryItemDetailDto detail)
    {
        var normalized = CollapseWhitespace(detail.GalleryItem.Content.Vi.Description);
        if (normalized.Length == 0)
        {
            normalized = CollapseWhitespace(
                $"Khám phá {detail.Location.LocationName} tại {detail.Campus.CampusName} trên VisitFPTU.");
        }

        return Truncate(normalized, MaxDescriptionLength);
    }

    private static string CollapseWhitespace(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var sb = new StringBuilder(raw.Length);
        var pendingSpace = false;
        foreach (var ch in raw)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0) pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max) return text;

        var cut = text.Substring(0, max);
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > max / 2) cut = cut.Substring(0, lastSpace);
        return cut.TrimEnd() + "…";
    }

    // ── Image selection ──────────────────────────────────────────────────────

    /// <summary>
    /// Picks the card image, in the order a human would: the primary media if it can produce one, then
    /// any other usable image, then any YouTube thumbnail, then the static site fallback. An uploaded
    /// VIDEO never becomes the og:image (Facebook would try to render an mp4 as a picture).
    /// </summary>
    private static string SelectImageUrl(PublicGalleryItemDetailDto detail, string frontendBase)
    {
        var media = detail.Media;
        var primary = media.FirstOrDefault(m => m.IsPrimary) ?? media.FirstOrDefault();

        if (primary is not null && TryImageUrl(primary, frontendBase, out var primaryUrl))
            return primaryUrl;

        // Next usable uploaded IMAGE, in display order.
        foreach (var m in media)
        {
            if (ReferenceEquals(m, primary)) continue;
            if (!IsUploadedImage(m)) continue;
            if (TryImageUrl(m, frontendBase, out var url)) return url;
        }

        // Then any YouTube thumbnail.
        foreach (var m in media)
        {
            if (ReferenceEquals(m, primary)) continue;
            if (!IsYouTube(m)) continue;
            if (TryImageUrl(m, frontendBase, out var url)) return url;
        }

        return frontendBase + DefaultImagePath;
    }

    private static bool TryImageUrl(PublicGalleryMediaDto media, string frontendBase, out string url)
    {
        if (IsUploadedImage(media))
        {
            // Rebuilt from the trusted FileId rather than the DTO's relative Url string.
            url = $"{frontendBase}/api/public/visit-fptu/media/{media.FileId}/content";
            return true;
        }

        if (IsYouTube(media) && IsHttpsUrl(media.ThumbnailUrl))
        {
            url = media.ThumbnailUrl!;
            return true;
        }

        url = string.Empty;
        return false;
    }

    private static bool IsUploadedImage(PublicGalleryMediaDto media) =>
        string.Equals(media.SourceType, GalleryMediaSourceTypes.UploadedFile, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(media.MediaType, ImageMediaType, StringComparison.OrdinalIgnoreCase);

    private static bool IsYouTube(PublicGalleryMediaDto media) =>
        string.Equals(media.SourceType, GalleryMediaSourceTypes.YouTube, StringComparison.OrdinalIgnoreCase);

    private static bool IsHttpsUrl(string? candidate) =>
        !string.IsNullOrWhiteSpace(candidate) &&
        Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps;

    private static string NormalizeBase(string frontendBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            throw new ArgumentException("Frontend base URL is required.", nameof(frontendBaseUrl));

        return frontendBaseUrl.Trim().TrimEnd('/');
    }
}
