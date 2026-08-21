using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PEMS.Api.Middleware;
using Xunit;

namespace PEMS.IntegrationTests.Middleware;

/// <summary>
/// BUG-01 exit gate: in a Production-like environment, the public email-action HTML pages
/// (<c>/api/public/email-actions/*</c>) must get a CSP that allows their inline-styled
/// <c>&lt;form method="post"&gt;</c>, while every other route keeps the strict, form-submission-blocking
/// policy — <see cref="SecurityHeadersMiddleware"/> must never be relaxed globally to fix one route.
///
/// Hosts a minimal <see cref="TestServer"/> with ONLY this middleware in the pipeline (no database, no
/// auth) — the header contract depends only on <c>IHostEnvironment.EnvironmentName</c> and the request
/// path, so a full app host would only add unrelated startup cost.
/// </summary>
public sealed class SecurityHeadersMiddlewareTests
{
    private static TestServer BuildServer(string environmentName)
    {
        var builder = new WebHostBuilder()
            .UseEnvironment(environmentName)
            .ConfigureServices(services => services.AddRouting())
            .Configure(app =>
            {
                app.UseMiddleware<SecurityHeadersMiddleware>();
                app.Run(context => context.Response.WriteAsync("ok"));
            });
        return new TestServer(builder);
    }

    [Fact]
    public async Task Production_public_email_action_route_gets_the_relaxed_form_allowing_csp()
    {
        using var server = BuildServer(Environments.Production);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/api/public/email-actions/abc123");

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));
        var csp = Assert.Single(values!);
        Assert.Contains("form-action 'self'", csp);
        Assert.Contains("style-src 'unsafe-inline'", csp);
        Assert.DoesNotContain("form-action 'none'", csp);
    }

    [Fact]
    public async Task Production_every_other_route_keeps_the_strict_no_form_csp()
    {
        using var server = BuildServer(Environments.Production);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/api/visit-requests");

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));
        var csp = Assert.Single(values!);
        Assert.Contains("form-action 'none'", csp);
        Assert.DoesNotContain("style-src 'unsafe-inline'", csp);
    }

    [Fact]
    public async Task Production_swagger_route_gets_no_csp_header_at_all()
    {
        using var server = BuildServer(Environments.Production);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task Development_gets_no_csp_header_on_any_route_including_the_public_email_action_one()
    {
        using var server = BuildServer(Environments.Development);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/api/public/email-actions/abc123");

        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task Every_response_still_carries_the_other_baseline_security_headers_regardless_of_route()
    {
        using var server = BuildServer(Environments.Production);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/api/public/email-actions/abc123");

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }
}
