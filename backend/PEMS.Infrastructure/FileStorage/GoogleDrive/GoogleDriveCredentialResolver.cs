using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;

namespace PEMS.Infrastructure.FileStorage.GoogleDrive;

/// <summary>
/// Reads the current Google Drive refresh token: the encrypted one in <c>api_configurations</c> if an ADMIN
/// has connected this deployment, otherwise the configured one.
///
/// <para>
/// Read per call, never cached. That is the point of the class — the token used to be a field on an options
/// object captured at start-up, so an operator who obtained a fresh one still had to restart the process
/// (or redeploy, in production) before a single upload could use it. Nothing here holds state between
/// calls, so the request after a reconnect sees the new credential.
/// </para>
/// </summary>
public sealed class GoogleDriveCredentialResolver : IGoogleDriveCredentialResolver
{
    private readonly IApplicationDbContext _db;
    private readonly ISecretProtector _secretProtector;
    private readonly GoogleDriveOptions _options;
    private readonly ILogger<GoogleDriveCredentialResolver> _logger;

    public GoogleDriveCredentialResolver(
        IApplicationDbContext db,
        ISecretProtector secretProtector,
        IOptions<GoogleDriveOptions> options,
        ILogger<GoogleDriveCredentialResolver> logger)
    {
        _db = db;
        _secretProtector = secretProtector;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> ResolveRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        // Only the ciphertext leaves the database: this runs on every Drive call, and the rest of the row
        // (quotas, test history, settings) is of no interest to a token exchange.
        var ciphertext = await _db.ApiConfigurations
            .AsNoTracking()
            .Where(c => c.ApiCode == GoogleDriveIntegrationConstants.ApiCode && c.DeletedAt == null)
            .Select(c => c.CredentialsJsonEncrypted)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(ciphertext))
            return ReadStoredRefreshToken(ciphertext!);

        // Never connected through the console. The configured value is the migration path, and the reason
        // this deployment still starts and still uploads while its ADMIN has not pressed reconnect yet.
        return string.IsNullOrWhiteSpace(_options.RefreshToken) ? null : _options.RefreshToken;
    }

    /// <summary>
    /// Decrypts the stored envelope, or refuses. It does NOT fall through to
    /// <see cref="GoogleDriveOptions.RefreshToken"/>: a credential that exists and cannot be read means the
    /// protection key changed (typically <c>Security:SecretProtectionKey</c> unset, so the key was derived
    /// from a JWT secret that has since rotated). Quietly using the environment token would leave every
    /// upload working, the console showing "connected", and the actual fault invisible until the day the
    /// environment variable is removed — which is the last step of this very rollout.
    /// </summary>
    private string ReadStoredRefreshToken(string ciphertext)
    {
        string plaintext;
        try
        {
            plaintext = _secretProtector.Unprotect(ciphertext);
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException
                                       or System.Security.Cryptography.CryptographicException)
        {
            // Type of failure only — the exception message and the ciphertext both stay out of the log.
            _logger.LogError(
                "Stored Google Drive credential could not be decrypted ({Failure}). Check that "
                + "Security:SecretProtectionKey is the key the credential was written with.",
                ex.GetType().Name);
            throw Unreadable();
        }

        var refreshToken = GoogleDriveCredentialEnvelope.TryParse(plaintext)?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogError(
                "Stored Google Drive credential decrypted but carried no refresh token. "
                + "Reconnect Google Drive from API management to rewrite it.");
            throw Unreadable();
        }

        return refreshToken!;
    }

    private static BusinessRuleException Unreadable() => new(
        "Không đọc được thông tin kết nối Google Drive đã lưu. Vui lòng kết nối lại Google Drive.",
        GoogleDriveErrorCodes.CredentialUnreadable);
}
