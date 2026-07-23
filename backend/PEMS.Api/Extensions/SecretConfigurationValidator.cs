namespace PEMS.Api.Extensions;

/// <summary>
/// Fail-closed start-up validation for runtime secrets.
///
/// PEMS keeps non-sensitive configuration in tracked <c>appsettings*.json</c>, but every real credential
/// (JWT signing key, SMTP password, Google Drive OAuth client secret / refresh token) must come from the
/// environment — <c>JwtSettings__SecretKey</c>, <c>Smtp__Password</c>, <c>GoogleDrive__ClientSecret</c>,
/// <c>GoogleDrive__RefreshToken</c> — or from a gitignored local override file.
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
        if (environment.IsProduction() && !IsConfigured(configuration["JwtSettings:SecretKey"]))
            missing.Add("JwtSettings:SecretKey");

        // SMTP: only required when the feature is enabled, but then required in EVERY environment —
        // an enabled mailer with no password fails on the first send, which is far harder to diagnose.
        if (configuration.GetValue<bool>("Smtp:Enabled"))
        {
            if (!IsConfigured(configuration["Smtp:User"])) missing.Add("Smtp:User");
            if (!IsConfigured(configuration["Smtp:Password"])) missing.Add("Smtp:Password");
        }

        // Google Drive storage: same rule — only when the integration is switched on.
        if (configuration.GetValue<bool>("GoogleDrive:Enabled")
            && string.Equals(configuration["GoogleDrive:AuthMode"], "OAuthUser", StringComparison.OrdinalIgnoreCase)
            && environment.IsProduction())
        {
            if (!IsConfigured(configuration["GoogleDrive:ClientSecret"])) missing.Add("GoogleDrive:ClientSecret");
            if (!IsConfigured(configuration["GoogleDrive:RefreshToken"])) missing.Add("GoogleDrive:RefreshToken");
        }

        if (missing.Count == 0)
            return;

        throw new InvalidOperationException(
            $"Missing required configuration for environment '{environment.EnvironmentName}': " +
            $"{string.Join(", ", missing)}. Supply each one via an environment variable " +
            "(replace ':' with '__', e.g. JwtSettings__SecretKey) or a gitignored local settings file. " +
            "Secret values are never read from tracked appsettings files.");
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
