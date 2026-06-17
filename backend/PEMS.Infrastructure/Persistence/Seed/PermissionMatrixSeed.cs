using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Infrastructure.Persistence.Seed;

public sealed record RolePermissionGrant(string RoleCode, string PermissionCode, string Level);

/// <summary>
/// The initial role → permission matrix. Conservative by design: every role gets
/// "Own" (O) on the authentication/profile use cases; only ADMIN gets full account
/// and role management; HO and STAFF get read-only account visibility (the actual
/// data scope is still enforced per request in the backend handlers).
/// </summary>
public static class PermissionMatrixSeed
{
    private static readonly string[] AllRoles =
    {
        RoleCodes.Admin, RoleCodes.Ho, RoleCodes.Staff,
        RoleCodes.Dept, RoleCodes.Student, RoleCodes.Visitor
    };

    private static readonly string[] AuthProfilePermissions =
    {
        PermissionCodes.LoginSso,
        PermissionCodes.LoginCredentials,
        PermissionCodes.Logout,
        PermissionCodes.ForgotPassword,
        PermissionCodes.ViewProfile,
        PermissionCodes.UpdateProfile,
        PermissionCodes.ChangePassword,
    };

    public static IReadOnlyList<RolePermissionGrant> Build()
    {
        var grants = new List<RolePermissionGrant>();

        // 1) Every role can act on its own authentication / profile (Own scope).
        foreach (var role in AllRoles)
            foreach (var perm in AuthProfilePermissions)
                grants.Add(new RolePermissionGrant(role, perm, PermissionLevels.Own));

        // 2) ADMIN — full account & role management.
        string[] adminFull =
        {
            PermissionCodes.ViewAccountList, PermissionCodes.CreateAccount, PermissionCodes.ManageAccountStatus,
            PermissionCodes.ViewAccountDetails, PermissionCodes.SearchFilterAccounts, PermissionCodes.UpdateAccountRole,
            PermissionCodes.ViewRoleList, PermissionCodes.CreateRole, PermissionCodes.ConfigureRolePermissions,
            PermissionCodes.UpdateRoleDetails, PermissionCodes.DisableDeleteRole,
        };
        foreach (var perm in adminFull)
            grants.Add(new RolePermissionGrant(RoleCodes.Admin, perm, PermissionLevels.Full));

        // 3) HO — read-only account visibility across the system.
        string[] accountRead =
        {
            PermissionCodes.ViewAccountList, PermissionCodes.ViewAccountDetails, PermissionCodes.SearchFilterAccounts,
        };
        foreach (var perm in accountRead)
            grants.Add(new RolePermissionGrant(RoleCodes.Ho, perm, PermissionLevels.Read));

        // 4) STAFF (incl. Staff Leader) — read-only account visibility (scoped to campus in handlers).
        foreach (var perm in accountRead)
            grants.Add(new RolePermissionGrant(RoleCodes.Staff, perm, PermissionLevels.Read));

        return grants;
    }
}
