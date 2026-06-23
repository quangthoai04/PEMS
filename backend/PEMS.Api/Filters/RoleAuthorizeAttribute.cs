using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RoleAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _allowedRoles;

    public RoleAuthorizeAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentUserService = context.HttpContext.RequestServices.GetService(typeof(ICurrentUserService)) as ICurrentUserService;

        if (currentUserService == null || !currentUserService.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (string.IsNullOrEmpty(currentUserService.RoleCode))
        {
            context.Result = new ForbidResult();
            return;
        }

        // Determine EffectiveRole
        var effectiveRole = PEMS.Application.Common.Security.EffectiveRole.Resolve(currentUserService.RoleCode, currentUserService.SubRole);

        if (_allowedRoles.Length > 0 && !_allowedRoles.Contains(effectiveRole))
        {
            context.Result = new ObjectResult(new { Message = "Bạn không có quyền thực hiện thao tác này." }) { StatusCode = 403 };
            return;
        }
        
        await Task.CompletedTask;
    }
}
