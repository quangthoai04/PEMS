using MediatR;
using Microsoft.Extensions.Options;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;

namespace PEMS.Application.ApiIntegrations.Commands.StartGoogleDriveOAuth;

/// <summary>
/// Builds the Google consent URL for an ADMIN reconnect.
///
/// <para>
/// The URL is RETURNED rather than redirected to, because the caller is an XHR from the API-management
/// screen: a 302 answered to <c>fetch</c>/axios would be followed by the HTTP stack, and the browser would
/// end up parsing Google's HTML instead of navigating to it. The frontend performs the navigation.
/// </para>
/// </summary>
public sealed class StartGoogleDriveOAuthCommandHandler
    : IRequestHandler<StartGoogleDriveOAuthCommand, GoogleDriveOAuthStartResultDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGoogleDriveOAuthStateService _stateService;
    private readonly GoogleDriveOptions _options;

    public StartGoogleDriveOAuthCommandHandler(
        ICurrentUserService currentUser,
        IGoogleDriveOAuthStateService stateService,
        IOptions<GoogleDriveOptions> options)
    {
        _currentUser = currentUser;
        _stateService = stateService;
        _options = options.Value;
    }

    public Task<GoogleDriveOAuthStartResultDto> Handle(
        StartGoogleDriveOAuthCommand request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureManage(_currentUser);

        // EnsureManage already refused an anonymous caller; this is the compiler's null check, and the
        // reason the state can name a real user.
        var adminUserId = _currentUser.UserId
            ?? throw new AuthBusinessException(
                ApiIntegrationErrorCodes.Forbidden, "Chỉ ADMIN mới được quản lý cấu hình API.", 403);

        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.RedirectUri))
            throw new BusinessRuleException(
                "Google Drive chưa được cấu hình OAuth (ClientId/RedirectUri) trên máy chủ.",
                GoogleDriveErrorCodes.ConfigMissing);

        var query = new (string Key, string Value)[]
        {
            ("client_id", _options.ClientId!),
            ("redirect_uri", _options.RedirectUri!),
            ("response_type", "code"),
            ("scope", GoogleDriveIntegrationConstants.Scope),
            // offline is what makes Google issue a refresh token at all; consent is what makes it issue a
            // NEW one on a re-grant, which is the entire point of a reconnect — without it Google answers a
            // returning account with an access token only, and the expired credential stays expired.
            ("access_type", "offline"),
            ("prompt", "consent"),
            ("state", _stateService.Create(adminUserId)),
        };

        var queryString = string.Join(
            '&', query.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        // The client SECRET is never part of this URL — it belongs only in the server-side code exchange.
        return Task.FromResult(new GoogleDriveOAuthStartResultDto
        {
            AuthorizationUrl = $"{GoogleDriveIntegrationConstants.AuthorizationEndpoint}?{queryString}",
        });
    }
}
