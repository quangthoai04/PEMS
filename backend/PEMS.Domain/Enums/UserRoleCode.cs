namespace PEMS.Shared;

// Maps roles.role_code (SQL v8.3): the 6 system roles.
public static class UserRoleCode
{
    public const string Admin = "ADMIN";
    public const string Ho = "HO";
    public const string Staff = "STAFF";
    public const string Dept = "DEPT";
    public const string Student = "STUDENT";
    public const string Visitor = "VISITOR";
}
