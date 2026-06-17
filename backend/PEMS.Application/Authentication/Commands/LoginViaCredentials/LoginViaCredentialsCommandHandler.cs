using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Authentication.Common;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Authentication.Commands.LoginviaCredentials;

public sealed class LoginviaCredentialsCommandHandler : IRequestHandler<LoginviaCredentialsCommand, AuthResponse>
{
    private const string GenericCredentialError = "Invalid email or password.";
    private const string GenericBlockedError = "Unable to sign in. Please contact administrator.";

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionService _sessionService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPermissionChecker _permissionChecker;
    private readonly ISecurityAuditService _audit;
    private readonly IDateTimeService _clock;
    private readonly int _maxFailedAttempts;
    private readonly int _lockoutMinutes;

    public LoginviaCredentialsCommandHandler(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ISessionService sessionService,
        IJwtTokenService jwtTokenService,
        IPermissionChecker permissionChecker,
        ISecurityAuditService audit,
        IDateTimeService clock,
        IConfiguration configuration)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _sessionService = sessionService;
        _jwtTokenService = jwtTokenService;
        _permissionChecker = permissionChecker;
        _audit = audit;
        _clock = clock;
        _maxFailedAttempts = int.TryParse(configuration["Security:MaxFailedLoginAttempts"], out var a) ? a : 5;
        _lockoutMinutes = int.TryParse(configuration["Security:LockoutMinutes"], out var m) ? m : 15;
    }

    public async Task<AuthResponse> Handle(LoginviaCredentialsCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var portal = request.LoginPortal;

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.PrimaryCampus)
            .Include(u => u.Department)
            .Include(u => u.AuthProviders)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
            await FailAsync(null, email, portal, ProviderTypes.LocalPassword, LoginLogStatuses.Failed,
                "user_not_found", request, GenericCredentialError, cancellationToken);

        var now = _clock.UtcNow;

        // Temporary lockout window.
        if (user!.LockedUntil is not null && user.LockedUntil > now)
            await FailAsync(user, email, portal, ProviderTypes.LocalPassword, LoginLogStatuses.Blocked,
                "account_locked", request, GenericBlockedError, cancellationToken);

        // Account status must be ACTIVE.
        if (user.Status != UserStatuses.Active)
            await FailAsync(user, email, portal, ProviderTypes.LocalPassword, LoginLogStatuses.Blocked,
                $"status_{user.Status}", request, GenericBlockedError, cancellationToken);

        // Role must be active and not soft-deleted.
        if (user.Role is null || user.Role.Status != EntityStatuses.Active || user.Role.DeletedAt is not null)
            await FailAsync(user, email, portal, ProviderTypes.LocalPassword, LoginLogStatuses.Blocked,
                "role_inactive", request, GenericBlockedError, cancellationToken);

        // Portal must match the role (VISITOR ↔ VISITOR portal; everyone else ↔ INTERNAL).
        var isVisitor = user.Role!.RoleCode == RoleCodes.Visitor;
        if ((portal == LoginPortals.Visitor && !isVisitor) || (portal == LoginPortals.Internal && isVisitor))
            await FailAsync(user, email, portal, ProviderTypes.LocalPassword, LoginLogStatuses.Failed,
                "wrong_portal", request, GenericBlockedError, cancellationToken);

        // Local password provider, if present, must be enabled.
        var localProvider = user.AuthProviders
            .FirstOrDefault(p => p.ProviderType == ProviderTypes.LocalPassword);
        if (localProvider is not null && !localProvider.IsEnabled)
            await FailAsync(user, email, portal, ProviderTypes.LocalPassword, LoginLogStatuses.Blocked,
                "local_provider_disabled", request, GenericBlockedError, cancellationToken);

        // Verify the password.
        var passwordOk = !string.IsNullOrEmpty(user.PasswordHash)
                         && _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);

        if (!passwordOk)
        {
            user.FailedLoginCount += 1;
            var lockedNow = user.FailedLoginCount >= _maxFailedAttempts;
            if (lockedNow)
            {
                user.LockedUntil = now.AddMinutes(_lockoutMinutes);
                await _db.SaveChangesAsync(cancellationToken);
                await _audit.WriteSecurityEventAsync(user.UserId, email, SecurityEventTypes.AccountLocked,
                    SecuritySeverities.High, request.IpAddress, request.UserAgent,
                    $"{{\"failedAttempts\":{user.FailedLoginCount}}}", cancellationToken);
                await FailAsync(user, email, portal, ProviderTypes.LocalPassword, LoginLogStatuses.Blocked,
                    "lockout_triggered", request, GenericBlockedError, cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await FailAsync(user, email, portal, ProviderTypes.LocalPassword, LoginLogStatuses.Failed,
                "bad_password", request, GenericCredentialError, cancellationToken);
        }

        // ── Success ───────────────────────────────────────────────────────────
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        user.FirstLoginAt ??= now;
        if (localProvider is not null)
            localProvider.LastUsedAt = now;

        var response = await AuthResultBuilder.IssueAsync(
            user, portal, localProvider?.AuthProviderId, request.IpAddress, request.UserAgent,
            _sessionService, _jwtTokenService, _permissionChecker, cancellationToken);

        await _audit.WriteLoginLogAsync(user.UserId, email, portal, user.PrimaryCampusId,
            ProviderTypes.LocalPassword, LoginLogStatuses.Success, null,
            request.IpAddress, request.UserAgent, null, cancellationToken);
        await _audit.WriteSecurityEventAsync(user.UserId, email, SecurityEventTypes.LoginSuccess,
            SecuritySeverities.Low, request.IpAddress, request.UserAgent, null, cancellationToken);

        return response;
    }

    /// <summary>Writes a failed/blocked login log and throws a generic 401. Never returns.</summary>
    private async Task FailAsync(
        User? user, string email, string portal, string providerType, string status,
        string internalReason, LoginviaCredentialsCommand request, string publicMessage,
        CancellationToken cancellationToken)
    {
        await _audit.WriteLoginLogAsync(user?.UserId, email, portal, user?.PrimaryCampusId,
            providerType, status, internalReason, request.IpAddress, request.UserAgent, null, cancellationToken);

        if (status == LoginLogStatuses.Blocked)
            await _audit.WriteSecurityEventAsync(user?.UserId, email, SecurityEventTypes.LoginBlocked,
                SecuritySeverities.Medium, request.IpAddress, request.UserAgent,
                $"{{\"reason\":\"{internalReason}\"}}", cancellationToken);
        else
            await _audit.WriteSecurityEventAsync(user?.UserId, email, SecurityEventTypes.LoginFailed,
                SecuritySeverities.Low, request.IpAddress, request.UserAgent,
                $"{{\"reason\":\"{internalReason}\"}}", cancellationToken);

        throw new AuthenticationFailedException(publicMessage, internalReason);
    }
}
