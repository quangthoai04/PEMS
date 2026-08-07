using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.ApiIntegrations.Commands.CompleteGoogleDriveOAuth;

/// <summary>
/// Turns Google's callback into a stored credential: verify the state, exchange the code, encrypt the
/// refresh token into <c>api_configurations.credentials_json_encrypted</c>, audit it.
///
/// <para>
/// This replaces a DEV-only page that rendered the refresh token as HTML for an operator to copy into
/// <c>appsettings.Development.json</c> — and, in production, into a Railway variable followed by a redeploy.
/// Nothing here returns, logs, or redirects with a token; the browser is sent back to the console with a
/// result word and, on failure, one fixed slug.
/// </para>
/// <para>
/// The order of checks is the security property, not a style choice: the code is exchanged only AFTER the
/// state authenticates. An endpoint that exchanged first would spend a stranger's code against this
/// deployment's client credentials before deciding whether to trust them.
/// </para>
/// </summary>
public sealed class CompleteGoogleDriveOAuthCommandHandler
    : IRequestHandler<CompleteGoogleDriveOAuthCommand, GoogleDriveOAuthCallbackResultDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISecretProtector _secretProtector;
    private readonly IGoogleDriveOAuthStateService _stateService;
    private readonly IDateTimeService _clock;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleDriveOptions _options;
    private readonly ILogger<CompleteGoogleDriveOAuthCommandHandler> _logger;

    public CompleteGoogleDriveOAuthCommandHandler(
        IApplicationDbContext db,
        ISecretProtector secretProtector,
        IGoogleDriveOAuthStateService stateService,
        IDateTimeService clock,
        IHttpClientFactory httpClientFactory,
        IOptions<GoogleDriveOptions> options,
        ILogger<CompleteGoogleDriveOAuthCommandHandler> logger)
    {
        _db = db;
        _secretProtector = secretProtector;
        _stateService = stateService;
        _clock = clock;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GoogleDriveOAuthCallbackResultDto> Handle(
        CompleteGoogleDriveOAuthCommand request, CancellationToken cancellationToken)
    {
        // 1-3. Nothing is trusted, and nothing is spent, until the state authenticates.
        var validation = _stateService.Validate(request.State);
        if (!validation.IsValid || validation.State is null)
        {
            _logger.LogWarning(
                "Google Drive OAuth callback refused before any token exchange: {Reason}.",
                validation.FailureReason);
            return Failure(validation.FailureReason ?? GoogleDriveOAuthRedirectReasons.InvalidState);
        }

        var adminUserId = validation.State.AdminUserId;

        // 4. Google refused, or the admin declined on the consent screen. Its own outcome — there is
        //    nothing to repair and nothing to overwrite.
        if (!string.IsNullOrWhiteSpace(request.Error))
        {
            _logger.LogInformation(
                "Google Drive OAuth consent did not complete for admin {AdminUserId} (error={Error}).",
                adminUserId, Sanitize(request.Error!));
            return Failure(string.Equals(request.Error, "access_denied", StringComparison.OrdinalIgnoreCase)
                ? GoogleDriveOAuthRedirectReasons.AccessDenied
                : GoogleDriveOAuthRedirectReasons.TokenExchangeFailed);
        }

        if (string.IsNullOrWhiteSpace(request.Code))
            return Failure(GoogleDriveOAuthRedirectReasons.TokenExchangeFailed);

        // 5. Server-side configuration. Checked here rather than at start-up because a deployment must be
        //    able to boot with no credential at all — reconnecting is what this screen is for.
        if (string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret)
            || string.IsNullOrWhiteSpace(_options.RedirectUri))
        {
            _logger.LogError(
                "Google Drive OAuth callback cannot exchange a code: ClientId/ClientSecret/RedirectUri "
                + "are not fully configured on this host.");
            return Failure(GoogleDriveOAuthRedirectReasons.ConfigMissing);
        }

        // 6-7. Exchange, and read exactly one field out of the response.
        var refreshToken = await ExchangeAuthorizationCodeAsync(request.Code!, cancellationToken);
        if (refreshToken is null)
            return Failure(GoogleDriveOAuthRedirectReasons.TokenExchangeFailed);

        if (refreshToken.Length == 0)
        {
            // Google accepted the code and issued no refresh token (a re-grant it did not consider new).
            // The stored credential is untouched: replacing a working token with nothing would turn a
            // pointless reconnect into an outage across every upload path in the system.
            _logger.LogWarning(
                "Google Drive OAuth returned no refresh token for admin {AdminUserId}; the stored "
                + "credential was left unchanged.", adminUserId);
            return Failure(GoogleDriveOAuthRedirectReasons.NoRefreshToken);
        }

        // 8-9. Connected only once the write succeeds.
        return await SaveCredentialAsync(refreshToken, adminUserId, cancellationToken);
    }

    /// <summary>
    /// Posts the authorization code to Google. Returns the refresh token, an EMPTY string when Google
    /// succeeded but issued none, or <c>null</c> when the exchange itself failed.
    ///
    /// <para>
    /// Nothing from this method reaches a log: not the code, not the client secret, not the response body.
    /// A token endpoint's payload is the one place a credential is guaranteed to be, and the OAuth
    /// <c>error</c> field alone is enough to tell an expired code from a redirect_uri mismatch.
    /// </para>
    /// </summary>
    private async Task<string?> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId!,
            ["client_secret"] = _options.ClientSecret!,
            ["redirect_uri"] = _options.RedirectUri!,
            ["grant_type"] = "authorization_code",
        });

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(
                GoogleDriveIntegrationConstants.TokenUrl, form, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Google Drive OAuth token exchange could not reach the token endpoint.");
            return null;
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Google Drive OAuth token exchange returned {Status} (error={Error}).",
                    (int)response.StatusCode, DescribeTokenError(body));
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var token = doc.RootElement.TryGetProperty("refresh_token", out var el)
                            && el.ValueKind == JsonValueKind.String
                    ? el.GetString()
                    : null;

                return string.IsNullOrWhiteSpace(token) ? string.Empty : token!;
            }
            catch (JsonException ex)
            {
                // Deliberately does not log the body — on this path it is a 200 from a token endpoint.
                _logger.LogError(ex, "Google Drive OAuth token response was not valid JSON.");
                return null;
            }
        }
    }

    /// <summary>
    /// Encrypts the token into the well-known Drive row, creating that row only if the deployment has none,
    /// and clears the test verdict — the credential just changed, so the previous SUCCESS describes a token
    /// that no longer exists and would otherwise sit on the card as if it still did.
    /// </summary>
    private async Task<GoogleDriveOAuthCallbackResultDto> SaveCredentialAsync(
        string refreshToken, ulong adminUserId, CancellationToken cancellationToken)
    {
        var now = _clock.VietnamNow;
        var config = await _db.ApiConfigurations.FirstOrDefaultAsync(
            c => c.ApiCode == GoogleDriveIntegrationConstants.ApiCode && c.DeletedAt == null,
            cancellationToken);

        var isFirstConnect = config is null || string.IsNullOrEmpty(config.CredentialsJsonEncrypted);

        if (config is null)
        {
            // Matched on api_code, which is UNIQUE — so this branch means the deployment genuinely has no
            // Drive row, never that a second one is about to appear beside the seeded one.
            config = new ApiConfiguration
            {
                ApiCode = GoogleDriveIntegrationConstants.ApiCode,
                Name = GoogleDriveIntegrationConstants.Name,
                ProviderName = GoogleDriveIntegrationConstants.ProviderName,
                Purpose = GoogleDriveIntegrationConstants.Purpose,
                BaseUrl = GoogleDriveIntegrationConstants.BaseUrl,
                DefaultMethod = "POST",
                AuthType = "OAUTH2",
                OauthClientId = _options.ClientId,
                OauthTokenUrl = GoogleDriveIntegrationConstants.TokenUrl,
                OauthScope = GoogleDriveIntegrationConstants.Scope,
                DataSensitivity = "CONFIDENTIAL",
                TimeoutSeconds = 30,
                Status = ApiIntegrationStatuses.Active,
                CreatedAt = now,
                CreatedBy = adminUserId,
            };
            _db.ApiConfigurations.Add(config);
        }

        config.CredentialsJsonEncrypted = _secretProtector.Protect(
            new GoogleDriveCredentialEnvelope { RefreshToken = refreshToken }.ToJson());
        config.LastTestStatus = null;
        config.LastTestedAt = null;
        config.LastTestMessage = null;
        config.UpdatedAt = now;
        config.UpdatedBy = adminUserId;

        try
        {
            // Two writes in one transaction, because the audit row needs the config's identity: on a
            // first-ever connect api_config_id does not exist until the row is inserted, and an audit
            // entry pointing at 0 would name nothing. Committing them together keeps the invariant that
            // a stored credential change is always accompanied by the record of who made it.
            await using var transaction = await _db.BeginTransactionAsync(cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = adminUserId,
                Action = isFirstConnect
                    ? GoogleDriveIntegrationConstants.AuditConnect
                    : GoogleDriveIntegrationConstants.AuditReconnect,
                EntityType = GoogleDriveIntegrationConstants.AuditEntityType,
                EntityId = config.ApiConfigId,
                CreatedAt = now,
            });
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The token exists at Google and is not stored here. It cannot be un-issued from this path, and
            // revoking it would need a second network call on an already-failing request — so the honest
            // answer is that the reconnect did not happen, and pressing it again is safe.
            _logger.LogError(ex, "Google Drive OAuth credential could not be saved for admin {AdminUserId}.",
                adminUserId);
            return Failure(GoogleDriveOAuthRedirectReasons.SaveFailed);
        }

        _logger.LogInformation(
            "Google Drive credential {Action} by admin {AdminUserId} (api_config_id={ApiConfigId}).",
            isFirstConnect ? "connected" : "reconnected", adminUserId, config.ApiConfigId);

        return new GoogleDriveOAuthCallbackResultDto { Success = true };
    }

    private static GoogleDriveOAuthCallbackResultDto Failure(string reason)
        => new() { Success = false, Reason = reason };

    /// <summary>The <c>error</c> field of an OAuth error body — enough to diagnose, without the payload.</summary>
    private static string DescribeTokenError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("error", out var el) ? el.GetString() ?? "?" : "?";
        }
        catch (JsonException)
        {
            return "(unparseable)";
        }
    }

    /// <summary>
    /// Google's <c>error</c> query value is attacker-influenceable text arriving on an anonymous endpoint.
    /// Clipped and stripped of anything that could forge a line in the log.
    /// </summary>
    private static string Sanitize(string value)
    {
        var clipped = value.Length > 40 ? value[..40] : value;
        return new string(clipped.Where(char.IsLetterOrDigit).ToArray());
    }
}
