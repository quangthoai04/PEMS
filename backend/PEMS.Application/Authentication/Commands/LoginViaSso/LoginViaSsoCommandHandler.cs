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
    private readonly IPermissionChecker _permissionChecker;
    private readonly ISecurityAuditService _audit;
    private readonly IDateTimeService _clock;
    private readonly AuthOptions _options;

    public LoginviaSSOCommandHandler(
        IApplicationDbContext db,
        IGoogleTokenValidator googleValidator,
        ISessionService sessionService,
        IJwtTokenService jwtTokenService,
        IPermissionChecker permissionChecker,
        ISecurityAuditService audit,
        IDateTimeService clock,
        AuthOptions options)
    {
        _db = db;
        _googleValidator = googleValidator;
        _sessionService = sessionService;
        _jwtTokenService = jwtTokenService;
        _permissionChecker = permissionChecker;
        _audit = audit;
        _clock = clock;
        _options = options;
    }

    public async Task<AuthResponse> Handle(LoginviaSSOCommand request, CancellationToken cancellationToken)
    {
        var portal = request.LoginPortal;

        // Provider gate.
        if (!_options.AllowGoogleSso)
            await FailAsync(null, "unknown", portal, "google_sso_disabled", SecurityEventFailureReasonCodes.SsoProviderError,
                request, AuthErrorCodes.SsoDisabled, "Google sign-in is currently disabled.", 403, cancellationToken);

        var info = await _googleValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (info is null)
            await FailAsync(null, "unknown", portal, "invalid_google_token", SecurityEventFailureReasonCodes.InvalidSsoClaims,
                request, AuthErrorCodes.ExternalAuthFailed, GenericSsoError, 401, cancellationToken);

        var email = info!.Email.Trim().ToLowerInvariant();
        var now = _clock.UtcNow;

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.PrimaryCampus)
            .Include(u => u.Department)
            .Include(u => u.AuthProviders)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // ── VISITOR portal ───────────────────────────────────────────────────
        if (portal == LoginPortals.Visitor)
        {
            if (user is null)
            {
                // Case V1 — first external login at the Visitor portal: auto-provision a VISITOR.
                if (!_options.AutoCreateVisitorOnExternalLogin)
                    await FailAsync(null, email, portal, "visitor_provision_disabled", SecurityEventFailureReasonCodes.VisitorAutoProvisionDisabled,
                        request, AuthErrorCodes.VisitorProvisionDisabled, GenericSsoError, 403, cancellationToken);

                user = await CreateVisitorFromExternalLoginAsync(info, email, now, cancellationToken);
                var newProvider = user.AuthProviders.First(p => p.ProviderType == ProviderTypes.GoogleSso);
                return await IssueAndAuditAsync(user, portal, newProvider, email, now, request, cancellationToken);
            }

            // Case V2/V3 — existing account.
            await EnsureActiveAsync(user, email, portal, request, cancellationToken);

            if (user.Role!.RoleCode != RoleCodes.Visitor)
                await FailAsync(user, email, portal, "wrong_portal_internal_in_visitor", SecurityEventFailureReasonCodes.PortalMismatch,
                    request, AuthErrorCodes.PortalMismatch,
                    "Tài khoản này không được phép đăng nhập tại cổng hiện tại. Vui lòng kiểm tra lại cổng đăng nhập phù hợp.", 403, cancellationToken);

            var provider = await EnsureGoogleProviderLinkedAsync(user, info, now, email, portal, request, cancellationToken);
            return await IssueAndAuditAsync(user, portal, provider, email, now, request, cancellationToken);
        }

        // ── INTERNAL portal ──────────────────────────────────────────────────
        // Campus must be selected first (Case I — campus required).
        if (!request.SelectedCampusId.HasValue)
            await FailAsync(user, email, portal, "missing_campus", SecurityEventFailureReasonCodes.CampusMismatch,
                request, AuthErrorCodes.CampusRequired, "Tài khoản của bạn không có quyền truy cập cơ sở đã chọn. Vui lòng chọn đúng cơ sở được phân quyền.", 400, cancellationToken);

        var selectedCampus = await _db.Campuses.FirstOrDefaultAsync(c => c.CampusId == request.SelectedCampusId.Value, cancellationToken);
        if (selectedCampus is null)
            await FailAsync(user, email, portal, "campus_not_found", SecurityEventFailureReasonCodes.CampusMismatch,
                request, AuthErrorCodes.CampusNotFound, "Tài khoản của bạn không có quyền truy cập cơ sở đã chọn. Vui lòng chọn đúng cơ sở được phân quyền.", 404, cancellationToken);

        if (selectedCampus.Status != "ACTIVE")
            await FailAsync(user, email, portal, "campus_inactive", SecurityEventFailureReasonCodes.CampusMismatch,
                request, AuthErrorCodes.CampusInactive, "Tài khoản của bạn không có quyền truy cập cơ sở đã chọn. Vui lòng chọn đúng cơ sở được phân quyền.", 403, cancellationToken);

        // Case I1 — internal accounts are NEVER auto-provisioned.
        if (user is null)
            await FailAsync(null, email, portal, "internal_account_not_found", SecurityEventFailureReasonCodes.AccountNotFound,
                request, AuthErrorCodes.InternalAccountNotFound,
                "Your internal account has not been created yet. Please contact your Staff Leader or administrator.",
                403, cancellationToken);

        await EnsureActiveAsync(user!, email, portal, request, cancellationToken);

        // Case I2 — Visitor account at the internal portal.
        if (user!.Role!.RoleCode == RoleCodes.Visitor)
            await FailAsync(user, email, portal, "wrong_portal_visitor_in_internal", SecurityEventFailureReasonCodes.PortalMismatch,
                request, AuthErrorCodes.PortalMismatch,
                "Tài khoản này không được phép đăng nhập tại cổng hiện tại. Vui lòng kiểm tra lại cổng đăng nhập phù hợp.", 403, cancellationToken);

        // Case I3 — campus must match the user's primary campus.
        if (user.PrimaryCampusId is not null
            && request.SelectedCampusId != user.PrimaryCampusId)
            await FailAsync(user, email, portal, "campus_mismatch", SecurityEventFailureReasonCodes.CampusMismatch,
                request, AuthErrorCodes.CampusMismatch,
                "Tài khoản của bạn không có quyền truy cập cơ sở đã chọn. Vui lòng chọn đúng cơ sở được phân quyền.", 403, cancellationToken);

        var internalProvider = await EnsureGoogleProviderLinkedAsync(user, info, now, email, portal, request, cancellationToken);
        return await IssueAndAuditAsync(user, portal, internalProvider, email, now, request, cancellationToken);
    }

    /// <summary>Creates a brand-new VISITOR account + linked Google provider and persists it.</summary>
    private async Task<User> CreateVisitorFromExternalLoginAsync(
        GoogleUserInfo info, string email, DateTime now, CancellationToken cancellationToken)
    {
        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.RoleCode == RoleCodes.Visitor
                                      && r.Status == EntityStatuses.Active
                                      && r.DeletedAt == null, cancellationToken)
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
            await FailAsync(user, email, portal, "google_provider_disabled", SecurityEventFailureReasonCodes.SsoProviderError,
                request, AuthErrorCodes.ExternalAuthFailed, GenericSsoError, 403, cancellationToken);

        if (string.IsNullOrEmpty(provider.ProviderSubject))
            provider.ProviderSubject = info.Subject;
        else if (!string.Equals(provider.ProviderSubject, info.Subject, StringComparison.Ordinal))
            await FailAsync(user, email, portal, "google_subject_mismatch", SecurityEventFailureReasonCodes.InvalidSsoClaims,
                request, AuthErrorCodes.ExternalAuthFailed, GenericSsoError, 403, cancellationToken);

        return provider;
    }

    private async Task EnsureActiveAsync(
        User user, string email, string portal, LoginviaSSOCommand request, CancellationToken cancellationToken)
    {
        if (user.LockedUntil is not null && user.LockedUntil > _clock.UtcNow)
            await FailAsync(user, email, portal, "account_locked", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountLocked,
                "Your account is temporarily locked. Please try again later.", 403, cancellationToken);

        if (user.Status != UserStatuses.Active)
            await FailAsync(user, email, portal, $"status_{user.Status}", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountInactive, "Your account is not active.", 403, cancellationToken);

        if (user.Role is null || user.Role.Status != EntityStatuses.Active || user.Role.DeletedAt is not null)
            await FailAsync(user, email, portal, "role_inactive", SecurityEventFailureReasonCodes.AccountDisabled,
                request, AuthErrorCodes.AccountInactive, "Your account is not active.", 403, cancellationToken);
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
            _sessionService, _jwtTokenService, _permissionChecker, cancellationToken);

        await _audit.WriteLoginLogAsync(user.UserId, email, portal, request.SelectedCampusId,
            ProviderTypes.GoogleSso, LoginLogStatuses.Success, null,
            request.IpAddress, request.UserAgent, null, cancellationToken);
        await _audit.WriteSecurityEventAsync(user.UserId, email, SecurityEventTypes.SsoLogin,
            "SUCCESS", null, request.IpAddress, request.UserAgent, 
            portal, request.SelectedCampusId, ProviderTypes.GoogleSso, null, null, cancellationToken);

        return response;
    }

    /// <summary>Writes a failed login log + security event, then throws a coded auth error. Never returns.</summary>
    private async Task FailAsync(
        User? user, string email, string portal, string internalReason, string failureReasonCode,
        LoginviaSSOCommand request, string errorCode, string message, int statusCode,
        CancellationToken cancellationToken)
    {
        await _audit.WriteLoginLogAsync(user?.UserId, email, portal, request.SelectedCampusId,
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
            portal, request.SelectedCampusId, ProviderTypes.GoogleSso, null, internalReason, cancellationToken);

        throw new AuthBusinessException(errorCode, message, statusCode);
    }
}
