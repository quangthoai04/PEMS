namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Answers one question, once per Drive call: which refresh token should this request use?
///
/// <para>
/// It exists because the answer stopped being a start-up constant. <c>GoogleDriveOptions.RefreshToken</c>
/// is bound when the host starts, so replacing an expired token meant editing
/// <c>appsettings.Development.json</c> or a Railway variable and restarting the process — a redeploy to
/// change a credential that Google expires roughly weekly while the OAuth app is in Testing. The token now
/// lives encrypted in <c>api_configurations.credentials_json_encrypted</c>, where an ADMIN reconnect can
/// replace it and the very next Drive request picks it up.
/// </para>
/// <para>
/// Resolution order is DATABASE first, environment second. The environment fallback is a migration
/// affordance, not a peer: it covers deployments whose ADMIN has not reconnected yet, and is meant to be
/// removed once they have (see the rollout notes in
/// <c>docs/GoogleDrive/07_08_PEMS_GOOGLE_DRIVE_ADMIN_OAUTH_REFRESH_TOKEN_MANAGEMENT_IMPLEMENTATION_PLAN.md</c>).
/// A credential that IS stored but cannot be decrypted is a failure, never a reason to fall back — see
/// <c>GoogleDriveErrorCodes.CredentialUnreadable</c>.
/// </para>
/// </summary>
public interface IGoogleDriveCredentialResolver
{
    /// <summary>
    /// The refresh token to exchange for an access token, or <c>null</c> when this deployment has never
    /// been connected (neither a database credential nor a configured one).
    /// </summary>
    /// <exception cref="PEMS.Application.Common.Exceptions.BusinessRuleException">
    /// A credential is stored and unreadable — <c>GOOGLE_DRIVE_CREDENTIAL_UNREADABLE</c>. Deliberately
    /// fail-closed: silently using the environment token would leave a broken protection key undiagnosed.
    /// </exception>
    Task<string?> ResolveRefreshTokenAsync(CancellationToken cancellationToken = default);
}
