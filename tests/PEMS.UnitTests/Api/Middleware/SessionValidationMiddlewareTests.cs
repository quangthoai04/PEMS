using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using PEMS.Api.Middleware;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Api.Middleware;

/// <summary>
/// Root cause of the "VI login screen shows an English 'Authentication required.' toast" report:
/// this middleware ran BEFORE `UseAuthorization()` and never checked `[AllowAnonymous]`, so a
/// stale-but-syntactically-valid session on the browser's leftover Bearer token 401'd the LOGIN
/// request itself — the new, correct credentials in the body were never even read. Confirmed
/// against the real backend + MySQL before this fix (see the auth 401 audit's network trace):
/// `POST /auth/login` with a valid-signature token pointing at a deleted session returned
/// `{"message":"Your session has been revoked..."}"` with no `errorCode`, and the handler never ran.
///
/// These tests pin both halves of the fix: `[AllowAnonymous]` is now respected (AUTH-02-equivalent),
/// and every 401 this middleware writes now carries a stable `errorCode` the frontend can localize.
/// </summary>
public class SessionValidationMiddlewareTests
{
    private static DefaultHttpContext BuildContext(ClaimsPrincipal? principal, bool allowAnonymous)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        if (principal is not null) context.User = principal;

        var metadata = allowAnonymous
            ? new EndpointMetadataCollection(new AllowAnonymousAttribute())
            : EndpointMetadataCollection.Empty;
        context.SetEndpoint(new Endpoint(null, metadata, "test-endpoint"));
        return context;
    }

    private static ClaimsPrincipal AuthenticatedPrincipal(ulong userId, ulong sessionId) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(PemsClaimTypes.UserId, userId.ToString()),
            new Claim(PemsClaimTypes.SessionId, sessionId.ToString()),
        }, "TestAuth"));

    private static async Task<JsonElement> ReadJsonBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task AllowAnonymous_endpoint_is_never_blocked_by_a_stale_or_revoked_session()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);

        var sessionServiceMock = new Mock<ISessionService>();
        // The session does NOT exist / is not active — exactly what a login page's leftover
        // localStorage token points at after a revoke, a dev-DB reset, or an E2E cleanup script.
        sessionServiceMock
            .Setup(s => s.IsSessionActiveAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var nextCalled = false;
        var middleware = new SessionValidationMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext(AuthenticatedPrincipal(999, 999999), allowAnonymous: true);

        await middleware.InvokeAsync(context, sessionServiceMock.Object, db);

        Assert.True(nextCalled);
        Assert.Equal(200, context.Response.StatusCode); // untouched — DefaultHttpContext's default
        sessionServiceMock.Verify(
            s => s.IsSessionActiveAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AllowAnonymous_endpoint_with_no_principal_at_all_still_passes_through()
    {
        // Guards the pre-existing branch (unauthenticated request) is untouched by the new check.
        var db = DelegationsTestDbContext.Create();
        var sessionServiceMock = new Mock<ISessionService>();
        var nextCalled = false;
        var middleware = new SessionValidationMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext(principal: null, allowAnonymous: true);

        await middleware.InvokeAsync(context, sessionServiceMock.Object, db);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Protected_endpoint_with_revoked_session_still_401s_with_a_stable_errorCode()
    {
        var db = DelegationsTestDbContext.Create();
        var (_, host) = DelegationsTestData.SeedBase(db);

        var sessionServiceMock = new Mock<ISessionService>();
        sessionServiceMock
            .Setup(s => s.IsSessionActiveAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var nextCalled = false;
        var middleware = new SessionValidationMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext(AuthenticatedPrincipal(host.UserId, 999999), allowAnonymous: false);

        await middleware.InvokeAsync(context, sessionServiceMock.Object, db);

        Assert.False(nextCalled);
        Assert.Equal(401, context.Response.StatusCode);
        var body = await ReadJsonBodyAsync(context);
        Assert.Equal(AuthErrorCodes.SessionRevoked, body.GetProperty("errorCode").GetString());
        Assert.False(body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Protected_endpoint_with_unparseable_claims_401s_with_the_generic_unauthorized_code()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);

        var sessionServiceMock = new Mock<ISessionService>();
        var middleware = new SessionValidationMiddleware(_ => Task.CompletedTask);
        // A principal IS authenticated but is missing the session/user claims entirely.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { }, "TestAuth"));
        var context = BuildContext(principal, allowAnonymous: false);

        await middleware.InvokeAsync(context, sessionServiceMock.Object, db);

        Assert.Equal(401, context.Response.StatusCode);
        var body = await ReadJsonBodyAsync(context);
        Assert.Equal(AuthErrorCodes.Unauthorized, body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Protected_endpoint_with_an_active_session_and_active_account_calls_next()
    {
        var db = DelegationsTestDbContext.Create();
        var (_, host) = DelegationsTestData.SeedBase(db);

        var sessionServiceMock = new Mock<ISessionService>();
        sessionServiceMock
            .Setup(s => s.IsSessionActiveAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var nextCalled = false;
        var middleware = new SessionValidationMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = BuildContext(AuthenticatedPrincipal(host.UserId, 1), allowAnonymous: false);

        await middleware.InvokeAsync(context, sessionServiceMock.Object, db);

        Assert.True(nextCalled);
        Assert.Equal(200, context.Response.StatusCode);
    }
}
