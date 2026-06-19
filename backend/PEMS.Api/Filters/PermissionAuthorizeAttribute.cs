using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Api.Filters;

/// <summary>
/// Action filter enforcing an RBAC permission at a minimum level. Use on protected
/// endpoints, e.g. <c>[RequirePermission(PermissionCodes.ViewAccountList, PermissionLevels.Read)]</c>.
/// The backend is the final authority — the frontend only hides UI.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permissionCode;
    private readonly string _minimumLevel;

    public RequirePermissionAttribute(string permissionCode, string minimumLevel = PermissionLevels.Read)
    {
        _permissionCode = permissionCode;
        _minimumLevel = minimumLevel;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var services = context.HttpContext.RequestServices;
        var currentUser = services.GetRequiredService<ICurrentUserService>();

        if (!currentUser.IsAuthenticated)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Authentication required." });
            return;
        }

        var roleId = currentUser.RoleId;
        var roleCode = currentUser.RoleCode;
        var subRole = currentUser.SubRole;

        if (roleId is null || string.IsNullOrEmpty(roleCode))
        {
            context.Result = new ObjectResult(new { message = "You do not have permission to perform this action." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        if (roleCode == "STAFF" || roleCode == "DEPT")
        {
            if (string.IsNullOrEmpty(subRole))
            {
                context.Result = new ObjectResult(new { message = "You do not have permission to perform this action. Sub-role is required." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
        }
        else
        {
            subRole = "NONE";
        }

        var permissionChecker = services.GetRequiredService<IPermissionChecker>();
        var allowed = await permissionChecker.HasPermissionAsync(
            roleId.Value, subRole, _permissionCode, _minimumLevel, context.HttpContext.RequestAborted);

        if (!allowed)
        {
            context.Result = new ObjectResult(new { message = "You do not have permission to perform this action." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
