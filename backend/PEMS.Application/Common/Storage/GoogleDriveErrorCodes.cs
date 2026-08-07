namespace PEMS.Application.Common.Storage;

/// <summary>
/// Why a Google Drive CONNECTION or WRITE failed, as opposed to why one stored file could not be read
/// (that is <see cref="StorageErrorCodes"/>, and the two are deliberately separate: a caller that gets
/// <see cref="FileNotFound"/> has a data problem, a caller that gets <see cref="ConfigMissing"/> has an
/// operator problem, and telling them apart is the whole job of this file).
///
/// <para>
/// The codes are the strings that were already on the wire before this class existed — the class exists
/// so they stop being typed out by hand at each throw site, which is how
/// <c>GetAccessTokenAsync</c> came to answer a token response with <c>UPLOAD_AVATAR_FAILED</c> during a
/// PDF upload, and how three genuinely different failures (no network, a 500 from Google, a rejected
/// client secret) came to share one sentence: "Không thể kết nối Google Drive. Vui lòng thử lại."
/// That sentence told the Host to retry, which was the correct advice for exactly one of the three.
/// </para>
/// </summary>
public static class GoogleDriveErrorCodes
{
    /// <summary>
    /// A credential the connection cannot be made without is absent from configuration: no refresh
    /// token, no client id, no client secret. Nothing was asked of Google — retrying cannot help, and
    /// the repair belongs to whoever owns the deployment's configuration.
    /// </summary>
    public const string ConfigMissing = "GOOGLE_DRIVE_CONFIG_MISSING";

    /// <summary>
    /// Google answered the token exchange with <c>invalid_grant</c>: the refresh token was revoked, or
    /// it expired (an OAuth app still in "Testing" issues tokens that die after ~7 days). Distinct from
    /// <see cref="AuthFailed"/> because the repair is specific and known — reconnect the account.
    /// </summary>
    public const string TokenExpired = "GOOGLE_DRIVE_TOKEN_EXPIRED";

    /// <summary>
    /// Google reached, and it refused the credentials for a reason that is not an expired grant — a
    /// wrong client secret, a disabled OAuth client, a revoked scope. A configuration fault, so the
    /// message must not invite the end user to keep pressing the button.
    /// </summary>
    public const string AuthFailed = "GOOGLE_DRIVE_AUTH_FAILED";

    /// <summary>
    /// A required folder is not configured for this kind of upload (avatar folder, root folder, parent
    /// folder). Like <see cref="ConfigMissing"/>, nothing reached Google.
    /// </summary>
    public const string NotConnected = "GOOGLE_DRIVE_NOT_CONNECTED";

    /// <summary>
    /// The target folder does not exist, or the connected account cannot see it. Drive answers both
    /// with <c>404</c> and will not distinguish them, so neither does this code.
    /// </summary>
    public const string FolderNotFoundOrNoPermission = "GOOGLE_DRIVE_FOLDER_NOT_FOUND_OR_NO_PERMISSION";

    /// <summary>Drive accepted the request and rejected the write.</summary>
    public const string UploadFailed = "GOOGLE_DRIVE_UPLOAD_FAILED";

    /// <summary>
    /// Drive could not be reached at all, or answered with something no caller can act on: DNS failure,
    /// a dropped connection, a timeout, a 5xx, a 429, or a 200 whose body was not the token response the
    /// protocol promises.
    ///
    /// <para>
    /// The one code here where "vui lòng thử lại" is honest advice — which is precisely why the other
    /// failures must not borrow it. Every incident that arrived as "Không thể kết nối Google Drive.
    /// Vui lòng thử lại." during an outage-free week was one of the codes above wearing this one's
    /// message.
    /// </para>
    /// </summary>
    public const string Unavailable = "GOOGLE_DRIVE_UNAVAILABLE";

    /// <summary>The upload returned <c>200</c> but no file id, so nothing addressable was created.</summary>
    public const string FileIdMissing = "GOOGLE_DRIVE_FILE_URL_MISSING";

