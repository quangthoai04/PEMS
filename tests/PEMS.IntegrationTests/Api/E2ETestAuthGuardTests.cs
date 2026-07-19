using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PEMS.Api.Authentication;
using PEMS.Application.Common.Security;
using Xunit;

namespace PEMS.IntegrationTests.Api;

/// <summary>
/// Guards for the TESTING-ONLY fail-closed real-stack E2E auth scheme (Slice 6). Unlike the WAF's
/// header-trusting <c>TestAuthHandler</c>, this scheme must be quadruple-gated (Testing env + explicit flag +
/// run secret + profile file), validate the secret in constant time, and resolve identity SERVER-SIDE from a
/// seeded profile — never from a browser-supplied role/campus header. Runs serially (mutates process env
/// vars, restored in finally). No DB.
/// </summary>
[Collection("E2EAuthEnvSerial")]
public sealed class E2ETestAuthGuardTests
{
    private static (string? enabled, string? secret, string? profiles) Snapshot() =>
        (Environment.GetEnvironmentVariable(E2ETestAuthGate.EnabledEnvVar),
         Environment.GetEnvironmentVariable(E2ETestAuthGate.SecretEnvVar),
         Environment.GetEnvironmentVariable(E2ETestAuthGate.ProfilesEnvVar));

    private static void Restore((string? enabled, string? secret, string? profiles) s)
    {
        Environment.SetEnvironmentVariable(E2ETestAuthGate.EnabledEnvVar, s.enabled);
        Environment.SetEnvironmentVariable(E2ETestAuthGate.SecretEnvVar, s.secret);
        Environment.SetEnvironmentVariable(E2ETestAuthGate.ProfilesEnvVar, s.profiles);
    }

    // ── Registration gate ─────────────────────────────────────────────────────

    [Fact]
    public void IsEnabledFor_requires_Testing_env_AND_flag_AND_secret_AND_profile_file()
    {
        var saved = Snapshot();
        var profileFile = Path.Combine(Path.GetTempPath(), $"e2e_profiles_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(profileFile, "[]");
            Environment.SetEnvironmentVariable(E2ETestAuthGate.EnabledEnvVar, "true");
            Environment.SetEnvironmentVariable(E2ETestAuthGate.SecretEnvVar, "run-secret");
            Environment.SetEnvironmentVariable(E2ETestAuthGate.ProfilesEnvVar, profileFile);

            Assert.True(E2ETestAuthGate.IsEnabledFor("Testing"));
            // Never in any non-Testing environment, even when fully gated.
            Assert.False(E2ETestAuthGate.IsEnabledFor("Production"));
            Assert.False(E2ETestAuthGate.IsEnabledFor("Development"));
            Assert.False(E2ETestAuthGate.IsEnabledFor("Staging"));

            // Missing flag → off.
            Environment.SetEnvironmentVariable(E2ETestAuthGate.EnabledEnvVar, null);
            Assert.False(E2ETestAuthGate.IsEnabledFor("Testing"));
            Environment.SetEnvironmentVariable(E2ETestAuthGate.EnabledEnvVar, "false");
            Assert.False(E2ETestAuthGate.IsEnabledFor("Testing"));
            Environment.SetEnvironmentVariable(E2ETestAuthGate.EnabledEnvVar, "true");

            // Missing secret → off.
            Environment.SetEnvironmentVariable(E2ETestAuthGate.SecretEnvVar, null);
            Assert.False(E2ETestAuthGate.IsEnabledFor("Testing"));
            Environment.SetEnvironmentVariable(E2ETestAuthGate.SecretEnvVar, "run-secret");

            // Missing profile file path → off.
            Environment.SetEnvironmentVariable(E2ETestAuthGate.ProfilesEnvVar, null);
            Assert.False(E2ETestAuthGate.IsEnabledFor("Testing"));
        }
        finally
        {
            Restore(saved);
            if (File.Exists(profileFile)) File.Delete(profileFile);
        }
    }

    // ── Secret comparison ──────────────────────────────────────────────────────

    [Fact]
    public void SecretMatches_is_constant_time_and_rejects_wrong_or_missing()
    {
        Assert.True(E2ETestAuthGate.SecretMatches("abc123", "abc123"));
        Assert.False(E2ETestAuthGate.SecretMatches("abc123", "abc124"));
        Assert.False(E2ETestAuthGate.SecretMatches("short", "a-much-longer-secret"));
        Assert.False(E2ETestAuthGate.SecretMatches(null, "abc123"));
        Assert.False(E2ETestAuthGate.SecretMatches("abc123", null));
        Assert.False(E2ETestAuthGate.SecretMatches("", ""));
    }

    // ── Server-side profile store ──────────────────────────────────────────────

