namespace PEMS.Application.Departments.Common;

/// <summary>
/// Stable, machine-readable error codes for the Staff Leader Department Management UCs
/// (UC-101 Add, UC-102 Update, UC-103 Search/Filter, UC-104 List, UC-105 Details).
/// Surfaced via the controlled exceptions so the frontend can map them to localized messages.
/// </summary>
public static class DepartmentErrorCodes
{
    /// <summary>Caller is not an active Staff Leader (STAFF/LEADER + campus). → 403.</summary>
    public const string DepartmentManagementForbidden = "DEPARTMENT_MANAGEMENT_FORBIDDEN";

    /// <summary>The Staff Leader has no primary campus assigned. → 422.</summary>
    public const string NoCampusAssigned = "DEPARTMENT_NO_CAMPUS_ASSIGNED";

    /// <summary>The caller's campus no longer exists. → 422.</summary>
    public const string CampusNotFound = "DEPARTMENT_CAMPUS_NOT_FOUND";

    /// <summary>The caller's campus is not ACTIVE. → 422.</summary>
    public const string CampusInactive = "DEPARTMENT_CAMPUS_INACTIVE";

    /// <summary>A department with the same name already exists in this campus. → 409.</summary>
    public const string DepartmentNameAlreadyExists = "DEPARTMENT_NAME_ALREADY_EXISTS";

    /// <summary>The target department does not exist. → 404.</summary>
    public const string DepartmentNotFound = "DEPARTMENT_NOT_FOUND";

    /// <summary>Tried to toggle an IC (default) department's status — not allowed. → 403.</summary>
    public const string DepartmentTypeNotManageable = "DEPARTMENT_TYPE_NOT_MANAGEABLE";

    /// <summary>The target department exists but belongs to another campus (UC-105/UC-102). → 403.</summary>
    public const string DepartmentScopeForbidden = "DEPARTMENT_SCOPE_FORBIDDEN";

    /// <summary>Tried to edit the default IC department's name (UC-102). → 409.</summary>
    public const string DepartmentIcNotEditable = "DEPARTMENT_IC_NOT_EDITABLE";
}
