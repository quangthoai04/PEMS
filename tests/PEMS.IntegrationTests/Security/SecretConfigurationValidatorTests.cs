using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PEMS.Api.Extensions;
using Xunit;

namespace PEMS.IntegrationTests.Security;

/// <summary>
/// Guards the fail-closed secret contract: real credentials come from the environment, and a missing one
/// must stop start-up instead of silently falling back to a weak/committed default.
///
/// Regression for a real defect: the repository used to ship a working JWT signing key and SMTP app password
/// inside tracked <c>appsettings.json</c>. After externalizing them, an empty value must FAIL in Production
/// rather than sign tokens with an empty key.
///
/// These tests build configuration in-memory — they never read the real appsettings files and never contain
/// a real secret value.
/// </summary>
public sealed class SecretConfigurationValidatorTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] pairs)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "PEMS.Api";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static IHostEnvironment Env(string name) => new FakeEnvironment { EnvironmentName = name };

    [Fact]
    public void Production_without_jwt_secret_fails_fast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecretConfigurationValidator.ValidateSecrets(
                Config(("JwtSettings:SecretKey", "")), Env("Production")));

        Assert.Contains("JwtSettings:SecretKey", ex.Message);
    }

    [Fact]
    public void Production_with_placeholder_jwt_secret_fails_fast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecretConfigurationValidator.ValidateSecrets(
                Config(("JwtSettings:SecretKey", "YOUR_JWT_SIGNING_KEY")), Env("Production")));

        Assert.Contains("JwtSettings:SecretKey", ex.Message);
    }

    [Fact]
    public void Production_with_supplied_jwt_secret_passes()
    {
        SecretConfigurationValidator.ValidateSecrets(
            Config(("JwtSettings:SecretKey", "a-value-injected-by-the-environment")), Env("Production"));
    }

    [Fact]
    public void Development_without_jwt_secret_is_allowed()
    {
        // Local dev and tests must run without real credentials.
        SecretConfigurationValidator.ValidateSecrets(Config(("JwtSettings:SecretKey", "")), Env("Development"));
    }

    [Fact]
    public void Smtp_enabled_without_password_fails_in_any_environment()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecretConfigurationValidator.ValidateSecrets(
                Config(("Smtp:Enabled", "true"), ("Smtp:User", "someone@example.test"), ("Smtp:Password", "")),
                Env("Development")));

        Assert.Contains("Smtp:Password", ex.Message);
    }

    /// <summary>
    /// Isolates the SMTP rule. The JWT key is supplied because Production requires it unconditionally —
    /// that rule has its own test in <see cref="Production_without_jwt_secret_fails_fast"/>, and leaving it
    /// unset here would make this test pass or fail for the wrong reason.
    /// </summary>
    [Fact]
    public void Smtp_disabled_does_not_require_credentials()
    {
        SecretConfigurationValidator.ValidateSecrets(
            Config(
                ("JwtSettings:SecretKey", "a-value-injected-by-the-environment"),
                ("Smtp:Enabled", "false"),
                ("Smtp:Password", "")),
            Env("Production"));
    }

    [Fact]
    public void Production_google_drive_oauth_requires_the_oauth_client()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecretConfigurationValidator.ValidateSecrets(
                Config(
                    ("JwtSettings:SecretKey", "supplied"),
                    ("GoogleDrive:Enabled", "true"),
                    ("GoogleDrive:AuthMode", "OAuthUser"),
                    ("GoogleDrive:ClientId", ""),
                    ("GoogleDrive:ClientSecret", ""),
                    ("GoogleDrive:RedirectUri", "")),
                Env("Production")));

        Assert.Contains("GoogleDrive:ClientId", ex.Message);
        Assert.Contains("GoogleDrive:ClientSecret", ex.Message);
        Assert.Contains("GoogleDrive:RedirectUri", ex.Message);
    }

    /// <summary>
    /// The refresh token is runtime state, not deployment configuration: it lives encrypted in
    /// api_configurations and is replaced by an ADMIN through the API-management screen.
    ///
    /// <para>
    /// A host with no stored token — a fresh production database, or one whose token Google revoked — must
    /// therefore START. Refusing to boot would take away the only screen that can reconnect it, which is
    /// how replacing the token used to require a redeploy in the first place.
    /// </para>
    /// </summary>
    [Fact]
    public void Production_google_drive_starts_without_a_refresh_token()
    {
        SecretConfigurationValidator.ValidateSecrets(
            Config(
                ("JwtSettings:SecretKey", "a-value-injected-by-the-environment"),
                ("GoogleDrive:Enabled", "true"),
                ("GoogleDrive:AuthMode", "OAuthUser"),
                ("GoogleDrive:ClientId", "an-oauth-client-id"),
                ("GoogleDrive:ClientSecret", "an-injected-client-secret"),
                ("GoogleDrive:RedirectUri", "https://api.example.test/api/google-drive/oauth/callback"),
                ("GoogleDrive:RefreshToken", "")),
            Env("Production"));
    }

    [Fact]
    public void Error_message_never_echoes_a_configured_value()
    {
        const string secretish = "super-secret-do-not-log";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecretConfigurationValidator.ValidateSecrets(
                Config(
                    ("JwtSettings:SecretKey", ""),
                    ("Smtp:Enabled", "true"),
                    ("Smtp:User", secretish),
                    ("Smtp:Password", "")),
                Env("Production")));

        Assert.DoesNotContain(secretish, ex.Message);
    }

    [Fact]
    public void Production_rejects_a_jwt_secret_that_is_too_short_to_sign_with()
    {
        // HS256 throws IDX10653 below 128 bits; refusing to start beats a 500 on the first login.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecretConfigurationValidator.ValidateSecrets(
                Config(("JwtSettings:SecretKey", "too-short")), Env("Production")));

        Assert.Contains("JwtSettings:SecretKey", ex.Message);
        Assert.Contains("too short", ex.Message);
    }

    [Fact]
    public void Development_without_jwt_secret_gets_a_generated_key_long_enough_to_sign()
    {
        var builder = new ConfigurationBuilder();
        var initial = Config(("JwtSettings:SecretKey", ""));

        var generated = SecretConfigurationValidator.TryProvideDevelopmentJwtSecret(builder, initial, Env("Development"));

        Assert.True(generated);
        var key = builder.Build()["JwtSettings:SecretKey"];
        Assert.False(string.IsNullOrWhiteSpace(key));
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(key!) >= SecretConfigurationValidator.MinimumJwtSecretBytes);
    }

    [Fact]
    public void Production_never_gets_a_generated_key()
    {
        var builder = new ConfigurationBuilder();

        var generated = SecretConfigurationValidator.TryProvideDevelopmentJwtSecret(
            builder, Config(("JwtSettings:SecretKey", "")), Env("Production"));

        Assert.False(generated);
        Assert.Null(builder.Build()["JwtSettings:SecretKey"]);
    }

    [Fact]
    public void A_supplied_key_is_never_overwritten()
    {
        var builder = new ConfigurationBuilder();

        var generated = SecretConfigurationValidator.TryProvideDevelopmentJwtSecret(
            builder, Config(("JwtSettings:SecretKey", "an-explicitly-supplied-development-key-value")), Env("Development"));

        Assert.False(generated);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("YOUR_SMTP_APP_PASSWORD", false)]
    [InlineData("CHANGE_ME", false)]
    [InlineData("SUPPLIED AT RUNTIME — set the env var", false)]
    [InlineData("an-actual-injected-value", true)]
    public void IsConfigured_rejects_blanks_and_placeholders(string? value, bool expected)
        => Assert.Equal(expected, SecretConfigurationValidator.IsConfigured(value));
}
