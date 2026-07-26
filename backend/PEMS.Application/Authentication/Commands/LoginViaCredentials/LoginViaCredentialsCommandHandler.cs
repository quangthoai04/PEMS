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

        // Portal is no longer chosen by the client — it is derived below from the
        // account's own role once it is loaded. Before that point (account unknown or
        // password login globally disabled) there is no account to derive it from, so
        // audit trails default to VISITOR (no known account ⇒ treat as external/visitor
        // traffic — same default used by the Google SSO handler's auto-provision path).
        var portal = LoginPortals.Visitor;

        // 0. Password login is disabled entirely in ProductionSsoOnly mode (or by config).
        if (!_options.PasswordLoginEnabled)
            await FailAsync(null, email, portal, null, LoginLogStatuses.Blocked, "password_login_disabled", SecurityEventFailureReasonCodes.AccountDisabled,
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
            await FailAsync(null, email, portal, null, LoginLogStatuses.Failed, "user_not_found", SecurityEventFailureReasonCodes.AccountNotFound,
                request, AuthErrorCodes.InvalidCredentials, GenericCredentialError, 401, cancellationToken);

        // Account found — derive the real portal from its role instead of trusting a client choice.
        portal = user!.Role!.RoleCode == RoleCodes.Visitor ? LoginPortals.Visitor : LoginPortals.Internal;

        var now = _clock.VietnamNow;

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

                await FailAsync(user, email, portal, user.PrimaryCampusId, LoginLogStatuses.Blocked, "lockout_triggered", SecurityEventFailureReasonCodes.AccountDisabled,
                    request, AuthErrorCodes.AccountLocked,
                    "Your account is temporarily locked. Please try again later.", 403, cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await FailAsync(user, email, portal, user.PrimaryCampusId, LoginLogStatuses.Failed, "bad_password", null,
                request, AuthErrorCodes.InvalidCredentials, GenericCredentialError, 401, cancellationToken);
        }

        // Temporary lockout window.
        if (user!.LockedUntil is not null && user.LockedUntil > now)
            await FailAsync(user, email, portal, user.PrimaryCampusId, LoginLogStatuses.Blocked, "account_locked", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountLocked,
                "Your account is temporarily locked. Please try again later.", 403, cancellationToken);

        // P0 #1: a pending account has not proven email ownership — a specific message, never "inactive"/campus.
        if (user.Status == UserStatuses.PendingEmailConfirmation)
            await FailAsync(user, email, portal, user.PrimaryCampusId, LoginLogStatuses.Blocked, "status_pending_email_confirmation", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountPendingEmailConfirmation,
                "Tài khoản chưa xác nhận email. Vui lòng kiểm tra email để xác nhận trước khi đăng nhập.", 403, cancellationToken);

        // Account status must be ACTIVE.
        if (user.Status != UserStatuses.Active)
            await FailAsync(user, email, portal, user.PrimaryCampusId, LoginLogStatuses.Blocked, $"status_{user.Status}", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountInactive, "Your account is not active.", 403, cancellationToken);

        // Role must be active and not soft-deleted.
        if (user.Role is null || user.Role.Status != EntityStatuses.Active)
            await FailAsync(user, email, portal, user.PrimaryCampusId, LoginLogStatuses.Blocked, "role_inactive", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountInactive, "Your account is not active.", 403, cancellationToken);

        // UC-106: DEPARTMENT accounts lose access while their department is INACTIVE
        // (users.status stays untouched — this is an org-level lock, not a personal one).
        if (DepartmentAccessRule.IsBlocked(user.Role!.RoleCode, user.Department?.Status))
            await FailAsync(user, email, portal, user.PrimaryCampusId, LoginLogStatuses.Blocked, "department_inactive", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.DepartmentInactive, DepartmentAccessRule.BlockedMessage, 403, cancellationToken);

        // UC-86 force-logout: campus-scoped accounts (STAFF/DEPARTMENT/STUDENT) may not sign in
        // while their PRIMARY campus is INACTIVE — no session, no tokens (BR-AUTH-CAMPUS-06).
        if (CampusAccessRule.IsBlocked(user.Role!.RoleCode, user.PrimaryCampus?.Status))
            await FailAsync(user, email, portal, user.PrimaryCampusId, LoginLogStatuses.Blocked, "campus_inactive_access_denied", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.CampusInactiveAccessDenied, CampusAccessRule.BlockedMessage, 403, cancellationToken);

        // Local password provider, if present, must be enabled.
        var localProvider = user.AuthProviders.FirstOrDefault(p => p.ProviderType == ProviderTypes.LocalPassword);
        if (localProvider is not null && !localProvider.IsEnabled)
            await FailAsync(user, email, portal, user.PrimaryCampusId, LoginLogStatuses.Blocked, "local_provider_disabled", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.PasswordLoginDisabled,
                "Password sign-in is disabled for this account.", 403, cancellationToken);

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

        await _audit.WriteLoginLogAsync(user.UserId, email, portal, user.PrimaryCampusId,
            ProviderTypes.LocalPassword, LoginLogStatuses.Success, null,
            request.IpAddress, request.UserAgent, null, cancellationToken);


        return response;
    }

    /// <summary>Writes a failed/blocked login log + security event, then throws a coded auth error. Never returns.</summary>
    private async Task FailAsync(
        User? user, string email, string portal, ulong? campusId, string logStatus, string internalReason, string? failureReasonCode,
        LoginviaCredentialsCommand request, string errorCode, string message, int statusCode,
        CancellationToken cancellationToken)
    {
        await _audit.WriteLoginLogAsync(user?.UserId, email, portal, campusId,
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
                portal, campusId, null, null, internalReason, cancellationToken);
        }

        throw new AuthBusinessException(errorCode, message, statusCode);
    }
}
