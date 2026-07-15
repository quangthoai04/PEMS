using System;
using System.Text.RegularExpressions;
using PEMS.Application.Common.Exceptions;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// How a gallery media file is sourced. Distinct from <c>media_type</c> (IMAGE/VIDEO): a VIDEO can be an
/// uploaded Drive file or an external YouTube reference. The frontend renders by <see cref="SourceType"/>.
/// </summary>
public static class GalleryMediaSourceTypes
{
    public const string UploadedFile = "UPLOADED_FILE";
    public const string YouTube = "YOUTUBE";

    /// <summary>MIME marker written to <c>files.mime_type</c> for a YouTube metadata row.</summary>
    public const string YouTubeMimeType = "video/youtube";
}

/// <summary>Canonicalised YouTube reference produced by <see cref="YouTubeUrlParser"/>.</summary>
public sealed record YouTubeVideoRef(string VideoId)
{
    public string WatchUrl => $"https://www.youtube.com/watch?v={VideoId}";
    /// <summary>Privacy-enhanced embed origin used in the public iframe (nocookie).</summary>
    public string EmbedUrl => $"https://www.youtube-nocookie.com/embed/{VideoId}";
    public string ThumbnailUrl => $"https://i.ytimg.com/vi/{VideoId}/hqdefault.jpg";
    public string OriginalFileName => $"youtube-{VideoId}";
}

/// <summary>
/// Validates and canonicalises a YouTube URL into a <see cref="YouTubeVideoRef"/>. Pure — no network / no
/// YouTube Data API (that is an optional later phase). The host is checked against an exact allow-list
/// (never a substring <c>Contains("youtube.com")</c>, which would pass <c>youtube.com.attacker.com</c>),
/// and the id must match the canonical 11-char YouTube format. Throws <see cref="BusinessRuleException"/>
/// (422) with a stable <see cref="GalleryErrorCodes"/> code on any invalid input.
/// </summary>
public static class YouTubeUrlParser
{
    private const int MaxUrlLength = 400;

    // Canonical YouTube video id: exactly 11 chars of [A-Za-z0-9_-].
    private static readonly Regex VideoIdRegex = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);

    private static readonly string[] WatchHosts = { "youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com" };
    private const string ShortHost = "youtu.be";

    /// <summary>
    /// Extracts the video id from a supported URL form and returns a canonical reference. Throws on any
    /// empty / malformed / non-YouTube / bad-id input.
    /// </summary>
    public static YouTubeVideoRef Parse(string? youtubeUrl)
    {
        if (string.IsNullOrWhiteSpace(youtubeUrl))
            throw new BusinessRuleException("Vui lòng nhập URL video YouTube.", GalleryErrorCodes.YoutubeUrlRequired);

        var raw = youtubeUrl.Trim();
        if (raw.Length > MaxUrlLength)
            throw new BusinessRuleException("URL YouTube quá dài.", GalleryErrorCodes.YoutubeUrlInvalid);

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new BusinessRuleException("URL YouTube không hợp lệ.", GalleryErrorCodes.YoutubeUrlInvalid);

        var host = uri.Host.ToLowerInvariant();
        var isShort = host == ShortHost;
        var isWatch = Array.IndexOf(WatchHosts, host) >= 0;
        if (!isShort && !isWatch)
            throw new BusinessRuleException(
                "URL không thuộc miền YouTube được cho phép.", GalleryErrorCodes.YoutubeHostNotAllowed);

        var candidate = isShort ? ExtractFromShort(uri) : ExtractFromWatch(uri);
        if (string.IsNullOrEmpty(candidate) || !VideoIdRegex.IsMatch(candidate))
            throw new BusinessRuleException(
                "Không lấy được mã video YouTube hợp lệ từ URL.", GalleryErrorCodes.YoutubeVideoIdInvalid);

        return new YouTubeVideoRef(candidate);
    }

    /// <summary>youtu.be/{id} — the id is the first (only) path segment.</summary>
    private static string? ExtractFromShort(Uri uri) => FirstPathSegment(uri);

    /// <summary>
    /// youtube.com forms: <c>/watch?v={id}</c>, <c>/shorts/{id}</c>, <c>/embed/{id}</c>. Only these
    /// well-known shapes are accepted; a random query param is never treated as an id.
    /// </summary>
    private static string? ExtractFromWatch(Uri uri)
    {
        var path = uri.AbsolutePath.Trim('/');
        var segments = path.Length == 0 ? Array.Empty<string>() : path.Split('/');

        if (segments.Length == 0)
            return QueryValue(uri.Query, "v"); // /watch?v=ID

        var first = segments[0].ToLowerInvariant();
        if (first == "watch")
            return QueryValue(uri.Query, "v");
        if ((first == "shorts" || first == "embed" || first == "v" || first == "live") && segments.Length >= 2)
            return segments[1];

        return null;
    }

    /// <summary>Reads a single query-string value (e.g. <c>v</c>) without pulling in System.Web.</summary>
    private static string? QueryValue(string query, string key)
    {
        if (string.IsNullOrEmpty(query)) return null;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var k = eq < 0 ? pair : pair.Substring(0, eq);
            if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) continue;
            var v = eq < 0 ? string.Empty : pair.Substring(eq + 1);
            return Uri.UnescapeDataString(v);
        }
        return null;
    }

    private static string? FirstPathSegment(Uri uri)
    {
        var path = uri.AbsolutePath.Trim('/');
        if (path.Length == 0) return null;
        var slash = path.IndexOf('/');
        return slash < 0 ? path : path.Substring(0, slash);
    }
}
