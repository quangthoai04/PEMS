using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PEMS.Application.Delegations.Commands;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Services;

/// <summary>
/// Server-side, pure, deterministic builder of the UC-17 <c>business_fingerprint</c> (v1).
/// The client never decides the fingerprint. Two submit intents with the same core visit
/// identity produce the same hash; changing any core field produces a different one.
///
/// v1 components (in order): normalized registrant email, normalized EFFECTIVE contact
/// email, normalized delegation name, effective visit scope, visit type, normalized
/// visitTypeOther (only when OTHER, else empty), sorted campus schedules
/// (campus code + start + end, wall-clock UTC+7 to the minute).
///
/// Deliberately EXCLUDED (soft content, not identity): purpose, working content, notes,
/// transportation note, visitors, support team, media consent, partnerId.
///
/// Only the SHA-256 hex digest is ever persisted — the canonical input string contains
/// PII and must not be stored or logged.
/// </summary>
public static class VisitRequestFingerprintBuilder
{
    public const string Version = "v1";

    /// <summary>Builds the v1 fingerprint from already-extracted core fields.</summary>
    public static string Build(
        string registrantEmail,
        string effectiveContactEmail,
        string delegationName,
        string effectiveVisitScope,
        string visitType,
        string? visitTypeOther,
        IEnumerable<(string CampusCode, DateTime Start, DateTime End)> campusVisits)
    {
        var type = NormalizeCode(visitType);
        var other = type == "OTHER" ? NormalizeText(visitTypeOther) : string.Empty;

        var slots = campusVisits
            .Select(s => $"{NormalizeCode(s.CampusCode)},{FormatWallClock(s.Start)},{FormatWallClock(s.End)}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var canonical = string.Join('|', new[]
        {
            Version,
            NormalizeEmail(registrantEmail),
            NormalizeEmail(effectiveContactEmail),
            NormalizeText(delegationName),
            NormalizeCode(effectiveVisitScope),
            type,
            other,
            string.Join(';', slots)
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public const string VersionV2 = "v2";

    /// <summary>
    /// Per-campus form v2 fingerprint (plan §6.3). Canonical input, in order: version tag, normalized
    /// registrant email, effective scope, then each campus (SORTED by campus code) as
    /// <c>code,start,end,delegationName,visitType,visitTypeOther,operationalContactEmail</c>.
    /// Delegation name and visit type are PER-CAMPUS in v2 (they are core identity here);
    /// purpose/notes/people are still excluded as soft content.
    ///
    /// <para>
    /// The contact address moved INTO the campus tuple with the per-campus cutover. It has to be part
    /// of the identity: the address decides who is asked to confirm that campus, so a resend that
    /// silently changed it would otherwise reuse the fingerprint of the form the registrant first
    /// submitted and look like the same intent.
    /// </para>
    ///
    /// The version tag guarantees a v2 hash can never collide with a v1 hash for the same core data.
    /// Only the SHA-256 hex digest is persisted — the canonical string carries PII and must not be
    /// stored or logged.
    /// </summary>
    public static string BuildV2(
        string registrantEmail,
        string effectiveVisitScope,
        IEnumerable<(string CampusCode, DateTime Start, DateTime End, string DelegationName, string VisitType, string? VisitTypeOther, string? OperationalContactEmail)> campusVisits)
    {
        var campuses = campusVisits
            .Select(c =>
            {
                var type = NormalizeCode(c.VisitType);
                var other = type == "OTHER" ? NormalizeText(c.VisitTypeOther) : string.Empty;
                return string.Join(',', new[]
                {
                    NormalizeCode(c.CampusCode),
                    FormatWallClock(c.Start),
                    FormatWallClock(c.End),
                    NormalizeText(c.DelegationName),
                    type,
                    other,
                    NormalizeEmail(c.OperationalContactEmail),
                });
            })
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var canonical = string.Join('|', new[]
        {
            VersionV2,
            NormalizeEmail(registrantEmail),
            NormalizeCode(effectiveVisitScope),
            string.Join(';', campuses),
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Trim + lowercase (invariant). Emails are compared case-insensitively.</summary>
    public static string NormalizeEmail(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Masks the local part keeping first/last char: "abcd@x.com" → "a**d@x.com"; "ab@x.com" → "a*@x.com".
    /// The masked form is what audit/events keep after redaction — never the full email.
    /// </summary>
    public static string MaskEmail(string normalizedEmail)
    {
        var at = normalizedEmail.IndexOf('@');
        if (at <= 0) return "***";
        var local = normalizedEmail[..at];
        var domain = normalizedEmail[at..];
        if (local.Length <= 2) return $"{local[0]}*{domain}";
        return $"{local[0]}{new string('*', local.Length - 2)}{local[^1]}{domain}";
    }

    /// <summary>
    /// Unicode NFC + trim + collapse internal whitespace + lowercase (invariant).
    /// Vietnamese diacritics are preserved — "Đoàn ABC" and "Doan ABC" are different names.
    /// </summary>
    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var nfc = value.Normalize(NormalizationForm.FormC).Trim();
        var collapsed = string.Join(' ', nfc.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.ToLowerInvariant();
    }

    /// <summary>Trim + uppercase (invariant) for enum-like codes (visit type, scope, campus).</summary>
    public static string NormalizeCode(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// Canonical wall-clock (UTC+7) to the minute, exactly as the user entered it — PEMS
    /// datetimes are stored/transported as local wall-clock, so NO timezone conversion here
    /// (converting would shift the date/time the UI showed).
    /// </summary>
    public static string FormatWallClock(DateTime value)
        => value.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
}
