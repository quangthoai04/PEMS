namespace PEMS.Api.Extensions;

/// <summary>
/// Fail-closed start-up validation for the two public base URLs the app stamps into outbound content:
/// <c>App:PublicApiBaseUrl</c> (the domain public email-action links point at — see
/// <c>IEmailActionTokenService.BuildPublicActionUrl</c>) and <c>App:FrontendBaseUrl</c> (the domain
/// deep-links into the SPA point at — see <c>EmailComposition</c>/<c>EmailActionTemplates</c>).
///
/// <para>
/// Outside Production these are permissive: local dev and the test suite run against
/// <c>localhost</c>/<c>127.0.0.1</c> by design, and requiring a real domain there would break every
/// contributor's inner loop for no safety gained. In Production, though, an unset or malformed value here
/// does not fail loudly — it silently mints a link that points nowhere real (or, worse, at
/// <c>localhost</c> on the SERVER, which no recipient's browser can ever reach), and that only surfaces
/// once a real visitor clicks a real email days later. Refusing to start is far cheaper than that.
/// </para>
/// </summary>
public static class BaseUrlConfigurationValidator
{
    private static readonly string[] LocalHosts = { "localhost", "127.0.0.1", "::1", "0.0.0.0" };

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> in Production when either base URL is missing, not
    /// an absolute URI, not HTTPS, or points at a loopback/local address. A no-op everywhere else
    /// (Development, Testing) so the local/dev inner loop and the test suite are unaffected.
    /// </summary>
    public static void ValidateBaseUrls(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (!environment.IsProduction())
            return;

        var problems = new List<string>();

        CheckOne(configuration["App:PublicApiBaseUrl"], "App:PublicApiBaseUrl", problems);
        CheckOne(configuration["App:FrontendBaseUrl"], "App:FrontendBaseUrl", problems);

        if (problems.Count == 0)
            return;

        throw new InvalidOperationException(
            $"Invalid base URL configuration for environment '{environment.EnvironmentName}': " +
            $"{string.Join("; ", problems)}. Each must be an absolute https:// URL pointing at a real, " +
            "public domain — not empty, not localhost/127.0.0.1, not http://.");
    }

    private static void CheckOne(string? value, string key, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            problems.Add($"{key} is not set");
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            problems.Add($"{key} ('{value}') is not a valid absolute URL");
            return;
        }

        if (Array.IndexOf(LocalHosts, uri.Host) >= 0)
        {
            problems.Add($"{key} ('{value}') points at a loopback/local address");
            return;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            problems.Add($"{key} ('{value}') must use https://, not '{uri.Scheme}://'");
        }
    }
}
