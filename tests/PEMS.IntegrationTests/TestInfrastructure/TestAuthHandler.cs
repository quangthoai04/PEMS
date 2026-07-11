using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Security;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Fake authentication scheme used only by <see cref="PemsWebApplicationFactory"/>.
/// Reads identity claims from request headers instead of validating a real JWT, so
/// Integration Tests can simulate any role (HO, STAFF, ADMIN, VISITOR, ...) without a
/// real login flow or a real Google SSO/JWT secret.
///
/// Absence of <see cref="UserIdHeader"/> means "anonymous" — used to exercise the
/// 401 Unauthorized path exactly like a request with no bearer token would.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public const string UserIdHeader = "X-Test-UserId";
    public const string RoleCodeHeader = "X-Test-RoleCode";
    public const string SubRoleHeader = "X-Test-SubRole";
    public const string SessionIdHeader = "X-Test-SessionId";
    public const string PrimaryCampusIdHeader = "X-Test-PrimaryCampusId";
    public const string DepartmentIdHeader = "X-Test-DepartmentId";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userId) || string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(PemsClaimTypes.UserId, userId!),
        };

        if (Request.Headers.TryGetValue(RoleCodeHeader, out var roleCode) && !string.IsNullOrWhiteSpace(roleCode))
            claims.Add(new Claim(PemsClaimTypes.RoleCode, roleCode!));

        if (Request.Headers.TryGetValue(SubRoleHeader, out var subRole) && !string.IsNullOrWhiteSpace(subRole))
            claims.Add(new Claim(PemsClaimTypes.SubRole, subRole!));

        if (Request.Headers.TryGetValue(SessionIdHeader, out var sessionId) && !string.IsNullOrWhiteSpace(sessionId))
            claims.Add(new Claim(PemsClaimTypes.SessionId, sessionId!));

        if (Request.Headers.TryGetValue(PrimaryCampusIdHeader, out var primaryCampusId) && !string.IsNullOrWhiteSpace(primaryCampusId))
            claims.Add(new Claim(PemsClaimTypes.PrimaryCampusId, primaryCampusId!));

        if (Request.Headers.TryGetValue(DepartmentIdHeader, out var departmentId) && !string.IsNullOrWhiteSpace(departmentId))
            claims.Add(new Claim(PemsClaimTypes.DepartmentId, departmentId!));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
