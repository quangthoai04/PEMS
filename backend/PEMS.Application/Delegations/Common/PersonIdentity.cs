namespace PEMS.Application.Delegations.Common;

/// <summary>
/// The ONE way PEMS decides whether two person records describe the same human (NP-03).
///
/// <para>
/// A visit collects the same people through several doors — the delegation member list, the campus's
/// operational contact, the internal participant list, the biên bản. Each door stores a different set
/// of columns, and only some of them carry an id. Whenever two of those lists have to be merged, the
/// question "is this the same person?" comes up, and it must be answered the same way every time:
/// two lists de-duplicated by two different rules produce a biên bản that contains somebody twice and
/// a headcount nobody can reconcile.
/// </para>
///
/// <para><b>The priority, strongest evidence first</b> (callers apply the ones they have):</para>
/// <list type="number">
///   <item>Same <c>guest_member_id</c>, or same <c>user_id</c> — an id is proof. Nothing overrides it.</item>
///   <item>Same normalised email (or phone) — strong, and the only cross-list evidence available when
///   one side has an account and the other is a typed snapshot.</item>
///   <item>Same <see cref="Key"/> (name + role + organisation) — a HINT. Good enough to skip a
///   duplicate auto-fill row; never good enough to merge two records the user can see.</item>
/// </list>
///
/// <para>
/// Name alone is deliberately NOT on that list. Two members of one delegation sharing a name is
/// ordinary — that is exactly why the key carries role and organisation as well, and why an entry with
/// no name matches nothing at all rather than matching everything with no name.
/// </para>
/// </summary>
public static class PersonIdentity
{
    /// <summary>
    /// The identity fingerprint two records must share to count as the same person by rule 3: full
    /// name, role and organisation, each trimmed, lower-cased and with runs of whitespace collapsed.
    ///
    /// <para>
    /// Returns an empty string when there is no name to match on — an unnamed row is never merged with
    /// anything, so callers can treat "" as "no opinion".
    /// </para>
    /// <para>
    /// Accents are deliberately left alone: stripping them would make "Nguyễn Văn An" and "Nguyen Van
    /// Ân" the same person, and in a system full of Vietnamese names that is a merge waiting to happen.
    /// </para>
    /// </summary>
    public static string Key(string? fullName, string? role, string? organization)
    {
        var name = Normalize(fullName);
        if (name.Length == 0) return string.Empty;
        return $"{name}|{Normalize(role)}|{Normalize(organization)}";
    }

    /// <summary>Trimmed, lower-cased, inner whitespace collapsed. Empty for null/blank.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var sb = new System.Text.StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value.Trim())
        {
            if (char.IsWhiteSpace(ch)) { pendingSpace = sb.Length > 0; continue; }
            if (pendingSpace) { sb.Append(' '); pendingSpace = false; }
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Rule 2 for email addresses: trimmed and lower-cased, so a snapshot typed as " An@Example.COM "
    /// matches the account that owns <c>an@example.com</c>. Empty for null/blank — never a match.
    /// </summary>
    public static string NormalizeEmail(string? email) => Normalize(email);
}
