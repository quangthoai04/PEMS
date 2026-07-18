using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PEMS.Api.Controllers;
using PEMS.Application.Common.Options;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Tests for the public per-campus form v2 capability endpoint
/// (<c>GET /api/public/features/per-campus-form-v2</c>) — the single authority the browser uses to decide
/// whether to route to the v2 flow. Two layers:
///   • A DB-free theory that constructs the controller directly across all FOUR flag combinations, asserting
///     <c>enabled == read AND write</c> and that no other field leaks.
///   • WAF-backed HTTP tests proving the endpoint is anonymous, wired into the real pipeline, and that the
///     flags flow through real DI (default OFF, and ON/ON via WithWebHostBuilder).
///
/// The endpoint never touches the database, so these tests never mutate pems_test.
/// </summary>
public sealed class PublicFeaturesCapabilityApiTests : IClassFixture<PemsWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PemsWebApplicationFactory _factory;

    public PublicFeaturesCapabilityApiTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record Capability(bool ReadEnabled, bool WriteEnabled, bool Enabled);

    // ── 1. DB-free: all four flag combinations at the controller level ──────────
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void Capability_enabled_is_read_AND_write(bool read, bool write, bool expectedEnabled)
    {
        var controller = new PublicFeaturesController(
            new PerCampusFormV2Options { Enabled = read },
            new PerCampusFormV2WriteOptions { Enabled = write });

        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(
            controller.GetPerCampusFormV2Capability());
        var body = Assert.IsType<PerCampusFormV2CapabilityResponse>(result.Value);

        Assert.Equal(read, body.ReadEnabled);
        Assert.Equal(write, body.WriteEnabled);
        Assert.Equal(expectedEnabled, body.Enabled);
    }

    // ── 2. HTTP: anonymous + default OFF shape (routing / AllowAnonymous / no secret) ──
    [Fact]
    public async Task Endpoint_is_anonymous_and_defaults_off()
    {
        // No X-Test-UserId header → anonymous. Must still return 200 (not 401).
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/public/features/per-campus-form-v2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Capability>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(body!.ReadEnabled);
        Assert.False(body.WriteEnabled);
        Assert.False(body.Enabled);

        // The payload must expose ONLY the three capability flags — no other config key.
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = doc.RootElement.EnumerateObject().Select(p => p.Name.ToLowerInvariant()).ToHashSet();
        Assert.Equal(new HashSet<string> { "readenabled", "writeenabled", "enabled" }, names);
    }

    // ── 3. HTTP: flags flow through real DI when both are ON ─────────────────────
    [Fact]
    public async Task Endpoint_reports_enabled_when_both_flags_on()
    {
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<PerCampusFormV2Options>();
                services.AddSingleton(new PerCampusFormV2Options { Enabled = true });
                services.RemoveAll<PerCampusFormV2WriteOptions>();
                services.AddSingleton(new PerCampusFormV2WriteOptions { Enabled = true });
            })).CreateClient();

        var body = await client.GetFromJsonAsync<Capability>(
            "/api/public/features/per-campus-form-v2", JsonOptions);

        Assert.NotNull(body);
        Assert.True(body!.ReadEnabled);
        Assert.True(body.WriteEnabled);
        Assert.True(body.Enabled);
    }
}
