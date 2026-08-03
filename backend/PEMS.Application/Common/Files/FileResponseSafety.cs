using System;
using System.Collections.Generic;
using System.Linq;

namespace PEMS.Application.Common.Files;

/// <summary>
/// What a file response is allowed to say about itself, and how a client is allowed to address it.
///
/// <para>
/// Two separate problems live here because they have the same cause — a stored <c>files</c> row is
/// data, and data that ends up in a response header or an <c>&lt;iframe&gt;</c> decides how the
/// browser behaves. <see cref="SafeFileName"/> keeps a filename from carrying a path or a header
/// break into <c>Content-Disposition</c>; <see cref="SafeInlineContentType"/> keeps a document that
/// executes script from being served in a way a browser would render as a page.
/// </para>
/// </summary>
public static class FileResponseSafety
{
    /// <summary>Long enough for any real document name, short enough not to blow a header budget.</summary>
    private const int MaxFileNameLength = 180;

    private const string FallbackFileName = "file";

    /// <summary>Served to a browser when the caller asked for INLINE rendering.</summary>
    public const string NeutralContentType = "application/octet-stream";

    /// <summary>
    /// Types a browser would execute or treat as a document in the responding origin. SVG is on this
    /// list for the same reason HTML is: it carries <c>&lt;script&gt;</c>. These are still downloadable —
    /// only the inline rendering path refuses to name them.
    /// </summary>
    private static readonly HashSet<string> InlineUnsafeContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/html",
        "application/xhtml+xml",
        "image/svg+xml",
        "text/xml",
        "application/xml",
        "text/xsl",
        "application/xslt+xml",
        "text/javascript",
        "application/javascript",
        "application/x-javascript",
    };

    /// <summary>
    /// A filename safe to put in <c>Content-Disposition</c> and safe for a browser to write to disk.
    ///
    /// <para>
    /// Strips the directory part (a stored name like <c>../../etc/passwd</c> must suggest
    /// <c>passwd</c>, never a path), then removes control characters — CR and LF above all, because a
    /// header value that contains them is a header-injection primitive rather than a name.
    /// </para>
    /// </summary>
    public static string SafeFileName(string? originalFilename)
    {
        if (string.IsNullOrWhiteSpace(originalFilename)) return FallbackFileName;

        // Leaf only. Both separators are checked because the stored value may have come from either OS.
        var leaf = originalFilename;
        var lastSeparator = leaf.LastIndexOfAny(new[] { '/', '\\' });
        if (lastSeparator >= 0) leaf = leaf[(lastSeparator + 1)..];

        // Control characters (CR/LF/NUL/TAB and friends) plus the quote that would end the header's
        // quoted-string early. Everything else — including Vietnamese diacritics — is kept: ASP.NET
        // Core encodes non-ASCII into the RFC 5987 `filename*` form, so the name survives intact.
        var cleaned = new string(leaf
            .Where(c => !char.IsControl(c) && c != '"')
            .ToArray())
            .Trim()
            .Trim('.');   // a name that is only dots addresses a directory, not a file

        if (string.IsNullOrWhiteSpace(cleaned)) return FallbackFileName;

        return cleaned.Length > MaxFileNameLength ? cleaned[..MaxFileNameLength] : cleaned;
    }

    /// <summary>
    /// The content type to declare when the bytes are rendered INLINE. An unsafe or unknown type is
    /// reported as <see cref="NeutralContentType"/>, which a browser downloads instead of rendering.
    ///
    /// <para>
    /// Callers that stream as an attachment do not need this: a download is not a rendering context.
    /// </para>
    /// </summary>
    public static string SafeInlineContentType(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType)) return NeutralContentType;

        // Compare the bare type: `text/html; charset=utf-8` must be recognised as text/html.
        var bare = mimeType.Split(';')[0].Trim();
        if (string.IsNullOrEmpty(bare)) return NeutralContentType;

        return InlineUnsafeContentTypes.Contains(bare) ? NeutralContentType : bare;
    }
}

/// <summary>
/// The only addresses a client is given for a stored file.
///
/// <para>
/// Both point at our own authenticated routes, never at the storage provider. A Drive
/// <c>webContentLink</c> in a response is two problems at once: it names the provider's file id to
/// anyone reading the payload, and it invites the client to fetch bytes down a path where
/// <c>FileAccessAuthorizationService</c> was never consulted. The provider reference stays server-side.
/// </para>
/// </summary>
public static class InternalFileUrls
{
    /// <summary>Streams the bytes as an attachment (save-as).</summary>
    public static string Download(ulong fileId) => $"/api/files/{fileId}/download";

    /// <summary>Streams the same bytes for inline rendering (preview).</summary>
    public static string Content(ulong fileId) => $"/api/files/{fileId}/content";
}
