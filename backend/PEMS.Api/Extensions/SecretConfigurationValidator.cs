namespace PEMS.Api.Extensions;

/// <summary>
/// Fail-closed start-up validation for runtime secrets.
///
/// PEMS keeps non-sensitive configuration in tracked <c>appsettings*.json</c>, but every real credential
/// (JWT signing key, SMTP password, Google Drive OAuth client secret) must come from the environment —
/// <c>JwtSettings__SecretKey</c>, <c>Smtp__Password</c>, <c>GoogleDrive__ClientSecret</c> — or from a
/// gitignored local override file.
///
/// <para>
/// The Drive REFRESH token is deliberately not on that list any more. It is runtime state held encrypted in
/// the database and replaced through the API-management screen, so requiring it at start-up would keep a
/// host with an expired token from booting into the one screen that can replace it.
/// </para>
///
/// The point of this class is that a missing secret must never silently degrade into a weak default:
/// a production host with no JWT key would otherwise mint tokens signed with an empty/committed string.
/// Validation messages name the configuration KEY only and never echo a value.
/// </summary>
public static class SecretConfigurationValidator
{
    /// <summary>Values that look configured but are placeholders — treated as "not configured".</summary>
    private static readonly string[] PlaceholderMarkers =
    {
        "YOUR_", "CHANGE_ME", "CHANGEME", "REPLACE_ME", "TODO", "PLACEHOLDER", "SUPPLIED AT RUNTIME",
    };

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when a required secret is missing for this environment.
    /// Non-production environments stay permissive so local/dev and tests can run without real credentials —
    /// except for secrets whose owning feature is explicitly switched ON (e.g. SMTP), which must be complete
    /// everywhere or the feature would fail at first use instead of at start-up.
    /// </summary>
    public static void ValidateSecrets(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var missing = new List<string>();

        // JWT signing key: required in Production. Without it every issued token would be forgeable.
        // Length matters too — HS256 rejects anything under 128 bits at SIGNING time, which would other-
        // wise surface as a 500 on the first login rather than a refusal to start.
        if (environment.IsProduction())
        {
            var jwtSecret = configuration["JwtSettings:SecretKey"];
            if (!IsConfigured(jwtSecret))
                missing.Add("JwtSettings:SecretKey");
            else if (System.Text.Encoding.UTF8.GetByteCount(jwtSecret!) < MinimumJwtSecretBytes)
                missing.Add($"JwtSettings:SecretKey (too short — needs at least {MinimumJwtSecretBytes} bytes)");
        }

        // SMTP: only required when the feature is enabled, but then required in EVERY environment —
        // an enabled mailer with no password fails on the first send, which is far harder to diagnose.
        if (configuration.GetValue<bool>("Smtp:Enabled"))
        {
            if (!IsConfigured(configuration["Smtp:User"])) missing.Add("Smtp:User");
            if (!IsConfigured(configuration["Smtp:Password"])) missing.Add("Smtp:Password");
        }

        // Google Drive storage: same rule — only when the integration is switched on.
        //
        // What is required here is the OAuth CLIENT, not the refresh token. The token is runtime state now:
        // it lives encrypted in api_configurations and is replaced by an ADMIN through the API-management
        // screen, so a host with an expired or absent one must still START — that screen is where it gets
        // fixed, and refusing to boot would take away the only way to fix it. RedirectUri joins the list
        // because the reconnect flow cannot be started without it, and a mismatch is rejected by Google at
        // the end of the round-trip rather than at the beginning.
        if (configuration.GetValue<bool>("GoogleDrive:Enabled")
            && string.Equals(configuration["GoogleDrive:AuthMode"], "OAuthUser", StringComparison.OrdinalIgnoreCase)
            && environment.IsProduction())
        {
            if (!IsConfigured(configuration["GoogleDrive:ClientId"])) missing.Add("GoogleDrive:ClientId");
            if (!IsConfigured(configuration["GoogleDrive:ClientSecret"])) missing.Add("GoogleDrive:ClientSecret");
            if (!IsConfigured(configuration["GoogleDrive:RedirectUri"])) missing.Add("GoogleDrive:RedirectUri");
        }

        if (missing.Count == 0)
            return;

        throw new InvalidOperationException(
            $"Missing required configuration for environment '{environment.EnvironmentName}': " +
            $"{string.Join(", ", missing)}. Supply each one via an environment variable " +
            "(replace ':' with '__', e.g. JwtSettings__SecretKey) or a gitignored local settings file. " +
            "Secret values are never read from tracked appsettings files.");
    }

    /// <summary>
    /// HS256 needs at least 128 bits of key material; the signing code rejects anything shorter
    /// (IDX10703 for an empty key, IDX10653 for a short one).
    /// </summary>
    public const int MinimumJwtSecretBytes = 32;

    /// <summary>
    /// Outside Production, supplies a random per-process JWT signing key when none is configured.
    ///
    /// Without this, removing the previously committed key left <c>JwtSettings:SecretKey</c> as an empty
    /// STRING — which is not null, so the existing `?? throw` guards never fire. The host started happily
    /// and then every login blew up inside the token handler with
    /// "IDX10703: ... key length is zero" — a 500 at sign-in instead of an actionable message.
    ///
    /// A random key is strong (never a weak shared default) and keeps `dotnet run` working with no setup.
    /// It changes on every restart, so previously issued tokens and refresh sessions stop validating —
    /// acceptable locally, and the reason Production still REFUSES to start without a real key.
    ///
    /// Returns true when a key was generated.
    /// </summary>
    public static bool TryProvideDevelopmentJwtSecret(IConfigurationBuilder configurationBuilder, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsProduction() || IsConfigured(configuration["JwtSettings:SecretKey"]))
            return false;

        var generated = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(MinimumJwtSecretBytes));

        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = generated,
        });

        return true;
    }

    /// <summary>A secret counts as configured only when it is non-blank and not an obvious placeholder.</summary>
    public static bool IsConfigured(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        foreach (var marker in PlaceholderMarkers)
        {
            if (value.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
