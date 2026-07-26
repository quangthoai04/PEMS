using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Authentication.Common;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Authentication.Commands.LoginviaSSO;

public sealed class LoginviaSSOCommandHandler : IRequestHandler<LoginviaSSOCommand, AuthResponse>
{
    // Deliberately generic so SSO never reveals whether/why an account failed beyond the coded reason.
    private const string GenericSsoError = "Unable to sign in with this account.";

    private readonly IApplicationDbContext _db;
    private readonly IGoogleTokenValidator _googleValidator;
    private readonly ISessionService _sessionService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ISecurityAuditService _audit;
    private readonly IDateTimeService _clock;
    private readonly AuthOptions _options;

    public LoginviaSSOCommandHandler(
        IApplicationDbContext db,
        IGoogleTokenValidator googleValidator,
        ISessionService sessionService,
        IJwtTokenService jwtTokenService,

        ISecurityAuditService audit,
        IDateTimeService clock,
        AuthOptions options)
    {
        _db = db;
        _googleValidator = googleValidator;
        _sessionService = sessionService;
        _jwtTokenService = jwtTokenService;

        _audit = audit;
        _clock = clock;
        _options = options;
    }

    public async Task<AuthResponse> Handle(LoginviaSSOCommand request, CancellationToken cancellationToken)
    {
        // Portal is no longer chosen by the client — it is derived below from the account's
        // own role once it is loaded. Before that point (SSO disabled / invalid token) there
        // is no account to derive it from, so audit trails default to VISITOR (same neutral
        // default used by the credentials handler for its pre-account failure paths).
        var portal = LoginPortals.Visitor;

        // Provider gate.
        if (!_options.AllowGoogleSso)
            await FailAsync(null, "unknown", portal, null, "google_sso_disabled", SecurityEventFailureReasonCodes.SsoProviderError,
                request, AuthErrorCodes.SsoDisabled, "Google sign-in is currently disabled.", 403, cancellationToken);

        var info = await _googleValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (info is null)
            await FailAsync(null, "unknown", portal, null, "invalid_google_token", SecurityEventFailureReasonCodes.InvalidSsoClaims,
                request, AuthErrorCodes.ExternalAuthFailed, GenericSsoError, 401, cancellationToken);

        var email = info!.Email.Trim().ToLowerInvariant();
        var now = _clock.VietnamNow;

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.PrimaryCampus)
            .Include(u => u.Department)
            .Include(u => u.AuthProviders)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // No matching account — auto-provision a VISITOR (the only role an external login may
        // create for itself; there is no "Internal" attempt to fall back to anymore).
        if (user is null)
        {
            if (!_options.AutoCreateVisitorOnExternalLogin)
                await FailAsync(null, email, portal, null, "visitor_provision_disabled", SecurityEventFailureReasonCodes.VisitorAutoProvisionDisabled,
                    request, AuthErrorCodes.VisitorProvisionDisabled, GenericSsoError, 403, cancellationToken);

            user = await CreateVisitorFromExternalLoginAsync(info, email, now, cancellationToken);
            var newProvider = user.AuthProviders.First(p => p.ProviderType == ProviderTypes.GoogleSso);
            return await IssueAndAuditAsync(user, LoginPortals.Visitor, newProvider, email, now, request, cancellationToken);
        }

        // Account found — derive the real portal from its role instead of trusting a client choice.
        portal = user.Role!.RoleCode == RoleCodes.Visitor ? LoginPortals.Visitor : LoginPortals.Internal;

        await EnsureActiveAsync(user, email, portal, request, cancellationToken);

