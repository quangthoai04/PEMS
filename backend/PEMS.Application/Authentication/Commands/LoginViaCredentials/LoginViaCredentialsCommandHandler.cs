using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Authentication.Common;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Authentication.Commands.LoginviaCredentials;

public sealed class LoginviaCredentialsCommandHandler : IRequestHandler<LoginviaCredentialsCommand, AuthResponse>
{
    private const string GenericCredentialError = "Invalid email or password.";

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionService _sessionService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ISecurityAuditService _audit;
    private readonly IDateTimeService _clock;
    private readonly AuthOptions _options;
    private readonly int _maxFailedAttempts;
    private readonly int _lockoutMinutes;

    public LoginviaCredentialsCommandHandler(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ISessionService sessionService,
        IJwtTokenService jwtTokenService,

        ISecurityAuditService audit,
        IDateTimeService clock,
        AuthOptions options,
        IConfiguration configuration)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _sessionService = sessionService;
        _jwtTokenService = jwtTokenService;

        _audit = audit;
        _clock = clock;
        _options = options;
        _maxFailedAttempts = int.TryParse(configuration["Security:MaxFailedLoginAttempts"], out var a) ? a : 5;
        _lockoutMinutes = int.TryParse(configuration["Security:LockoutMinutes"], out var m) ? m : 15;
    }

    public async Task<AuthResponse> Handle(LoginviaCredentialsCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var portal = request.LoginPortal;

        // 0. Password login is disabled entirely in ProductionSsoOnly mode (or by config).
        if (!_options.PasswordLoginEnabled)
            await FailAsync(null, email, portal, LoginLogStatuses.Blocked, "password_login_disabled", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.PasswordLoginDisabled,
                "Password sign-in is disabled. Please use SSO/FEID.", 403, cancellationToken);

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.PrimaryCampus)
            .Include(u => u.Department)
            .Include(u => u.AuthProviders)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Unknown email — generic to avoid account enumeration.
        if (user is null)
            await FailAsync(null, email, portal, LoginLogStatuses.Failed, "user_not_found", SecurityEventFailureReasonCodes.AccountNotFound,
                request, AuthErrorCodes.InvalidCredentials, GenericCredentialError, 401, cancellationToken);

        var now = _clock.UtcNow;

        // Verify the password BEFORE revealing any portal/campus mismatch (anti-enumeration).
        var passwordOk = !string.IsNullOrEmpty(user.PasswordHash)
                         && _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);

        if (!passwordOk)
        {
            user.FailedLoginCount += 1;
            if (user.FailedLoginCount >= _maxFailedAttempts)
            {
                user.LockedUntil = now.AddMinutes(_lockoutMinutes);
                await _db.SaveChangesAsync(cancellationToken);

                await FailAsync(user, email, portal, LoginLogStatuses.Blocked, "lockout_triggered", SecurityEventFailureReasonCodes.AccountDisabled,
                    request, AuthErrorCodes.AccountLocked,
                    "Your account is temporarily locked. Please try again later.", 403, cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await FailAsync(user, email, portal, LoginLogStatuses.Failed, "bad_password", null,
                request, AuthErrorCodes.InvalidCredentials, GenericCredentialError, 401, cancellationToken);
        }

        // Temporary lockout window.
        if (user!.LockedUntil is not null && user.LockedUntil > now)
            await FailAsync(user, email, portal, LoginLogStatuses.Blocked, "account_locked", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountLocked,
                "Your account is temporarily locked. Please try again later.", 403, cancellationToken);

        // Account status must be ACTIVE.
        if (user.Status != UserStatuses.Active)
            await FailAsync(user, email, portal, LoginLogStatuses.Blocked, $"status_{user.Status}", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountInactive, "Your account is not active.", 403, cancellationToken);

        // Role must be active and not soft-deleted.
        if (user.Role is null || user.Role.Status != EntityStatuses.Active)
            await FailAsync(user, email, portal, LoginLogStatuses.Blocked, "role_inactive", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountInactive, "Your account is not active.", 403, cancellationToken);

        // Local password provider, if present, must be enabled.
        var localProvider = user.AuthProviders.FirstOrDefault(p => p.ProviderType == ProviderTypes.LocalPassword);
        if (localProvider is not null && !localProvider.IsEnabled)
            await FailAsync(user, email, portal, LoginLogStatuses.Blocked, "local_provider_disabled", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.PasswordLoginDisabled,
                "Password sign-in is disabled for this account.", 403, cancellationToken);

        // ── Same portal / role / campus policy as SSO (applied after password is verified) ──
        var isVisitor = user.Role!.RoleCode == RoleCodes.Visitor;
        if (portal == LoginPortals.Visitor && !isVisitor)
            await FailAsync(user, email, portal, LoginLogStatuses.Failed, "wrong_portal_internal_in_visitor", SecurityEventFailureReasonCodes.PortalMismatch,
                request, AuthErrorCodes.PortalMismatch,
                "Tài khoản này không được phép đăng nhập tại cổng hiện tại. Vui lòng kiểm tra lại cổng đăng nhập phù hợp.", 403, cancellationToken);

        if (portal == LoginPortals.Internal && isVisitor)
            await FailAsync(user, email, portal, LoginLogStatuses.Failed, "wrong_portal_visitor_in_internal", SecurityEventFailureReasonCodes.PortalMismatch,
                request, AuthErrorCodes.PortalMismatch,
                "Tài khoản này không được phép đăng nhập tại cổng hiện tại. Vui lòng kiểm tra lại cổng đăng nhập phù hợp.", 403, cancellationToken);

        if (portal == LoginPortals.Internal)
        {
            if (!request.SelectedCampusId.HasValue)
                await FailAsync(user, email, portal, LoginLogStatuses.Failed, "missing_campus", SecurityEventFailureReasonCodes.CampusMismatch,
                    request, AuthErrorCodes.CampusRequired,
                    "Tài khoản của bạn không có quyền truy cập cơ sở đã chọn. Vui lòng chọn đúng cơ sở được phân quyền.", 400, cancellationToken);

            var selectedCampus = await _db.Campuses.FirstOrDefaultAsync(c => c.CampusId == request.SelectedCampusId.Value, cancellationToken);
            if (selectedCampus is null)
                await FailAsync(user, email, portal, LoginLogStatuses.Failed, "campus_not_found", SecurityEventFailureReasonCodes.CampusMismatch,
                    request, AuthErrorCodes.CampusNotFound,
                    "Tài khoản của bạn không có quyền truy cập cơ sở đã chọn. Vui lòng chọn đúng cơ sở được phân quyền.", 404, cancellationToken);

            if (selectedCampus.Status != "ACTIVE")
                await FailAsync(user, email, portal, LoginLogStatuses.Failed, "campus_inactive", SecurityEventFailureReasonCodes.CampusMismatch,
                    request, AuthErrorCodes.CampusInactive,
                    "Tài khoản của bạn không có quyền truy cập cơ sở đã chọn. Vui lòng chọn đúng cơ sở được phân quyền.", 403, cancellationToken);

            if (user.PrimaryCampusId is not null
                && request.SelectedCampusId != user.PrimaryCampusId)
                await FailAsync(user, email, portal, LoginLogStatuses.Failed, "campus_mismatch", SecurityEventFailureReasonCodes.CampusMismatch,
                    request, AuthErrorCodes.CampusMismatch,
                    "Tài khoản của bạn không có quyền truy cập cơ sở đã chọn. Vui lòng chọn đúng cơ sở được phân quyền.", 403, cancellationToken);
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
            _sessionService, _jwtTokenService, cancellationToken);

        await _audit.WriteLoginLogAsync(user.UserId, email, portal, request.SelectedCampusId,
            ProviderTypes.LocalPassword, LoginLogStatuses.Success, null,
            request.IpAddress, request.UserAgent, null, cancellationToken);


        return response;
    }

    /// <summary>Writes a failed/blocked login log + security event, then throws a coded auth error. Never returns.</summary>
    private async Task FailAsync(
        User? user, string email, string portal, string logStatus, string internalReason, string? failureReasonCode,
        LoginviaCredentialsCommand request, string errorCode, string message, int statusCode,
        CancellationToken cancellationToken)
    {
        await _audit.WriteLoginLogAsync(user?.UserId, email, portal, request.SelectedCampusId,
            ProviderTypes.LocalPassword, logStatus, internalReason, request.IpAddress, request.UserAgent, null, cancellationToken);

        if (failureReasonCode != null)
        {
            var eventType = failureReasonCode switch
            {
                SecurityEventFailureReasonCodes.PortalMismatch => SecurityEventTypes.PortalValidation,
                SecurityEventFailureReasonCodes.CampusMismatch => SecurityEventTypes.CampusValidation,
                _ => SecurityEventTypes.SecurityPolicyCheck
            };
            
            await _audit.WriteSecurityEventAsync(user?.UserId, email, eventType,
                "BLOCKED", failureReasonCode, request.IpAddress, request.UserAgent,
                portal, request.SelectedCampusId, null, null, internalReason, cancellationToken);
        }

        throw new AuthBusinessException(errorCode, message, statusCode);
    }
}
