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

    // IDs are stored as plain strings in the JWT claims (BIGINT values) and parsed back to ulong here.
    private static ulong? ParseId(string? value)
        => ulong.TryParse(value, out var id) ? id : null;

    public ulong? UserId =>
        ParseId(Principal?.FindFirstValue(PemsClaimTypes.UserId)
                ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier));

    public string? Email =>
        Principal?.FindFirstValue(PemsClaimTypes.Email)
        ?? Principal?.FindFirstValue(ClaimTypes.Email);

    public ulong? RoleId => ParseId(Principal?.FindFirstValue(PemsClaimTypes.RoleId));

    public string? RoleCode =>
        Principal?.FindFirstValue(PemsClaimTypes.RoleCode)
        ?? Principal?.FindFirstValue(ClaimTypes.Role);

    public string? SubRole => Principal?.FindFirstValue(PemsClaimTypes.SubRole);

    public ulong? PrimaryCampusId => ParseId(Principal?.FindFirstValue(PemsClaimTypes.PrimaryCampusId));

    public ulong? DepartmentId => ParseId(Principal?.FindFirstValue(PemsClaimTypes.DepartmentId));

    public ulong? SessionId => ParseId(Principal?.FindFirstValue(PemsClaimTypes.SessionId));

    public string? LoginPortal => Principal?.FindFirstValue(PemsClaimTypes.LoginPortal);
}
