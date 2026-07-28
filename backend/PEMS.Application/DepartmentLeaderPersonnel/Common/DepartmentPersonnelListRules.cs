using System.Collections.Generic;
using PEMS.Domain.Constants;

namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>
/// Whitelists and normalization for the personnel list query (spec §9), shared by the validator and the
/// handler so the two cannot drift. Keeping the sort whitelist here is also what keeps the ordering
/// free of client-controlled column names.
/// </summary>
public static class DepartmentPersonnelListRules
{
    public const int MaxKeywordLength = 100;
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 10;

    /// <summary>The "no filter" sentinel. Every real status is a first-class filter of its own.</summary>
    public const string StatusFilterAll = "ALL";

    /// <summary>
    /// Status filter values. INACTIVE and LOCKED are deliberately separate — collapsing them would hide
    /// a security lock behind an ordinary deactivation (spec §9).
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedStatusFilters = new HashSet<string>
    {
        StatusFilterAll,
        UserStatuses.Active,
        UserStatuses.Inactive,
        UserStatuses.PendingEmailConfirmation,
        UserStatuses.Locked,
    };

    public static readonly IReadOnlySet<string> AllowedSortColumns = new HashSet<string>
    {
        "fullname", "email", "status", "createdat",
    };

    public static bool IsSupportedStatusFilter(string? status)
        => string.IsNullOrWhiteSpace(status)
           || AllowedStatusFilters.Contains(status.Trim().ToUpperInvariant());

    /// <summary>Normalized status filter, or null when the caller wants every status.</summary>
    public static string? NormalizeStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var normalized = status.Trim().ToUpperInvariant();
        return normalized == StatusFilterAll ? null : normalized;
    }

    /// <summary>Trimmed, length-capped, lower-cased keyword, or null when the caller sent nothing usable.</summary>
    public static string? NormalizeKeyword(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return null;
        var trimmed = keyword.Trim();
        if (trimmed.Length > MaxKeywordLength) trimmed = trimmed[..MaxKeywordLength];
        return trimmed.ToLowerInvariant();
    }

    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    public static int NormalizePageSize(int pageSize)
        => pageSize < 1 ? DefaultPageSize : (pageSize > MaxPageSize ? MaxPageSize : pageSize);
}
