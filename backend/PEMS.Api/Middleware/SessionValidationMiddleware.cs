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
                DepartmentStatus = u.Department != null ? u.Department.Status : null,
                CampusStatus = u.PrimaryCampus != null ? u.PrimaryCampus.Status : null
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

        // UC-86 force-logout: STAFF/DEPARTMENT/STUDENT accounts lose access immediately when
        // their primary campus is disabled — a still-valid JWT never overrides the DB state
        // (BR-AUTH-CAMPUS-06/07). 403 + machine-readable code so the frontend can clear its
        // auth state and redirect to login (BR-AUTH-CAMPUS-08). HO/ADMIN are never blocked.
        if (CampusAccessRule.IsBlocked(account.RoleCode, account.CampusStatus))
        {
            await WriteForbiddenAsync(
                context, AuthErrorCodes.CampusInactiveAccessDenied, CampusAccessRule.BlockedMessage);
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

    /// <summary>
    /// 403 with a machine-readable error code (same payload shape as ExceptionHandlingMiddleware)
    /// — the token itself is valid; the account's org context (campus) denies access.
    /// </summary>
    private static async Task WriteForbiddenAsync(HttpContext context, string errorCode, string message)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { success = false, errorCode, message }, JsonOptions));
    }
}
