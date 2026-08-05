using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Api.Filters;

/// <summary>
/// Coarse role gate (authorization layer 2). Resolves the caller's effective role from
/// (role_code + sub_role) and rejects anything outside <paramref name="allowedRoles"/>.
///
/// This is deliberately NOT the last line of defence: handlers still enforce object/data
/// scope (campus, department, ownership, participation). A role that passes here can still
/// be refused for the specific record it asked for.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RoleAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _allowedRoles;

    public RoleAuthorizeAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // A method-level [AllowAnonymous] must win over a class-level [RoleAuthorize].
        // Several controllers carry the role list on the class and open one or two
        // actions (public campus lists, invitation landing pages) individually; without
        // this check the class attribute would still 401 them.
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return Task.CompletedTask;
        }

        var currentUserService = context.HttpContext.RequestServices.GetService(typeof(ICurrentUserService)) as ICurrentUserService;

        if (currentUserService == null || !currentUserService.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        if (string.IsNullOrEmpty(currentUserService.RoleCode))
        {
            context.Result = Forbidden(context);
            return Task.CompletedTask;
        }

        string effectiveRole;
        try
        {
            effectiveRole = PEMS.Application.Common.Security.EffectiveRole.Resolve(
                currentUserService.RoleCode, currentUserService.SubRole);
        }
        catch (InvalidOperationException)
        {
            // An account whose (role_code, sub_role) pair is not a valid combination —
            // e.g. STAFF with no sub_role — is a data defect, not a server fault. It must
            // fail closed with 403 and never surface as a 500 (which both leaks that the
            // request got past authentication and, worse, tempts a "default to allow" fix).
            context.Result = Forbidden(context);
            return Task.CompletedTask;
        }

        if (_allowedRoles.Length > 0 && !_allowedRoles.Contains(effectiveRole))
        {
            context.Result = Forbidden(context);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Matches the shape ExceptionHandlingMiddleware emits so clients parse one contract.
    /// </summary>
    private static ObjectResult Forbidden(AuthorizationFilterContext context) =>
        new(new
        {
            success = false,
            errorCode = "FORBIDDEN",
            message = "Bạn không có quyền thực hiện thao tác này.",
            traceId = context.HttpContext.TraceIdentifier,
        })
        { StatusCode = StatusCodes.Status403Forbidden };
}
