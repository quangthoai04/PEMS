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

    [Fact]
    public void Smtp_disabled_does_not_require_credentials()
    {
        SecretConfigurationValidator.ValidateSecrets(
            Config(("Smtp:Enabled", "false"), ("Smtp:Password", "")), Env("Production"));
    }

    [Fact]
    public void Production_google_drive_oauth_requires_client_secret_and_refresh_token()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecretConfigurationValidator.ValidateSecrets(
                Config(
                    ("JwtSettings:SecretKey", "supplied"),
                    ("GoogleDrive:Enabled", "true"),
                    ("GoogleDrive:AuthMode", "OAuthUser"),
                    ("GoogleDrive:ClientSecret", ""),
                    ("GoogleDrive:RefreshToken", "")),
                Env("Production")));

        Assert.Contains("GoogleDrive:ClientSecret", ex.Message);
        Assert.Contains("GoogleDrive:RefreshToken", ex.Message);
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