    [Fact]
    public void ProfileStore_resolves_seeded_identities_and_fails_closed_on_unknown_or_missing()
    {
        var saved = Snapshot();
        var profileFile = Path.Combine(Path.GetTempPath(), $"e2e_profiles_{Guid.NewGuid():N}.json");
        try
        {
            var seed = new[]
            {
                new { key = "visitor_owner", userId = 8, roleCode = "VISITOR", subRole = (string?)null, primaryCampusId = (int?)null, email = (string?)"registrant@example.com" },
                new { key = "campus_leader_hn", userId = 3, roleCode = "STAFF", subRole = (string?)"LEADER", primaryCampusId = (int?)1, email = (string?)null },
                new { key = "campus_leader_hcm", userId = 9, roleCode = "STAFF", subRole = (string?)"LEADER", primaryCampusId = (int?)2, email = (string?)null },
            };
            File.WriteAllText(profileFile, JsonSerializer.Serialize(seed));
            Environment.SetEnvironmentVariable(E2ETestAuthGate.ProfilesEnvVar, profileFile);

            var store = new E2ETestProfileStore(NullLogger<E2ETestProfileStore>.Instance);

            Assert.True(store.TryResolve("visitor_owner", out var owner));
            Assert.Equal(8ul, owner.UserId);
            Assert.Equal("VISITOR", owner.RoleCode);

            Assert.True(store.TryResolve("campus_leader_hn", out var hn));
            Assert.Equal(1ul, hn.PrimaryCampusId);
            Assert.True(store.TryResolve("campus_leader_hcm", out var hcm));
            Assert.Equal(2ul, hcm.PrimaryCampusId); // leader HN can never resolve to campus HCM

            Assert.False(store.TryResolve("unknown_profile", out _)); // fail-closed

            // Missing file path → empty store → everything unknown.
            Environment.SetEnvironmentVariable(E2ETestAuthGate.ProfilesEnvVar, null);
            var emptyStore = new E2ETestProfileStore(NullLogger<E2ETestProfileStore>.Instance);
            Assert.False(emptyStore.TryResolve("visitor_owner", out _));
        }
        finally
        {
            Restore(saved);
            if (File.Exists(profileFile)) File.Delete(profileFile);
        }
    }

    // ── Handler behaviour ──────────────────────────────────────────────────────

    private static async Task<(AuthenticateResult Result, ClaimsPrincipal? Principal)> RunHandlerAsync(
        string envName, string? profileKey, string? providedSecret, E2ETestProfileStore store,
        IReadOnlyDictionary<string, string>? extraHeaders = null)
    {
        var handler = new E2ETestAuthHandler(
            new StubOptionsMonitor(), NullLoggerFactory.Instance, UrlEncoder.Default,
            new StubHostEnvironment(envName), store);
        var ctx = new DefaultHttpContext();
        if (profileKey != null) ctx.Request.Headers[E2ETestAuthGate.ProfileHeader] = profileKey;
        if (providedSecret != null) ctx.Request.Headers[E2ETestAuthGate.SecretHeader] = providedSecret;
        if (extraHeaders != null)
            foreach (var kv in extraHeaders) ctx.Request.Headers[kv.Key] = kv.Value;
        var scheme = new AuthenticationScheme(E2ETestAuthGate.SchemeName, null, typeof(E2ETestAuthHandler));
        await handler.InitializeAsync(scheme, ctx);
        var result = await handler.AuthenticateAsync();
        return (result, result.Principal);
    }

    [Fact]
    public async Task Handler_authenticates_only_a_valid_profile_with_the_correct_secret_and_never_from_headers()
    {
        var saved = Snapshot();
        var profileFile = Path.Combine(Path.GetTempPath(), $"e2e_profiles_{Guid.NewGuid():N}.json");
        try
        {
            var seed = new[]
            {
                new { key = "campus_leader_hn", userId = 3, roleCode = "STAFF", subRole = "LEADER", primaryCampusId = 1 },
            };
            File.WriteAllText(profileFile, JsonSerializer.Serialize(seed));
            Environment.SetEnvironmentVariable(E2ETestAuthGate.EnabledEnvVar, "true");
            Environment.SetEnvironmentVariable(E2ETestAuthGate.SecretEnvVar, "run-secret");
            Environment.SetEnvironmentVariable(E2ETestAuthGate.ProfilesEnvVar, profileFile);
            var store = new E2ETestProfileStore(NullLogger<E2ETestProfileStore>.Instance);

            // Valid profile + correct secret → success with the SERVER-SIDE role/campus, even when the caller
            // also sends spoof role/campus headers (which the handler ignores entirely).
            var spoof = new Dictionary<string, string>
            {
                ["X-Test-RoleCode"] = "ADMIN",
                ["X-Test-PrimaryCampusId"] = "999",
                [PemsClaimTypes.RoleCode] = "ADMIN",
            };
            var (ok, principal) = await RunHandlerAsync("Testing", "campus_leader_hn", "run-secret", store, spoof);
            Assert.True(ok.Succeeded);
            Assert.Equal("3", principal!.FindFirstValue(PemsClaimTypes.UserId));
            Assert.Equal("STAFF", principal.FindFirstValue(PemsClaimTypes.RoleCode)); // never ADMIN from the header
            Assert.Equal("LEADER", principal.FindFirstValue(PemsClaimTypes.SubRole));
            Assert.Equal("1", principal.FindFirstValue(PemsClaimTypes.PrimaryCampusId)); // never 999

            // Wrong secret → fail.
            Assert.False((await RunHandlerAsync("Testing", "campus_leader_hn", "wrong-secret", store)).Result.Succeeded);
            // Missing secret → fail.
            Assert.False((await RunHandlerAsync("Testing", "campus_leader_hn", null, store)).Result.Succeeded);
            // Unknown profile → fail.
            Assert.False((await RunHandlerAsync("Testing", "ghost", "run-secret", store)).Result.Succeeded);
            // No profile header → NoResult (anonymous, not a hard failure).
            var none = await RunHandlerAsync("Testing", null, "run-secret", store);
            Assert.False(none.Result.Succeeded);
            Assert.True(none.Result.None);

            // Even a valid profile + correct secret authenticates NOTHING outside Testing (defense in depth).
            var prod = await RunHandlerAsync("Production", "campus_leader_hn", "run-secret", store);
            Assert.True(prod.Result.None);
        }
        finally
        {
            Restore(saved);
            if (File.Exists(profileFile)) File.Delete(profileFile);
        }
    }

    // ── Minimal stubs ──────────────────────────────────────────────────────────

    private sealed class StubOptionsMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue { get; } = new();
        public AuthenticationSchemeOptions Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<AuthenticationSchemeOptions, string?> listener) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public StubHostEnvironment(string environmentName) => EnvironmentName = environmentName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "PEMS.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
