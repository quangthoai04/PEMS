using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;

namespace PEMS.Infrastructure.Identity;

/// <summary>
/// Reads the caller's identity from the validated JWT claims on the current
/// HttpContext. Returns nulls for anonymous requests.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? UserId =>
        Principal?.FindFirstValue(PemsClaimTypes.UserId)
        ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email =>
        Principal?.FindFirstValue(PemsClaimTypes.Email)
        ?? Principal?.FindFirstValue(ClaimTypes.Email);

    public string? RoleId => Principal?.FindFirstValue(PemsClaimTypes.RoleId);

    public string? RoleCode =>
        Principal?.FindFirstValue(PemsClaimTypes.RoleCode)
        ?? Principal?.FindFirstValue(ClaimTypes.Role);

    public string? SubRole => Principal?.FindFirstValue(PemsClaimTypes.SubRole);

    public string? PrimaryCampusId => Principal?.FindFirstValue(PemsClaimTypes.PrimaryCampusId);

    public string? DepartmentId => Principal?.FindFirstValue(PemsClaimTypes.DepartmentId);

    public string? SessionId => Principal?.FindFirstValue(PemsClaimTypes.SessionId);

    public string? LoginPortal => Principal?.FindFirstValue(PemsClaimTypes.LoginPortal);
}
