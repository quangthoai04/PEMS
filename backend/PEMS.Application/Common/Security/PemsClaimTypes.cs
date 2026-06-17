namespace PEMS.Application.Common.Security;

/// <summary>Custom JWT claim names used by PEMS access tokens.</summary>
public static class PemsClaimTypes
{
    public const string UserId = "uid";
    public const string Email = "email";
    public const string RoleCode = "role_code";
    public const string RoleId = "role_id";
    public const string SubRole = "sub_role";
    public const string PrimaryCampusId = "primary_campus_id";
    public const string DepartmentId = "department_id";
    public const string SessionId = "session_id";
    public const string LoginPortal = "login_portal";
}
