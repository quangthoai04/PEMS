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

namespace PEMS.Application.Authentication.Commands.LoginviaSSO;

public sealed class LoginviaSSOCommandHandler : IRequestHandler<LoginviaSSOCommand, AuthResponse>
{
    // Deliberately generic so SSO never reveals whether an account exists / why it failed.
    private const string GenericSsoError = "Unable to sign in with this account.";

    private readonly IApplicationDbContext _db;
    private readonly IGoogleTokenValidator _googleValidator;
    private readonly ISessionService _sessionService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPermissionChecker _permissionChecker;
    private readonly ISecurityAuditService _audit;
    private readonly IDateTimeService _clock;
    private readonly bool _autoProvision;

    public LoginviaSSOCommandHandler(
        IApplicationDbContext db,
        IGoogleTokenValidator googleValidator,
        ISessionService sessionService,
        IJwtTokenService jwtTokenService,
        IPermissionChecker permissionChecker,
        ISecurityAuditService audit,
        IDateTimeService clock,
        IConfiguration configuration)
    {
        _db = db;
        _googleValidator = googleValidator;
        _sessionService = sessionService;
        _jwtTokenService = jwtTokenService;
        _permissionChecker = permissionChecker;
        _audit = audit;
        _clock = clock;
        _autoProvision = bool.TryParse(configuration["GoogleAuth:AutoProvision"], out var p) && p;
    }

    public async Task<AuthResponse> Handle(LoginviaSSOCommand request, CancellationToken cancellationToken)
    {
        var portal = request.LoginPortal;

        var info = await _googleValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (info is null)
            await FailAsync(null, "unknown", portal, "invalid_google_token", request, cancellationToken);

        var email = info!.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.PrimaryCampus)
            .Include(u => u.Department)
            .Include(u => u.AuthProviders)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // No silent provisioning unless the SSO_PROVISIONED rule is explicitly enabled.
            if (!_autoProvision)
                await FailAsync(null, email, portal, "user_not_found_no_provision", request, cancellationToken);

            // Auto-provisioning is intentionally not implemented (requires role/campus policy).
            await FailAsync(null, email, portal, "auto_provision_not_supported", request, cancellationToken);
        }

        var now = _clock.UtcNow;

        if (user!.Status != UserStatuses.Active)
            await FailAsync(user, email, portal, $"status_{user.Status}", request, cancellationToken);

        if (user.Role is null || user.Role.Status != EntityStatuses.Active || user.Role.DeletedAt is not null)
            await FailAsync(user, email, portal, "role_inactive", request, cancellationToken);

        var isVisitor = user.Role!.RoleCode == RoleCodes.Visitor;
        if ((portal == LoginPortals.Visitor && !isVisitor) || (portal == LoginPortals.Internal && isVisitor))
            await FailAsync(user, email, portal, "wrong_portal", request, cancellationToken);

        // Require an enabled Google provider; link the subject on first use.
        var googleProvider = user.AuthProviders.FirstOrDefault(p => p.ProviderType == ProviderTypes.GoogleSso);
        if (googleProvider is null || !googleProvider.IsEnabled)
            await FailAsync(user, email, portal, "google_provider_missing_or_disabled", request, cancellationToken);

        if (string.IsNullOrEmpty(googleProvider!.ProviderSubject))
            googleProvider.ProviderSubject = info.Subject;
        else if (!string.Equals(googleProvider.ProviderSubject, info.Subject, StringComparison.Ordinal))
            await FailAsync(user, email, portal, "google_subject_mismatch", request, cancellationToken);

        // ── Success ───────────────────────────────────────────────────────────
        googleProvider.LastUsedAt = now;
        googleProvider.ProviderEmail = email;
        user.LastLoginAt = now;
        user.FirstLoginAt ??= now;

        var response = await AuthResultBuilder.IssueAsync(
            user, portal, googleProvider.AuthProviderId, request.IpAddress, request.UserAgent,
            _sessionService, _jwtTokenService, _permissionChecker, cancellationToken);

        await _audit.WriteLoginLogAsync(user.UserId, email, portal, user.PrimaryCampusId,
            ProviderTypes.GoogleSso, LoginLogStatuses.Success, null,
            request.IpAddress, request.UserAgent, null, cancellationToken);
        await _audit.WriteSecurityEventAsync(user.UserId, email, SecurityEventTypes.LoginSuccess,
            SecuritySeverities.Low, request.IpAddress, request.UserAgent, null, cancellationToken);

        return response;
    }

    private async Task FailAsync(
        User? user, string email, string portal, string internalReason,
        LoginviaSSOCommand request, CancellationToken cancellationToken)
    {
        await _audit.WriteLoginLogAsync(user?.UserId, email, portal, user?.PrimaryCampusId,
            ProviderTypes.GoogleSso, LoginLogStatuses.Failed, internalReason,
            request.IpAddress, request.UserAgent, null, cancellationToken);
        await _audit.WriteSecurityEventAsync(user?.UserId, email, SecurityEventTypes.LoginFailed,
            SecuritySeverities.Low, request.IpAddress, request.UserAgent,
            $"{{\"reason\":\"{internalReason}\"}}", cancellationToken);

        throw new AuthenticationFailedException(GenericSsoError, internalReason);
    }
}
