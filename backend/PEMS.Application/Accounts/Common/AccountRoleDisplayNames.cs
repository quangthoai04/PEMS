using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Human-readable role labels for account emails. Account handlers previously each carried their own
/// private copy of this switch, and the copies had drifted apart (one knew DEPARTMENT/STUDENT, the
/// other did not), so the same account could be described differently depending on which email sent
/// it. This is the single mapping — extend it here, not at the call site.
/// </summary>
public static class AccountRoleDisplayNames
{
    /// <summary>
    /// Maps a role code + sub-role to the label shown to the account holder. Unknown codes fall back
    /// to the raw code so a new role never renders as an empty string.
    /// </summary>
    public static string Resolve(string roleCode, string? subRole) => roleCode switch
    {
        RoleCodes.Ho => "Head Office",
        RoleCodes.Admin => "System Administrator",
        RoleCodes.Staff when subRole == UserSubRoles.Leader => "Staff Leader — Trưởng phòng IC",
        RoleCodes.Staff => "IC Staff",
        RoleCodes.Department when subRole == UserSubRoles.Leader => "Department Leader — Trưởng phòng ban",
        RoleCodes.Department => "Department Staff",
        RoleCodes.Student => "Student",
        RoleCodes.Visitor => "Visitor",
        _ => roleCode
    };
}
