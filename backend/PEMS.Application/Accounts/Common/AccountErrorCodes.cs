namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Stable, machine-readable error codes for the Account Management UCs (UC-95..UC-100).
/// Surfaced via <see cref="PEMS.Application.Common.Exceptions.AuthBusinessException"/> so the
/// frontend can map them to localized messages. Keep in sync with the frontend
/// account-management error map.
/// </summary>
public static class AccountErrorCodes
{
    /// <summary>Authenticated but lacks the Account-List/Search permission. → 403.</summary>
    public const string AccountListForbidden = "ACCOUNT_LIST_FORBIDDEN";

    /// <summary>Tried to read accounts of a campus outside the caller's scope. → 403.</summary>
    public const string CampusScopeForbidden = "CAMPUS_SCOPE_FORBIDDEN";

    /// <summary>sortBy was not in the allowed whitelist. → 400.</summary>
    public const string UnsupportedSortColumn = "UNSUPPORTED_SORT_COLUMN";

    /// <summary>A filter value was structurally invalid. → 400.</summary>
    public const string InvalidAccountFilter = "INVALID_ACCOUNT_FILTER";
}
