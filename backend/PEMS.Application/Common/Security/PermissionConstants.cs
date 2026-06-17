namespace PEMS.Application.Common.Security;

/// <summary>
/// Permission codes (UC based) used across the system. Values match the
/// <c>permissions.permission_code</c> column seeded into the database.
/// </summary>
public static class PermissionCodes
{
    // ── Authentication ────────────────────────────────────────────────────
    public const string LoginViaSso = "UC-10.LOGIN_VIA_SSO";
    public const string LoginViaCredentials = "UC-11.LOGIN_VIA_CREDENTIALS";
    public const string Logout = "UC-012.LOGOUT";
    public const string ForgotPassword = "UC-013.FORGOT_PASSWORD";

    // ── Profile ───────────────────────────────────────────────────────────
    public const string ViewProfile = "UC-014.VIEW_PROFILE";
    public const string UpdateProfile = "UC-015.UPDATE_PROFILE";
    public const string ChangePassword = "UC-016.CHANGE_PASSWORD";

    // ── Account Management ────────────────────────────────────────────────
    public const string ViewAccountList = "UC-095.VIEW_ACCOUNT_LIST";
    public const string CreateAccount = "UC-096.CREATE_ACCOUNT";
    public const string ManageAccountStatus = "UC-097.MANAGE_ACCOUNT_STATUS";
    public const string ViewAccountDetails = "UC-98.VIEW_ACCOUNT_DETAILS";
    public const string SearchAndFilterAccounts = "UC-99.SEARCH_AND_FILTER_ACCOUNTS";
    public const string UpdateAccountRole = "UC-100.UPDATE_ACCOUNT_ROLE";

    // ── Role Management ───────────────────────────────────────────────────
    public const string ViewRoleList = "UC-117.VIEW_ROLE_LIST";
    public const string CreateNewRole = "UC-118.CREATE_NEW_ROLE";
    public const string ConfigureRolePermissions = "UC-119.CONFIGURE_ROLE_PERMISSIONS";
    public const string UpdateRoleDetails = "UC-120.UPDATE_ROLE_DETAILS";
    public const string DisableDeleteRole = "UC-121.DISABLE_DELETE_ROLE";
}
