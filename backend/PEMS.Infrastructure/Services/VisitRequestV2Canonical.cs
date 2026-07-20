using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Shared backend derivations for per-campus form v2 (create, pending-edit, resubmit). These are the values a
/// client must NEVER send — they are always recomputed here from the full, normalized per-campus snapshot:
/// <list type="bullet">
///   <item><c>visit_scope</c> — SINGLE vs MULTI from the campus count;</item>
///   <item><c>has_mixed_campus_details</c> — whether any campus differs in normalized COPYABLE content OR member
///         set (ignores campus_id and schedule);</item>
///   <item>the v2 canonical business fingerprint (version-tagged);</item>
///   <item>the smallest-<c>campus_id</c> compatibility projection (transition only).</item>
/// </list>
/// The single source of truth so create and every edit stay byte-for-byte consistent.
/// </summary>
public static class VisitRequestV2Canonical
{
    public static string ScopeOf(int campusCount)
        => campusCount > 1 ? VisitScopes.MultiCampus : VisitScopes.SingleCampus;

    /// <summary>
    /// Scope from the DISTINCT campuses actually being visited — the only correct basis, and
    /// independent of how many entries the payload happens to contain. The validator already
    /// rejects duplicates, so this normally equals the entry count; deriving it from the distinct
    /// set means a request can never be recorded as MULTI_CAMPUS because the same campus appeared
    /// twice, regardless of validation order. Client-sent scope is never consulted.
    /// </summary>
    public static string ScopeOf(IEnumerable<CampusVisitFormDto> campuses)
        => ScopeOf(campuses
            .Select(c => c.CampusId?.Trim().ToUpperInvariant() ?? string.Empty)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count());

    /// <summary>has_mixed = any campus differs in normalized copyable form content OR member set (ignores campus/time).</summary>
    public static bool ComputeHasMixed(IList<CampusVisitFormDto> campuses)
    {
        if (campuses.Count <= 1) return false;
        var first = CanonicalContent(campuses[0]);
        for (var i = 1; i < campuses.Count; i++)
            if (CanonicalContent(campuses[i]) != first) return true;
        return false;
    }

    public static string CanonicalContent(CampusVisitFormDto cv)
    {
        string N(string? s) => VisitRequestFingerprintBuilder.NormalizeText(s);
        string C(string? s) => VisitRequestFingerprintBuilder.NormalizeCode(s);
        string E(string? s) => VisitRequestFingerprintBuilder.NormalizeEmail(s);
        // Members are order-independent: sort the normalized tuples.
        static string People(IEnumerable<string> xs) => string.Join(';', xs.OrderBy(x => x, StringComparer.Ordinal));
        var visitors = People(cv.Visitors.Select(v => $"{N(v.FullName)}|{N(v.Organization)}|{N(v.JobTitle)}|{C(v.Nationality)}"));
        var support = People(cv.ExternalSupportMembers.Select(m => $"{N(m.FullName)}|{N(m.Organization)}|{N(m.JobTitle)}|{C(m.Nationality)}"));
        return string.Join('#', new[]
        {
            N(cv.DelegationName), C(cv.VisitType), N(cv.VisitTypeOther), N(cv.Purpose), N(cv.WorkingContent),
            N(cv.OperationalContact.FullName), N(cv.OperationalContact.Organization), C(cv.OperationalContact.Phone), E(cv.OperationalContact.Email),
            C(cv.WorkingLanguage), N(cv.TransportationNote), C(cv.MediaConsentStatus), N(cv.MediaConsentNote), N(cv.Notes),
            $"V[{visitors}]", $"S[{support}]",
        });
    }

    /// <summary>Version-tagged canonical fingerprint over registrant/contact email, scope and per-campus core identity.</summary>
    public static string BuildFingerprint(
        string registrantEmailNorm, string contactEmailNorm, string scope, IEnumerable<CampusVisitFormDto> campuses)
        => VisitRequestFingerprintBuilder.BuildV2(
            registrantEmailNorm, contactEmailNorm, scope,
            campuses.Select(cv => (cv.CampusId, cv.PlannedStartAt, cv.PlannedEndAt, cv.DelegationName, cv.VisitType, cv.VisitTypeOther)));
}
