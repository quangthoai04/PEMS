using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common;
using PEMS.Application.Emails.Preview;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// HMAC-SHA256 preview tokens: <c>base64url(payload) + "." + base64url(mac)</c>.
///
/// <para>
/// <b>Not a JWT, deliberately.</b> A JWT would bring a parser that accepts an <c>alg</c> header from the
/// token itself, a library surface far wider than this needs, and a format people expect to be able to
/// pass to other systems. This token is read by exactly one method in exactly one application: the
/// algorithm is fixed in code, there is no header to negotiate, and an attacker has nothing to select.
/// </para>
/// <para>
/// <b>Signed, not encrypted.</b> The payload is readable by anyone holding the token — which is the
/// sender, who prepared it. That is fine because it contains no message: only hashes, ids and an expiry
/// (see <see cref="EmailPreviewTokenPayload"/>). What matters is that it cannot be CHANGED, and a MAC
/// gives that without a key-management story for decryption.
/// </para>
/// </summary>
public sealed class EmailPreviewTokenService : IEmailPreviewTokenService
{
    /// <summary>
    /// Bumped when the payload's field order or meaning changes. Tokens of another version are refused
    /// rather than parsed leniently — a field that has moved would otherwise be read as its neighbour,
    /// and the resulting mismatch would be reported as a content change nobody made.
    /// </summary>
    private const string Version = "v1";

    private const char PayloadSeparator = '\u001F';

    private readonly byte[] _key;
    private readonly ILogger<EmailPreviewTokenService> _logger;

    public EmailPreviewTokenService(
        IConfiguration configuration, ILogger<EmailPreviewTokenService> logger)
    {
        _logger = logger;

        // The JWT signing key, reused rather than a second secret to deploy. Both authenticate something
        // this application issued to itself, both rotate together, and a separate key would be one more
        // thing a deployment can forget — with the failure showing up as "your preview expired" on every
        // send, which names neither the cause nor the fix.
        var secret = configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException(
                "JwtSettings:SecretKey is not configured; email preview tokens cannot be signed.");

        _key = Encoding.UTF8.GetBytes(secret);
    }

    public string Issue(EmailPreviewTokenPayload payload)
    {
        var body = Serialize(payload);
        var mac = Sign(body);

        return Base64Url(Encoding.UTF8.GetBytes(body)) + "." + Base64Url(mac);
    }

    public EmailPreviewTokenPayload? Verify(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var dot = token!.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1) return Reject("malformed: no separator");

        string body;
        byte[] presented;

        try
        {
            body = Encoding.UTF8.GetString(FromBase64Url(token[..dot]));
            presented = FromBase64Url(token[(dot + 1)..]);
        }
        catch (FormatException)
        {
            return Reject("malformed: not base64url");
        }

        // Fixed-time comparison. A byte-by-byte one leaks, through timing, how much of a forged MAC was
        // correct — which is enough to construct the rest a byte at a time.
        if (!CryptographicOperations.FixedTimeEquals(presented, Sign(body)))
            return Reject("signature mismatch");

        var payload = Deserialize(body);
        if (payload is null) return Reject("unreadable payload");

        // Expiry last, on a payload already proven authentic. Checking it first would answer "is this
        // shape right" for tokens that were never signed by us.
        if (payload.ExpiresAt <= VietnamTime.Now()) return Reject("expired");

        return payload;
    }

    private EmailPreviewTokenPayload? Reject(string reason)
    {
        // The reason is logged and never returned — see IEmailPreviewTokenService.Verify.
        _logger.LogWarning("Email preview token rejected: {Reason}", reason);
        return null;
    }

    private byte[] Sign(string body) => HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(body));

    private static string Serialize(EmailPreviewTokenPayload p) => string.Join(
        PayloadSeparator,
        Version,
        p.Purpose,
        p.ActorUserId.ToString(CultureInfo.InvariantCulture),
        p.TemplateCode,
        p.TemplateRevision.ToString(CultureInfo.InvariantCulture),
        p.ScopeKey,
        p.ContentHash,
        p.AttachmentHash,
        p.ReplyToEmail ?? string.Empty,
        p.ExpiresAt.ToString("O", CultureInfo.InvariantCulture));

    private static EmailPreviewTokenPayload? Deserialize(string body)
    {
        var parts = body.Split(PayloadSeparator);
        if (parts.Length != 10) return null;
        if (parts[0] != Version) return null;

        if (!ulong.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var actor))
            return null;
        if (!uint.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var revision))
            return null;
        if (!DateTime.TryParse(
                parts[9], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires))
            return null;

        return new EmailPreviewTokenPayload(
            Purpose: parts[1],
            ActorUserId: actor,
            TemplateCode: parts[3],
            TemplateRevision: revision,
            ScopeKey: parts[5],
            ContentHash: parts[6],
            AttachmentHash: parts[7],
            ReplyToEmail: string.IsNullOrEmpty(parts[8]) ? null : parts[8],
            ExpiresAt: expires);
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