        var provider = await EnsureGoogleProviderLinkedAsync(user, info, now, email, portal, request, cancellationToken);
        return await IssueAndAuditAsync(user, portal, provider, email, now, request, cancellationToken);
    }

    /// <summary>Creates a brand-new VISITOR account + linked Google provider and persists it.</summary>
    private async Task<User> CreateVisitorFromExternalLoginAsync(
        GoogleUserInfo info, string email, DateTime now, CancellationToken cancellationToken)
    {
        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.RoleCode == RoleCodes.Visitor
                                      && r.Status == EntityStatuses.Active
                                      , cancellationToken)
            ?? throw new AuthBusinessException(AuthErrorCodes.ExternalAuthFailed,
                "Visitor role is not configured.", 500);

        var user = new User
        {
            FullName = string.IsNullOrWhiteSpace(info.Name) ? email : info.Name!.Trim(),
            Email = email,
            RoleId = role.RoleId,
            Status = UserStatuses.Active,
            CreatedVia = CreatedViaValues.SsoAutoProvision,
            EmailVerifiedAt = info.EmailVerified ? now : null,
            CreatedAt = now,
            Role = role,
            // VISITOR: no campus / department / sub-role / password (enforced by DB triggers).
        };

        var provider = new UserAuthProvider
        {
            // AuthProviderId is DB-generated; UserId is set via the navigation on save.
            ProviderType = ProviderTypes.GoogleSso,
            ProviderSubject = info.Subject,
            ProviderEmail = email,
            IsEnabled = true,
            LinkedAt = now,
        };
        user.AuthProviders.Add(provider);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
        return user;
    }

    /// <summary>
    /// Finds the user's Google provider, auto-linking one on first SSO use for a
    /// pre-provisioned account (the spec's <c>EnsureLinkedAsync</c>). Verifies the subject.
    /// </summary>
    private async Task<UserAuthProvider> EnsureGoogleProviderLinkedAsync(
        User user, GoogleUserInfo info, DateTime now, string email, string portal,
        LoginviaSSOCommand request, CancellationToken cancellationToken)
    {
        var provider = user.AuthProviders.FirstOrDefault(p => p.ProviderType == ProviderTypes.GoogleSso);
        if (provider is null)
        {
            provider = new UserAuthProvider
            {
                UserId = user.UserId,
                ProviderType = ProviderTypes.GoogleSso,
                ProviderSubject = info.Subject,
                ProviderEmail = email,
                IsEnabled = true,
                LinkedAt = now,
            };
            user.AuthProviders.Add(provider);
            _db.UserAuthProviders.Add(provider);
            await _db.SaveChangesAsync(cancellationToken);
            return provider;
        }

        if (!provider.IsEnabled)
            await FailAsync(user, email, portal, user.PrimaryCampusId, "google_provider_disabled", SecurityEventFailureReasonCodes.SsoProviderError,
                request, AuthErrorCodes.ExternalAuthFailed, GenericSsoError, 403, cancellationToken);

        if (string.IsNullOrEmpty(provider.ProviderSubject))
            provider.ProviderSubject = info.Subject;
        else if (!string.Equals(provider.ProviderSubject, info.Subject, StringComparison.Ordinal))
            await FailAsync(user, email, portal, user.PrimaryCampusId, "google_subject_mismatch", SecurityEventFailureReasonCodes.InvalidSsoClaims,
                request, AuthErrorCodes.ExternalAuthFailed, GenericSsoError, 403, cancellationToken);

        return provider;
    }

    private async Task EnsureActiveAsync(
        User user, string email, string portal, LoginviaSSOCommand request, CancellationToken cancellationToken)
    {
        if (user.LockedUntil is not null && user.LockedUntil > _clock.VietnamNow)
            await FailAsync(user, email, portal, user.PrimaryCampusId, "account_locked", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountLocked,
                "Your account is temporarily locked. Please try again later.", 403, cancellationToken);

        // P0 #1: a pending account has not proven email ownership — a specific message, never "inactive"/campus.
        if (user.Status == UserStatuses.PendingEmailConfirmation)
            await FailAsync(user, email, portal, user.PrimaryCampusId, "status_pending_email_confirmation", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountPendingEmailConfirmation,
                "Tài khoản chưa xác nhận email. Vui lòng kiểm tra email để xác nhận trước khi đăng nhập.", 403, cancellationToken);

        if (user.Status != UserStatuses.Active)
            await FailAsync(user, email, portal, user.PrimaryCampusId, $"status_{user.Status}", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountInactive, "Your account is not active.", 403, cancellationToken);

        if (user.Role is null || user.Role.Status != EntityStatuses.Active)
            await FailAsync(user, email, portal, user.PrimaryCampusId, "role_inactive", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountInactive, "Your account is not active.", 403, cancellationToken);

        // UC-106: DEPARTMENT accounts lose access while their department is INACTIVE
        // (applies to DEPARTMENT+LEADER/STAFF; other roles are never blocked by this rule).
        if (DepartmentAccessRule.IsBlocked(user.Role!.RoleCode, user.Department?.Status))
            await FailAsync(user, email, portal, user.PrimaryCampusId, "department_inactive", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.DepartmentInactive, DepartmentAccessRule.BlockedMessage, 403, cancellationToken);

        // UC-86 force-logout: campus-scoped internal accounts (STAFF/DEPARTMENT/STUDENT) may not
        // sign in while their PRIMARY campus is INACTIVE — no session, no tokens
        // (BR-AUTH-CAMPUS-06). The rule is role-scoped, so Visitor SSO is never affected.
        if (CampusAccessRule.IsBlocked(user.Role!.RoleCode, user.PrimaryCampus?.Status))
            await FailAsync(user, email, portal, user.PrimaryCampusId, "campus_inactive_access_denied", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.CampusInactiveAccessDenied, CampusAccessRule.BlockedMessage, 403, cancellationToken);
    }

    private async Task<AuthResponse> IssueAndAuditAsync(
        User user, string portal, UserAuthProvider provider, string email, DateTime now,
        LoginviaSSOCommand request, CancellationToken cancellationToken)
    {
        provider.LastUsedAt = now;
        provider.ProviderEmail = email;
        user.LastLoginAt = now;
        user.FirstLoginAt ??= now;

        var response = await AuthResultBuilder.IssueAsync(
            user, portal, provider.AuthProviderId, request.IpAddress, request.UserAgent,
            _sessionService, _jwtTokenService, cancellationToken);

        await _audit.WriteLoginLogAsync(user.UserId, email, portal, user.PrimaryCampusId,
            ProviderTypes.GoogleSso, LoginLogStatuses.Success, null,
            request.IpAddress, request.UserAgent, null, cancellationToken);
        await _audit.WriteSecurityEventAsync(user.UserId, email, SecurityEventTypes.SsoLogin,
            "SUCCESS", null, request.IpAddress, request.UserAgent,
            portal, user.PrimaryCampusId, ProviderTypes.GoogleSso, null, null, cancellationToken);

        return response;
    }

    /// <summary>Writes a failed login log + security event, then throws a coded auth error. Never returns.</summary>
    private async Task FailAsync(
        User? user, string email, string portal, ulong? campusId, string internalReason, string failureReasonCode,
        LoginviaSSOCommand request, string errorCode, string message, int statusCode,
        CancellationToken cancellationToken)
    {
        await _audit.WriteLoginLogAsync(user?.UserId, email, portal, campusId,
            ProviderTypes.GoogleSso, LoginLogStatuses.Failed, internalReason,
            request.IpAddress, request.UserAgent, null, cancellationToken);
        var eventType = failureReasonCode switch
        {
            SecurityEventFailureReasonCodes.PortalMismatch => SecurityEventTypes.PortalValidation,
            SecurityEventFailureReasonCodes.CampusMismatch => SecurityEventTypes.CampusValidation,
            _ => statusCode == 403 ? SecurityEventTypes.SecurityPolicyCheck : SecurityEventTypes.SsoLogin
        };

        var resultText = statusCode == 403 ? "BLOCKED" : "FAILED";

        await _audit.WriteSecurityEventAsync(user?.UserId, email, eventType,
            resultText, failureReasonCode, request.IpAddress, request.UserAgent,
            portal, campusId, ProviderTypes.GoogleSso, null, internalReason, cancellationToken);

        throw new AuthBusinessException(errorCode, message, statusCode);
    }
}
