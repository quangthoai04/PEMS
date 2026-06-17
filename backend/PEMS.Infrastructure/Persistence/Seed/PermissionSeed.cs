using PEMS.Application.Common.Security;

namespace PEMS.Infrastructure.Persistence.Seed;

public sealed record PermissionDefinition(string Code, string Name, string Group, bool IsSystem);

/// <summary>
/// Canonical list of permissions seeded into the <c>permissions</c> table.
/// Mirrors the codes in <see cref="PermissionCodes"/>.
/// </summary>
public static class PermissionSeed
{
    public const string GroupAuthentication = "Authentication";
    public const string GroupProfile = "Profile";
    public const string GroupAccountManagement = "Account Management";
    public const string GroupRoleManagement = "Role Management";

    public static readonly IReadOnlyList<PermissionDefinition> All = new List<PermissionDefinition>
    {
        // Authentication
        new(PermissionCodes.LoginSso, "Login via SSO", GroupAuthentication, true),
        new(PermissionCodes.LoginCredentials, "Login via Credentials", GroupAuthentication, true),
        new(PermissionCodes.Logout, "Logout", GroupAuthentication, true),
        new(PermissionCodes.ForgotPassword, "Forgot Password", GroupAuthentication, true),

        // Profile
        new(PermissionCodes.ViewProfile, "View Profile", GroupProfile, true),
        new(PermissionCodes.UpdateProfile, "Update Profile", GroupProfile, true),
        new(PermissionCodes.ChangePassword, "Change Password", GroupProfile, true),

        // Account Management
        new(PermissionCodes.ViewAccountList, "View Account List", GroupAccountManagement, true),
        new(PermissionCodes.CreateAccount, "Create Account", GroupAccountManagement, true),
        new(PermissionCodes.ManageAccountStatus, "Manage Account Status", GroupAccountManagement, true),
        new(PermissionCodes.ViewAccountDetails, "View Account Details", GroupAccountManagement, true),
        new(PermissionCodes.SearchFilterAccounts, "Search / Filter Accounts", GroupAccountManagement, true),
        new(PermissionCodes.UpdateAccountRole, "Update Account Role", GroupAccountManagement, true),

        // Role Management
        new(PermissionCodes.ViewRoleList, "View Role List", GroupRoleManagement, true),
        new(PermissionCodes.CreateRole, "Create Role", GroupRoleManagement, true),
        new(PermissionCodes.ConfigureRolePermissions, "Configure Role Permissions", GroupRoleManagement, true),
        new(PermissionCodes.UpdateRoleDetails, "Update Role Details", GroupRoleManagement, true),
        new(PermissionCodes.DisableDeleteRole, "Disable / Delete Role", GroupRoleManagement, true),
    };
}
