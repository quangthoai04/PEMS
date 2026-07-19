using System.Collections.Generic;
using System.Linq;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

/// <summary>
/// Builds the scope-safe <see cref="SearchMatchContextDto"/> list for ONE result row, in memory, AFTER
/// the SQL has already applied authorization scope + keyword + count + order + pagination. The caller
/// passes ONLY the fields+campuses this actor is authorized to see, so a hidden campus can never appear;
/// this helper adds no filtering of its own beyond the keyword match and therefore cannot change the row's
/// hit/count/order (those were decided by SQL).
///
/// The match is a case-insensitive substring, mirroring the list query's <c>LOWER(x).Contains(kw)</c>.
/// Only stable field CODES are emitted — never the raw matched value (no PII/snippet leak).
/// </summary>
public static class VisitSearchMatchContextBuilder
{
    /// <summary>A searchable field: its stable code and the value to test against the keyword.</summary>
    public readonly record struct Field(string Code, string? Value);

    /// <summary>An authorized campus and the campus-scoped fields to test for it.</summary>
    public readonly record struct CampusScope(
        ulong VisitInstanceId, ulong CampusId, string? CampusName, IReadOnlyList<Field> Fields);

    /// <summary>
    /// Returns null when the keyword is blank (no search). Otherwise returns the request-level context
    /// (if any request-wide field matched) followed by one campus-level context per authorized campus that
    /// matched. An empty list means the row matched on nothing visible to this caller (the caller decides
    /// whether that is expected — it should not happen for a row the SQL keyword already matched).
    /// </summary>
    public static List<SearchMatchContextDto>? Build(
        string? keyword, IReadOnlyList<Field> requestFields, IReadOnlyList<CampusScope> campuses)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return null;
        var kw = keyword.Trim().ToLowerInvariant();

        var contexts = new List<SearchMatchContextDto>();

        var requestMatched = requestFields
            .Where(f => Matches(f.Value, kw))
            .Select(f => f.Code)
            .Distinct()
            .ToList();
        if (requestMatched.Count > 0)
            contexts.Add(new SearchMatchContextDto
            {
                Scope = SearchMatchScopes.Request,
                MatchedFields = requestMatched,
            });

        foreach (var campus in campuses)
        {
            var campusMatched = campus.Fields
                .Where(f => Matches(f.Value, kw))
                .Select(f => f.Code)
                .Distinct()
                .ToList();
            if (campusMatched.Count > 0)
                contexts.Add(new SearchMatchContextDto
                {
                    Scope = SearchMatchScopes.Campus,
                    VisitInstanceId = campus.VisitInstanceId,
                    CampusId = campus.CampusId,
                    CampusName = campus.CampusName,
                    MatchedFields = campusMatched,
                });
        }

        return contexts;
    }

    private static bool Matches(string? value, string loweredKeyword)
        => !string.IsNullOrEmpty(value) && value.ToLowerInvariant().Contains(loweredKeyword);
}
