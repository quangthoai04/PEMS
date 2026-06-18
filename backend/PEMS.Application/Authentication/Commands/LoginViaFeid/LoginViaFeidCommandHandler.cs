using MediatR;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Application.Authentication.Commands.LoginviaFeid;

/// <summary>
/// Handles FEID (FPT eID) login.
///
/// Reachable today: the provider gate (<c>FEID_DISABLED</c>) and the controlled
/// verifier failure (<c>FEID_NOT_CONFIGURED</c>). No FEID provider is wired yet, so
/// the verifier never returns a real identity — we deliberately do NOT fake a login.
///
/// When a real FEID provider is added, the post-verify branch must apply the SAME
/// dual-portal policy + Visitor auto-provision as <c>LoginviaSSOCommandHandler</c>
/// (campus-required / wrong-portal / campus-mismatch / account-status), then issue
/// via <c>AuthResultBuilder</c>. That logic is intentionally not duplicated here
/// while it is unreachable — see docs/auth/AUTH_CORE_BACKEND_DUAL_PORTAL.md.
/// </summary>
public sealed class LoginviaFeidCommandHandler : IRequestHandler<LoginviaFeidCommand, AuthResponse>
{
    private readonly IFeidIdentityVerifier _feidVerifier;
    private readonly ISecurityAuditService _audit;
    private readonly AuthOptions _options;

    public LoginviaFeidCommandHandler(
        IFeidIdentityVerifier feidVerifier,
        ISecurityAuditService audit,
        AuthOptions options)
    {
        _feidVerifier = feidVerifier;
        _audit = audit;
        _options = options;
    }

    public async Task<AuthResponse> Handle(LoginviaFeidCommand request, CancellationToken cancellationToken)
    {
        var portal = request.LoginPortal;

        // Provider gate — FEID must be explicitly enabled.
        if (!_options.AllowFeid)
        {
            await WriteFailureAsync(portal, LoginLogStatuses.Blocked, "feid_disabled",
                SecurityEventTypes.LoginBlocked, SecuritySeverities.Low, request, cancellationToken);
            throw new AuthBusinessException(
                AuthErrorCodes.FeidDisabled, "FEID sign-in is currently disabled.", 403);
        }

        try
        {
            // Verifies the FEID identity. With no real provider configured this throws
            // AuthBusinessException(FEID_NOT_CONFIGURED, 403) — never a fake success.
            var identity = await _feidVerifier.VerifyAsync(request.IdTokenOrCode, cancellationToken);

            // Unreachable until a real FEID provider returns an identity. Keep it explicit
            // rather than silently faking a login.
            _ = identity;
            throw new AuthBusinessException(
                AuthErrorCodes.FeidNotConfigured,
                "FEID sign-in is not available yet. Please use another sign-in method or contact the administrator.",
                403);
        }
        catch (AuthBusinessException ex)
        {
            await WriteFailureAsync(portal, LoginLogStatuses.Failed, ex.ErrorCode.ToLowerInvariant(),
                SecurityEventTypes.LoginFailed, SecuritySeverities.Low, request, cancellationToken);
            throw;
        }
    }

    private async Task WriteFailureAsync(
        string portal, string logStatus, string reason, string eventType, string severity,
        LoginviaFeidCommand request, CancellationToken cancellationToken)
    {
        // Email is unknown because no verified identity was obtained.
        // Awaited sequentially: both audit writes share the same scoped DbContext.
        await _audit.WriteLoginLogAsync(null, "unknown", portal, request.SelectedCampusId,
            ProviderTypes.FeId, logStatus, reason, request.IpAddress, request.UserAgent, null, cancellationToken);
        await _audit.WriteSecurityEventAsync(null, null, eventType, severity,
            request.IpAddress, request.UserAgent, $"{{\"provider\":\"FEID\",\"reason\":\"{reason}\"}}", cancellationToken);
    }
}
