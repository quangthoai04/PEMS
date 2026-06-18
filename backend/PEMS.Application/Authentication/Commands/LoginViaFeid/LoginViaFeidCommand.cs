using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Authentication.Commands.LoginviaFeid;

/// <summary>
/// FEID (FPT eID) login. Mirrors the SSO/credentials commands' shape. The FEID
/// provider is not yet wired, so this currently resolves to a controlled
/// <c>FEID_NOT_CONFIGURED</c> / <c>FEID_DISABLED</c> error rather than a fake login.
/// </summary>
public sealed class LoginviaFeidCommand : IRequest<AuthResponse>
{
    /// <summary>The FEID id-token or authorization code returned by the FEID provider.</summary>
    public string IdTokenOrCode { get; set; } = string.Empty;
    public string LoginPortal { get; set; } = string.Empty;
    public string? SelectedCampusId { get; set; }

    // Set by the controller from the HTTP context — never bound from the body.
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
