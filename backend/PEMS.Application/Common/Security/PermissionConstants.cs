namespace PEMS.Application.Common.Security;

/// <summary>
/// Permission codes (UC based) used across the system. Values MUST match the
/// <c>permissions.permission_code</c> column seeded by
/// <c>database/scripts/pems_full.sql</c> — the single source of truth that the
/// backend returns in AuthResponse.permissions / GET /auth/me.
/// Canonical format: <c>UC-NN.NAME</c> where NN is 2 digits for 1–99 (UC-01..UC-99)
/// and 3 digits for 100+ (UC-100..UC-135). Never 3-digit-pad a 2-digit number
/// (e.g. NOT "UC-095"). Do not invent codes.
/// </summary>
public static class PermissionCodes
{
    // ── Authentication ────────────────────────────────────────────────────
    public const string LoginViaSso = "UC-10.LOGIN_VIA_SSO";
    public const string LoginViaCredentials = "UC-11.LOGIN_VIA_CREDENTIALS";
    public const string Logout = "UC-12.LOGOUT";
    public const string ForgotPassword = "UC-13.FORGOT_PASSWORD";

    // ── Profile ───────────────────────────────────────────────────────────
    public const string ViewProfile = "UC-14.VIEW_PROFILE";
    public const string UpdateProfile = "UC-15.UPDATE_PROFILE";
    public const string ChangePassword = "UC-16.CHANGE_PASSWORD";

    // ── Account Management ────────────────────────────────────────────────
    public const string ViewAccountList = "UC-95.VIEW_ACCOUNT_LIST";
    public const string CreateAccount = "UC-96.CREATE_ACCOUNT";
    public const string ManageAccountStatus = "UC-97.MANAGE_ACCOUNT_STATUS";
    public const string ViewAccountDetails = "UC-98.VIEW_ACCOUNT_DETAILS";
    public const string SearchAndFilterAccounts = "UC-99.SEARCH_AND_FILTER_ACCOUNTS";
    public const string UpdateAccountRole = "UC-100.UPDATE_ACCOUNT_ROLE";

    // ── Email Management ──────────────────────────────────────────────────
    public const string EditEmailContent = "UC-46.EDIT_EMAIL_CONTENT";
    public const string SendEmail = "UC-47.SEND_EMAIL";
    public const string ViewEmail = "UC-48.VIEW_EMAIL";
    public const string ReplyToEmail = "UC-49.REPLY_TO_EMAIL";

    // ── Role Management ───────────────────────────────────────────────────
    public const string ViewRoleList = "UC-117.VIEW_ROLE_LIST";
    public const string CreateNewRole = "UC-118.CREATE_NEW_ROLE";
    public const string ConfigureRolePermissions = "UC-119.CONFIGURE_ROLE_PERMISSIONS";
    public const string UpdateRoleDetails = "UC-120.UPDATE_ROLE_DETAILS";
    public const string DisableDeleteRole = "UC-121.DISABLE_DELETE_ROLE";
}
