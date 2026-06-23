using System;

namespace PEMS.Application.Common.Security;

public static class EffectiveRole
{
    public const string Admin = "ADMIN";
    public const string Ho = "HO";
    public const string StaffLeader = "STAFF_LEADER";
    public const string Staff = "STAFF";
    public const string DepartmentLead = "DEPARTMENT_LEAD";
    public const string Department = "DEPARTMENT";
    public const string Student = "STUDENT";
    public const string Visitor = "VISITOR";

    public static string Resolve(string roleCode, string? subRole)
    {
        var sr = string.IsNullOrWhiteSpace(subRole) || subRole.Equals(SubRole.None, StringComparison.OrdinalIgnoreCase) 
            ? SubRole.None 
            : subRole.ToUpperInvariant();

        return (roleCode.ToUpperInvariant(), sr) switch
        {
            (RoleCode.Admin, SubRole.None) => Admin,
            (RoleCode.Ho, SubRole.None) => Ho,
            (RoleCode.Staff, SubRole.Leader) => StaffLeader,
            (RoleCode.Staff, SubRole.Staff) => Staff,
            (RoleCode.Department, SubRole.Leader) => DepartmentLead,
            (RoleCode.Department, SubRole.Staff) => Department,
            (RoleCode.Student, SubRole.None) => Student,
            (RoleCode.Visitor, SubRole.None) => Visitor,
            _ => throw new InvalidOperationException("Invalid Role/SubRole combination.")
        };
    }
}
