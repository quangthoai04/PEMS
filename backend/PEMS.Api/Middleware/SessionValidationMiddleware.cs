using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Api.Middleware;

/// <summary>
/// For every authenticated request, ensures the bound session is still active and
/// that the user + role are still ACTIVE. This makes logout, account deactivation
/// and role disabling take effect immediately (the frontend only hides UI).
/// </summary>
public sealed class SessionValidationMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;

    public SessionValidationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ISessionService sessionService,
        IApplicationDbContext db)
    {
        var principal = context.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var sessionIdClaim = principal.FindFirst(PemsClaimTypes.SessionId)?.Value;
        var userIdClaim = principal.FindFirst(PemsClaimTypes.UserId)?.Value;

        if (!ulong.TryParse(sessionIdClaim, out var sessionId) || !ulong.TryParse(userIdClaim, out var userId))
        {
            await WriteUnauthorizedAsync(context, "Your session is no longer valid. Please sign in again.");
            return;
        }

        var sessionActive = await sessionService.IsSessionActiveAsync(sessionId, context.RequestAborted);
        if (!sessionActive)
        {
            await WriteUnauthorizedAsync(context, "Your session has been revoked. Please sign in again.");
            return;
        }

        var account = await db.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.Status,
                RoleStatus = u.Role!.Status,
                RoleCode = u.Role!.RoleCode,
                DepartmentStatus = u.Department != null ? u.Department.Status : null
            })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (account is null
            || account.Status != UserStatuses.Active
            || account.RoleStatus != EntityStatuses.Active)
        {
            await WriteUnauthorizedAsync(context, "Your account is not active. Please contact administrator.");
            return;
        }

        // UC-106: DEPARTMENT accounts lose access immediately when their department is disabled,
        // even if a session somehow escaped revocation — never trust the JWT's login-time snapshot.
        if (DepartmentAccessRule.IsBlocked(account.RoleCode, account.DepartmentStatus))
        {
            await WriteUnauthorizedAsync(context, "Your department is no longer active. Please contact administrator.");
            return;
        }

        await _next(context);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message }, JsonOptions));
    }
}
