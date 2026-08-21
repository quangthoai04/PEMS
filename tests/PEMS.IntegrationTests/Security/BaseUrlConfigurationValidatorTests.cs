using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PEMS.Api.Extensions;
using Xunit;

namespace PEMS.IntegrationTests.Security;

/// <summary>
/// BUG-12: a Production host must refuse to start rather than silently mint public email-action / SPA
/// deep-links against an unset, malformed, non-HTTPS, or loopback base URL — a mistake here does not fail
/// loudly, it mints a link that quietly points nowhere real until a visitor clicks a real email days later.
/// </summary>
public sealed class BaseUrlConfigurationValidatorTests
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

    private static IConfiguration ValidUrls() => Config(
        ("App:PublicApiBaseUrl", "https://api.pems.fpt.edu.vn"),
        ("App:FrontendBaseUrl", "https://pems.fpt.edu.vn"));

    [Fact]
    public void Production_with_real_https_urls_passes()
    {
        BaseUrlConfigurationValidator.ValidateBaseUrls(ValidUrls(), Env("Production"));
    }

    [Fact]
    public void Production_with_unset_public_api_base_url_fails_fast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            BaseUrlConfigurationValidator.ValidateBaseUrls(
                Config(("App:PublicApiBaseUrl", ""), ("App:FrontendBaseUrl", "https://pems.fpt.edu.vn")),
                Env("Production")));

        Assert.Contains("App:PublicApiBaseUrl", ex.Message);
    }

    [Fact]
    public void Production_with_unset_frontend_base_url_fails_fast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            BaseUrlConfigurationValidator.ValidateBaseUrls(
                Config(("App:PublicApiBaseUrl", "https://api.pems.fpt.edu.vn"), ("App:FrontendBaseUrl", null)),
                Env("Production")));

        Assert.Contains("App:FrontendBaseUrl", ex.Message);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public void Production_with_a_loopback_host_fails_fast(string loopbackHost)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            BaseUrlConfigurationValidator.ValidateBaseUrls(
                Config(
                    ("App:PublicApiBaseUrl", $"https://{loopbackHost}:5265"),
                    ("App:FrontendBaseUrl", "https://pems.fpt.edu.vn")),
                Env("Production")));

        Assert.Contains("App:PublicApiBaseUrl", ex.Message);
        Assert.Contains("loopback", ex.Message);
    }

    [Fact]
    public void Production_with_http_instead_of_https_fails_fast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            BaseUrlConfigurationValidator.ValidateBaseUrls(
                Config(
                    ("App:PublicApiBaseUrl", "http://api.pems.fpt.edu.vn"),
                    ("App:FrontendBaseUrl", "https://pems.fpt.edu.vn")),
                Env("Production")));

        Assert.Contains("App:PublicApiBaseUrl", ex.Message);
        Assert.Contains("https://", ex.Message);
    }

    [Fact]
    public void Production_with_a_malformed_url_fails_fast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            BaseUrlConfigurationValidator.ValidateBaseUrls(
                Config(
                    ("App:PublicApiBaseUrl", "not-a-url"),
                    ("App:FrontendBaseUrl", "https://pems.fpt.edu.vn")),
                Env("Production")));

        Assert.Contains("App:PublicApiBaseUrl", ex.Message);
    }

    [Fact]
    public void Production_reports_both_problems_at_once_when_both_urls_are_bad()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            BaseUrlConfigurationValidator.ValidateBaseUrls(
                Config(("App:PublicApiBaseUrl", ""), ("App:FrontendBaseUrl", "http://localhost:3000")),
                Env("Production")));

        Assert.Contains("App:PublicApiBaseUrl", ex.Message);
        Assert.Contains("App:FrontendBaseUrl", ex.Message);
    }

    [Fact]
    public void Development_with_localhost_urls_is_allowed()
    {
        BaseUrlConfigurationValidator.ValidateBaseUrls(
            Config(("App:PublicApiBaseUrl", "http://localhost:5265"), ("App:FrontendBaseUrl", "http://localhost:3000")),
            Env("Development"));
    }

    [Fact]
    public void Testing_with_localhost_urls_is_allowed()
    {
        BaseUrlConfigurationValidator.ValidateBaseUrls(
            Config(("App:PublicApiBaseUrl", "http://localhost:5265"), ("App:FrontendBaseUrl", "http://localhost:3000")),
            Env("Testing"));
    }

    [Fact]
    public void Development_with_completely_unset_urls_is_allowed()
    {
        BaseUrlConfigurationValidator.ValidateBaseUrls(
            Config(("App:PublicApiBaseUrl", null), ("App:FrontendBaseUrl", null)),
            Env("Development"));
    }
}