    /// <summary>A folder listing/search failed for a reason other than the folder being absent.</summary>
    public const string FolderLookupFailed = "GOOGLE_DRIVE_FOLDER_LOOKUP_FAILED";

    /// <summary>A folder create failed for a reason other than the parent being absent.</summary>
    public const string FolderCreateFailed = "GOOGLE_DRIVE_FOLDER_CREATE_FAILED";

    /// <summary>
    /// A refresh token IS stored in <c>api_configurations.credentials_json_encrypted</c>, and it could not
    /// be read back — the ciphertext failed its AES-GCM authentication tag, or decrypted into something
    /// that is not the credential envelope.
    ///
    /// <para>
    /// Deliberately NOT <see cref="ConfigMissing"/>: the operator did configure a credential, and the two
    /// call for opposite actions. The usual cause is that <c>Security:SecretProtectionKey</c> changed (or
    /// was never pinned, so the key derived from the rotating JWT secret), which no amount of reconnecting
    /// diagnoses if the environment is not fixed first. Falling back to the environment token here would
    /// hide exactly that.
    /// </para>
    /// </summary>
    public const string CredentialUnreadable = "GOOGLE_DRIVE_CREDENTIAL_UNREADABLE";

    // ── ADMIN reconnect (OAuth authorization-code flow) ──────────────────────
    //
    // These describe the CONSENT round-trip, not a Drive call: nothing has been uploaded or read when one
    // of them is raised. They stay separate from the codes above so "the admin cancelled on Google's
    // screen" never arrives looking like "the stored token expired".

    /// <summary>The callback carried no <c>state</c>, or one this deployment did not issue / that was altered.</summary>
    public const string OAuthInvalidState = "GOOGLE_DRIVE_OAUTH_INVALID_STATE";

    /// <summary>The <c>state</c> was ours and authentic, but the admin took longer than its short lifetime.</summary>
    public const string OAuthStateExpired = "GOOGLE_DRIVE_OAUTH_STATE_EXPIRED";

    /// <summary>The admin refused consent on Google's screen. Not a fault — nothing to repair.</summary>
    public const string OAuthAccessDenied = "GOOGLE_DRIVE_OAUTH_ACCESS_DENIED";

    /// <summary>
    /// Google accepted the code and returned no <c>refresh_token</c> — it only issues one when the grant is
    /// new or consent was re-prompted. The previously stored credential is KEPT: overwriting it with
    /// nothing would turn a no-op reconnect into an outage.
    /// </summary>
    public const string OAuthNoRefreshToken = "GOOGLE_DRIVE_OAUTH_NO_REFRESH_TOKEN";

    /// <summary>Google rejected the authorization code exchange (expired code, redirect_uri mismatch, bad client).</summary>
    public const string OAuthTokenExchangeFailed = "GOOGLE_DRIVE_OAUTH_TOKEN_EXCHANGE_FAILED";

    /// <summary>A refresh token was obtained and the database write failed, so nothing was connected.</summary>
    public const string OAuthSaveFailed = "GOOGLE_DRIVE_OAUTH_SAVE_FAILED";
}

/// <summary>
/// The only vocabulary allowed in the <c>?googleDriveOAuth=failed&amp;reason=…</c> query string the callback
/// redirects the browser to. Short, fixed slugs — a URL is written to history, proxy logs and the address
/// bar, so nothing that came back from Google (error descriptions, codes, token payloads) may reach it.
/// The frontend maps each slug to Vietnamese wording; an unknown slug maps to a generic message.
/// </summary>
public static class GoogleDriveOAuthRedirectReasons
{
    public const string AccessDenied = "access_denied";
    public const string InvalidState = "invalid_state";
    public const string StateExpired = "state_expired";
    public const string NoRefreshToken = "no_refresh_token";
    public const string TokenExchangeFailed = "token_exchange_failed";
    public const string SaveFailed = "save_failed";
    public const string ConfigMissing = "config_missing";
}
