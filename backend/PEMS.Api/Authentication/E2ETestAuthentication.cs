using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Security;

namespace PEMS.Api.Authentication;

/// <summary>
/// Registration + validation gate for the TESTING-ONLY real-stack E2E authentication scheme. It is the
/// authenticated sibling of <c>FileSinkEmailService</c> and is deliberately NOT the header-trusting
/// <c>TestAuthHandler</c> used by the in-process WAF: that one lets the caller assert any role/campus via a
/// header, which must never reach a running host. Here the browser sends ONLY an opaque profile key + a
/// run-scoped secret; the identity (user/role/campus/department/email) is resolved SERVER-SIDE from a
/// seeded profile file. Every gate is fail-closed.
/// </summary>
public static class E2ETestAuthGate
{
    public const string SchemeName = "E2ETest";

    /// <summary>Must equal "true" (case-insensitive) — the explicit opt-in, separate from the environment.</summary>
    public const string EnabledEnvVar = "PEMS_E2E_TEST_AUTH_ENABLED";
    /// <summary>The run-scoped shared secret the orchestration generates; required and non-blank.</summary>
    public const string SecretEnvVar = "PEMS_E2E_TEST_AUTH_SECRET";
    /// <summary>Path to the server-side seeded profile JSON the orchestration writes; required and non-blank.</summary>
    public const string ProfilesEnvVar = "PEMS_E2E_TEST_AUTH_PROFILES";

    /// <summary>Browser sends only these two: an opaque profile key and the run secret. Never a role/campus.</summary>
    public const string ProfileHeader = "X-E2E-Profile";
    public const string SecretHeader = "X-E2E-Secret";

    /// <summary>
    /// True ONLY when ALL hold: the environment is Testing, the explicit gate is "true", a non-blank secret is
    /// configured, AND a profile file path is configured. Requiring the secret + path here (not just at use)
    /// keeps a partially-configured host from ever registering an open scheme (fail-closed + parallel-safe).
    /// </summary>
    public static bool IsEnabledFor(string? environmentName)
        => string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase)
           && string.Equals(Environment.GetEnvironmentVariable(EnabledEnvVar), "true", StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SecretEnvVar))
           && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ProfilesEnvVar));

    /// <summary>Constant-time secret comparison (both hashed to a fixed length first, so length never leaks).</summary>
    public static bool SecretMatches(string? provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected)) return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}

/// <summary>A server-side seeded identity. The browser never supplies any of these fields — only the key.</summary>
public sealed record E2ETestProfile(
    string Key,
    ulong UserId,
    string RoleCode,
    string? SubRole = null,
    ulong? PrimaryCampusId = null,
    ulong? DepartmentId = null,
    string? Email = null,
    ulong? SessionId = null);

/// <summary>
/// Loads the E2E profiles from the JSON file at <see cref="E2ETestAuthGate.ProfilesEnvVar"/> (written by the
/// orchestration with the disposable DB's seeded user/role/campus values). Fail-closed: a missing path,
/// missing file, or parse error yields an EMPTY store, so every profile key resolves to "unknown" →
/// unauthorized. The store is the ONLY source of role/campus — a key can never carry more authority than the
/// seed granted it.
/// </summary>
public sealed class E2ETestProfileStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly Dictionary<string, E2ETestProfile> _byKey;

    public E2ETestProfileStore(ILogger<E2ETestProfileStore> logger)
    {
        _byKey = Load(logger);
    }

    private static Dictionary<string, E2ETestProfile> Load(ILogger logger)
    {
        var empty = new Dictionary<string, E2ETestProfile>(StringComparer.Ordinal);
        var path = Environment.GetEnvironmentVariable(E2ETestAuthGate.ProfilesEnvVar);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return empty; // fail-closed: no profiles → everything is "unknown"
        try
        {
            var profiles = JsonSerializer.Deserialize<List<E2ETestProfile>>(File.ReadAllText(path), Json)
                           ?? new List<E2ETestProfile>();
            var map = new Dictionary<string, E2ETestProfile>(StringComparer.Ordinal);
            foreach (var p in profiles)
                if (!string.IsNullOrWhiteSpace(p.Key)) map[p.Key] = p;
            return map;
        }
        catch (Exception ex)
        {
            // Never log profile contents (seeded identities) — only that loading failed.
            logger.LogError(ex, "E2E test profile file could not be parsed; running with zero profiles (fail-closed).");
            return empty;
        }
    }

    public bool TryResolve(string key, out E2ETestProfile profile) => _byKey.TryGetValue(key, out profile!);
}

/// <summary>
/// Fail-closed authentication handler for real-stack E2E. Registered ONLY when
/// <see cref="E2ETestAuthGate.IsEnabledFor"/> is true (see AuthenticationExtensions). It re-checks the gate
/// on every request (defense in depth), validates the run secret in constant time, and resolves the profile
/// key against the server-side seeded store — never trusting a role/campus header.
/// </summary>
public sealed class E2ETestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IHostEnvironment _environment;
    private readonly E2ETestProfileStore _profiles;

    public E2ETestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHostEnvironment environment,
        E2ETestProfileStore profiles)
        : base(options, logger, encoder)
    {
        _environment = environment;
        _profiles = profiles;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Defense in depth: never authenticate unless the gate is still fully open.
        if (!E2ETestAuthGate.IsEnabledFor(_environment.EnvironmentName))
            return Task.FromResult(AuthenticateResult.NoResult());

        // No profile header → treat as anonymous (401 on protected endpoints, like a missing bearer token).
        if (!Request.Headers.TryGetValue(E2ETestAuthGate.ProfileHeader, out var profileKey)
            || string.IsNullOrWhiteSpace(profileKey))
            return Task.FromResult(AuthenticateResult.NoResult());

        var providedSecret = Request.Headers.TryGetValue(E2ETestAuthGate.SecretHeader, out var s) ? s.ToString() : null;
        var expectedSecret = Environment.GetEnvironmentVariable(E2ETestAuthGate.SecretEnvVar);
        if (!E2ETestAuthGate.SecretMatches(providedSecret, expectedSecret))
            return Task.FromResult(AuthenticateResult.Fail("Invalid E2E test secret."));

        if (!_profiles.TryResolve(profileKey.ToString(), out var profile))
            return Task.FromResult(AuthenticateResult.Fail("Unknown E2E test profile."));

        var claims = new List<Claim> { new(PemsClaimTypes.UserId, profile.UserId.ToString()) };
        if (!string.IsNullOrWhiteSpace(profile.RoleCode)) claims.Add(new(PemsClaimTypes.RoleCode, profile.RoleCode));
        if (!string.IsNullOrWhiteSpace(profile.SubRole)) claims.Add(new(PemsClaimTypes.SubRole, profile.SubRole!));
        if (profile.PrimaryCampusId is { } campus) claims.Add(new(PemsClaimTypes.PrimaryCampusId, campus.ToString()));
        if (profile.DepartmentId is { } dept) claims.Add(new(PemsClaimTypes.DepartmentId, dept.ToString()));
        if (!string.IsNullOrWhiteSpace(profile.Email)) claims.Add(new(PemsClaimTypes.Email, profile.Email!));
        // The SessionValidationMiddleware needs an active session bound to the user; the orchestration
        // seeds one and puts its id here so the E2E actor is validated exactly like a logged-in user.
        if (profile.SessionId is { } session) claims.Add(new(PemsClaimTypes.SessionId, session.ToString()));

        var identity = new ClaimsIdentity(claims, E2ETestAuthGate.SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), E2ETestAuthGate.SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
