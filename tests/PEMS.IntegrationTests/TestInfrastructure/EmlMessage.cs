using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Reads a produced <c>.eml</c> the way a mail client would: headers unfolded, RFC 2047 encoded-words
/// decoded, body decoded from whatever transfer encoding .NET chose.
///
/// <para>
/// It exists so every email-evidence suite asks the same questions of the same bytes. Assertions about
/// privacy depend on the difference between "this address is in a header" and "this text is in the body",
/// and a per-suite copy of the parsing is a good way to have that distinction quietly drift.
/// </para>
/// </summary>
public sealed class EmlMessage
{
    private readonly string _raw;

    public EmlMessage(string raw) => _raw = raw;

    /// <summary>
    /// The whole message, headers and all parts. Needed for multipart assertions — an attachment's
    /// <c>Content-Type</c> and <c>Content-Disposition</c> live in a PART header, not the top-level one.
    /// </summary>
    public string Raw => _raw;

    /// <summary>
    /// A header's value with folded continuation lines joined, read ONLY from the header block. An
    /// address that appears in the body is not a leak; one in a header is.
    /// </summary>
    public string Header(string name)
    {
        var lines = _raw.Replace("\r\n", "\n").Split('\n');
        var value = new StringBuilder();
        var capturing = false;

        foreach (var line in lines)
        {
            if (line.Length == 0) break;

            if (capturing)
            {
                if (line[0] is ' ' or '\t') { value.Append(line.Trim()); continue; }
                break;
            }

            if (line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
            {
                capturing = true;
                value.Append(line[(name.Length + 1)..].Trim());
            }
        }

        return value.ToString();
    }

    /// <summary>A header with any RFC 2047 encoded-words decoded back to readable text.</summary>
    public string DecodedHeader(string name) => DecodeEncodedWords(Header(name));

    /// <summary>
    /// Every <c>text/*</c> part of the message, decoded from its own transfer encoding and concatenated
    /// — for asking whether some text appears anywhere a recipient would read it.
    ///
    /// <para>
    /// <see cref="Raw"/> cannot answer that question. This sender writes the HTML part as base64, so the
    /// body is not present as readable characters at all; even as quoted-printable it would be wrapped
    /// at 76 characters, and a perfectly present <c>&lt;table</c> could sit in the file as
    /// <c>&lt;ta=\r\nble</c>. Both failure modes are asymmetric and dangerous: a positive assertion turns
    /// into a false alarm, but a NEGATIVE one — "this internal note is absent" — turns into a false pass,
    /// and that is the assertion whose entire purpose is to catch a leak.
    /// </para>
    /// <para>
    /// Binary parts (the PDF) are deliberately NOT included: their content streams are compressed, so
    /// searching them proves nothing in either direction.
    /// </para>
    /// </summary>
    public string DecodedTextParts
    {
        get
        {
            var lines = _raw.Replace("\r\n", "\n").Split('\n');
            var output = new StringBuilder();

            var inHeaders = true;          // every part opens with a header block
            var isText = false;
            var encoding = string.Empty;
            var body = new StringBuilder();

            void Flush()
            {
                if (isText && body.Length > 0) output.Append(Decode(body.ToString(), encoding)).Append('\n');
                body.Clear();
            }

            foreach (var line in lines)
            {
                // A boundary delimiter always begins with "--" at column 0 and closes the current part.
                if (line.StartsWith("--", StringComparison.Ordinal))
                {
                    Flush();
                    inHeaders = true;
                    isText = false;
                    encoding = string.Empty;
                    continue;
                }

                if (inHeaders)
                {
                    if (line.Length == 0) { inHeaders = false; continue; }

                    if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
                        isText = line.Contains("text/", StringComparison.OrdinalIgnoreCase);
                    else if (line.StartsWith("Content-Transfer-Encoding:", StringComparison.OrdinalIgnoreCase))
                        encoding = line["Content-Transfer-Encoding:".Length..].Trim();

                    continue;
                }

                body.Append(line).Append('\n');
            }

            Flush();
            return output.ToString();
        }
    }

    private static string Decode(string body, string encoding)
    {
        if (encoding.Equals("base64", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(
                    new string(body.Where(c => !char.IsWhiteSpace(c)).ToArray())));
            }
            catch (FormatException)
            {
                return body;    // not our business to fail here; the assertion will say what is missing
            }
        }

        return encoding.Equals("quoted-printable", StringComparison.OrdinalIgnoreCase)
            ? DecodeQuotedPrintable(body.Replace("=\n", string.Empty), Encoding.UTF8)
            : body;
    }

