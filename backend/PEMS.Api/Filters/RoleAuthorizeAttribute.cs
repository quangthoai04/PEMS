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

    /// <summary>
    /// The <c>errorCode</c> this gate returns on a refusal, when the module publishes one of its own.
    /// Defaults to the generic <c>FORBIDDEN</c>.
    ///
    /// <para>
    /// It exists because moving a refusal EARLIER must not silently change what the refusal says. Some
    /// modules were guarded only inside their handlers and answered with a domain code — Department
    /// management answers <c>DEPARTMENT_MANAGEMENT_FORBIDDEN</c>, which the department screen maps to
    /// "Bạn không có quyền quản lý phòng ban." Putting a role gate in front of those handlers is the
    /// right change; letting it answer <c>FORBIDDEN</c> instead was an unintended one, and it reached
    /// the user as a vaguer sentence.
    /// </para>
    /// <para>
    /// Set it ONLY where the module already publishes a code and a client already reads it. A gate with
    /// nothing to say keeps the generic answer, which is what almost every endpoint should do: inventing
    /// a per-endpoint code for every refusal gives clients more strings to branch on and no more
    /// information.
    /// </para>
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// The message that accompanies <see cref="ErrorCode"/>. Ignored unless one is set, so a gate cannot
    /// end up wording a refusal differently from the code it reports.
    /// </summary>
    public string? Message { get; set; }

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
            context.Result = Forbidden(context, ErrorCode, Message);
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
            context.Result = Forbidden(context, ErrorCode, Message);
            return Task.CompletedTask;
        }

        if (_allowedRoles.Length > 0 && !_allowedRoles.Contains(effectiveRole))
        {
            context.Result = Forbidden(context, ErrorCode, Message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Matches the shape ExceptionHandlingMiddleware emits for an <c>AuthBusinessException</c>, so a
    /// refusal from this gate and a refusal from a handler are the same object to a client.
    /// </summary>
    private static ObjectResult Forbidden(
        AuthorizationFilterContext context, string? errorCode, string? message) =>
        new(new
        {
            success = false,
            errorCode = string.IsNullOrWhiteSpace(errorCode) ? "FORBIDDEN" : errorCode,
            message = string.IsNullOrWhiteSpace(message)
                ? "Bạn không có quyền thực hiện thao tác này."
                : message,
            traceId = context.HttpContext.TraceIdentifier,
        })
        { StatusCode = StatusCodes.Status403Forbidden };
}
