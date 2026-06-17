using PEMS.Domain.Constants;

namespace PEMS.Infrastructure.Persistence.Seed;

/// <summary>
/// Definition of a development/test account. Department/campus are resolved by
/// code at seed time. STAFF/DEPT must carry a sub-role + department (DB triggers).
/// </summary>
public sealed record DevAccountDefinition(
    string FullName,
    string Email,
    string Password,
    string RoleCode,
    string? SubRole,
    string CampusCode,
    string? DepartmentCode);

/// <summary>
/// Test accounts seeded only in Development (or when <c>Seed:DevAccounts</c> is
/// enabled). All passwords are hashed with BCrypt at seed time — never stored plain.
/// </summary>
public static class DevAccountSeed
{
    public const string DefaultPassword = "Admin@123";

    public static readonly IReadOnlyList<DevAccountDefinition> All = new List<DevAccountDefinition>
    {
        new("System Administrator", "admin@fpt.edu.vn", DefaultPassword, RoleCodes.Admin, null, "HN", null),
        new("Head Office Manager", "ho@fpt.edu.vn", DefaultPassword, RoleCodes.Ho, null, "HN", null),
        new("IC Staff Leader (HN)", "staff.leader.hn@fpt.edu.vn", DefaultPassword, RoleCodes.Staff, SubRoles.Leader, "HN", "IC"),
        new("IC Staff (HN)", "staff.hn@fpt.edu.vn", DefaultPassword, RoleCodes.Staff, SubRoles.Staff, "HN", "IC"),
        new("Department Lead (HN)", "dept.leader.hn@fpt.edu.vn", DefaultPassword, RoleCodes.Dept, SubRoles.Leader, "HN", "ACADEMIC"),
        new("Department Personnel (HN)", "dept.hn@fpt.edu.vn", DefaultPassword, RoleCodes.Dept, SubRoles.Staff, "HN", "ACADEMIC"),
        new("Support Student", "student@fpt.edu.vn", DefaultPassword, RoleCodes.Student, null, "HN", null),
        new("External Visitor", "visitor@example.com", DefaultPassword, RoleCodes.Visitor, null, "HN", null),
    };
}