    /// <summary>How many addresses an address header lists.</summary>
    public int AddressCount(string name) => Header(name).Count(c => c == '@');

    /// <summary>The message body, decoded from base64 / quoted-printable when that is how it was sent.</summary>
    public string Body
    {
        get
        {
            var normalized = _raw.Replace("\r\n", "\n");
            var split = normalized.IndexOf("\n\n", StringComparison.Ordinal);
            var headers = split < 0 ? normalized : normalized[..split];
            var body = split < 0 ? string.Empty : normalized[(split + 2)..];

            if (headers.Contains("base64", StringComparison.OrdinalIgnoreCase))
                return Encoding.UTF8.GetString(Convert.FromBase64String(
                    new string(body.Where(c => !char.IsWhiteSpace(c)).ToArray())));

            if (headers.Contains("quoted-printable", StringComparison.OrdinalIgnoreCase))
                return DecodeQuotedPrintable(body.Replace("=\n", string.Empty), Encoding.UTF8);

            return body;
        }
    }

    /// <summary>
    /// The fixed text at the start of a stored subject, up to its first placeholder — what a produced
    /// subject can be matched against whether or not the template interpolates anything.
    /// </summary>
    public static string LiteralPrefix(string? storedSubject)
    {
        if (string.IsNullOrWhiteSpace(storedSubject))
            throw new InvalidOperationException("Template subject is empty — nothing to compare against.");

        var at = storedSubject.IndexOf("{{", StringComparison.Ordinal);
        return (at < 0 ? storedSubject : storedSubject[..at]).Trim();
    }

    private static string DecodeEncodedWords(string value)
    {
        var result = new StringBuilder();
        var rest = value;

        while (true)
        {
            var start = rest.IndexOf("=?", StringComparison.Ordinal);
            if (start < 0) { result.Append(rest); break; }

            result.Append(rest[..start]);
            var end = rest.IndexOf("?=", start + 2, StringComparison.Ordinal);
            if (end < 0) { result.Append(rest[start..]); break; }

            var parts = rest[(start + 2)..end].Split('?');
            if (parts.Length == 3)
            {
                var charset = Encoding.GetEncoding(parts[0]);
                result.Append(parts[1].ToUpperInvariant() == "B"
                    ? charset.GetString(Convert.FromBase64String(parts[2]))
                    : DecodeQuotedPrintable(parts[2], charset));
            }

            rest = rest[(end + 2)..];
        }

        return result.ToString();
    }

    private static string DecodeQuotedPrintable(string text, Encoding charset)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < text.Length; i++)
        {
            // Only a '=' followed by two HEX digits is an escape. Applied over a whole message this
            // matters: base64 padding ("...==") and MIME boundaries contain '=' followed by anything,
            // and treating those as escapes threw FormatException on a message that was perfectly fine.
            if (text[i] == '=' && i + 2 < text.Length
                && IsHex(text[i + 1]) && IsHex(text[i + 2]))
            {
                bytes.Add(Convert.ToByte(text.Substring(i + 1, 2), 16));
                i += 2;
            }
            else bytes.Add((byte)(text[i] == '_' ? ' ' : text[i]));
        }

        return charset.GetString(bytes.ToArray());
    }

    private static bool IsHex(char c)
        => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}
