using System.Collections.Generic;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// A multiset of one campus's CURRENT member rows, keyed by FULL normalized content — member type,
/// name, organization, job title and EFFECTIVE nationality (the country it resolves to, or its own
/// normalized text when it does not resolve to any real country). Used to recognize an incoming
/// member row whose content already exists on the campus, untouched, so its — possibly unresolvable,
/// legacy — nationality can pass straight through instead of being forced through
/// <see cref="NationalityResolution"/> (Patch 4 hardening H4-3/H4-4).
///
/// <para>
/// This is content equality, never identity. Two members are "the same" here only when EVERY field
/// matches exactly, after the same normalization <see cref="VisitRequestV2Canonical.CanonicalContent"/>
/// already fingerprints a campus with — never by name alone, never by list position, and never by a
/// client-minted key (<c>ClientMemberKey</c> is a per-submission session tag, freshly minted every time
/// a form opens and never persisted — see <c>OperationalContactLink</c>'s own doc comment). There is no
/// durable per-member identity anywhere in this schema to correlate against, and this class does not
/// invent one: a row whose content differs from every existing row, in ANY field, is new-or-changed
/// content and must resolve like a fresh create. A campus that genuinely has an unresolvable legacy
/// member survives edits to everything ELSE about that campus; the moment that specific member's own
/// content moves, it is rewritten like new and must name a real country.
/// </para>
/// </summary>
internal sealed class MemberContentIndex
{
    // Unit-separator (0x1F, built numerically rather than as a literal — it is not typeable directly)
    // never appears in normal typed text, so joining normalized fields with it cannot let two different
    // tuples collapse onto the same key the way a plain concatenation or a common delimiter like '|'
    // could (e.g. name="ab"+org="c" vs name="a"+org="bc").
    private static readonly char KeyDelimiter = (char)0x1F;

    private readonly Dictionary<string, Queue<string?>> _byKey = new();

    public static MemberContentIndex Build(IEnumerable<VisitGuestMember> current)
    {
        var index = new MemberContentIndex();
        foreach (var m in current)
        {
            var key = KeyOf(m.MemberType, m.FullName, m.Organization, m.JobTitle, m.Nationality);
            if (!index._byKey.TryGetValue(key, out var queue))
                index._byKey[key] = queue = new Queue<string?>();
            queue.Enqueue(m.Nationality);
        }
        return index;
    }

    /// <summary>
    /// True when a not-yet-consumed current row has EXACTLY this content (member type, name,
    /// organization, job title, and nationality in canonical space). <paramref name="storedNationality"/>
    /// is that row's own stored nationality text, verbatim — consumed on match so it cannot excuse a
    /// second, different incoming row that merely happens to share the same content.
    /// </summary>
    public bool TryTakeMatch(
        string memberType, string? fullName, string? organization, string? jobTitle, string? nationality,
        out string? storedNationality)
    {
        var key = KeyOf(memberType, fullName, organization, jobTitle, nationality);
        if (_byKey.TryGetValue(key, out var queue) && queue.Count > 0)
        {
            storedNationality = queue.Dequeue();
            return true;
        }
        storedNationality = null;
        return false;
    }

    private static string KeyOf(
        string? memberType, string? fullName, string? organization, string? jobTitle, string? nationality)
    {
        string N(string? s) => VisitRequestFingerprintBuilder.NormalizeText(s);
        var effectiveNationality = CountryName.TryResolve(nationality, out var canonical)
            ? canonical!
            : (nationality ?? string.Empty).Trim();
        return string.Join(
            KeyDelimiter,
            VisitRequestFingerprintBuilder.NormalizeCode(memberType), N(fullName), N(organization), N(jobTitle),
            N(effectiveNationality));
    }
}
