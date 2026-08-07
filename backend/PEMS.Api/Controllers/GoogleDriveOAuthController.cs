using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.ApiIntegrations.Commands.CompleteGoogleDriveOAuth;

namespace PEMS.Api.Controllers;

/// <summary>
/// Where Google sends the ADMIN's browser back after consent. One action, and it is a redirect: everything
/// that decides anything happens in <see cref="CompleteGoogleDriveOAuthCommandHandler"/>.
///
/// <para>
/// This used to be a DEV-only utility that rendered the <c>refresh_token</c> on an HTML page for an operator
/// to copy into <c>appsettings.Development.json</c> — and, in production, into a Railway variable followed by
/// a redeploy, because Google expires the token of an app still in "Testing" roughly weekly. It also carried
/// a <c>connect</c> action under a class-level <c>[AllowAnonymous]</c>, so anyone who could reach the host
/// could start a consent flow for the shared Drive account. Both are gone: the flow is started from the
/// ADMIN-gated <c>POST /api/api-integrations/google-drive/oauth/start</c>, and the token is encrypted into
/// the database without ever being displayed.
/// </para>
/// <para>
/// The controller stays <c>[AllowAnonymous]</c> because a browser redirect from Google carries no
/// Authorization header — there is no session to authenticate against. The <c>state</c> parameter is what
/// makes that safe, and the handler verifies it before spending the authorization code.
/// </para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/google-drive/oauth")]
public sealed class GoogleDriveOAuthController : ControllerBase
{
    /// <summary>Where the console lives, when no frontend base URL is configured (local dev default).</summary>
    private const string DefaultFrontendBaseUrl = "http://localhost:5173";

    /// <summary>The API-management screen, which reads the result off the query string and toasts it.</summary>
    private const string ConsolePath = "/dashboard/apis";

    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public GoogleDriveOAuthController(IMediator mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

    /// <summary>
    /// Exchanges the authorization code for a refresh token, stores it encrypted, and returns the ADMIN to
    /// the console. The redirect carries a result word and, on failure, one fixed slug — never a token, a
    /// code, or Google's error description, because a URL ends up in browser history and proxy logs.
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? error,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new CompleteGoogleDriveOAuthCommand(code, error, state), cancellationToken);

        var baseUrl = (_configuration["App:FrontendBaseUrl"] ?? DefaultFrontendBaseUrl).TrimEnd('/');

        return Redirect(result.Success
            ? $"{baseUrl}{ConsolePath}?googleDriveOAuth=success"
            : $"{baseUrl}{ConsolePath}?googleDriveOAuth=failed&reason={Uri.EscapeDataString(result.Reason ?? "unknown")}");
    }
}
