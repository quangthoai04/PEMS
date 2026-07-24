using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using PEMS.Api.Controllers;
using PEMS.Application.Common.Interfaces;
using Xunit;

namespace PEMS.IntegrationTests.Api;

/// <summary>
/// Controller-level mapping for the readiness endpoint (no database): a ready schema is 200, a not-ready
/// schema is 503, and Production hides the which-tables-are-missing detail while non-Production exposes it
/// for diagnosis. No branch ever emits a secret.
/// </summary>
public sealed class HealthReadinessControllerTests
{
    private sealed class FakeReadiness : IPureV2SchemaReadiness
    {
        private readonly PureV2ReadinessResult _result;
        public FakeReadiness(PureV2ReadinessResult result) => _result = result;
        public Task<PureV2ReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public FakeEnv(string environmentName) => EnvironmentName = environmentName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "PEMS.Api";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static PureV2ReadinessResult Ready() => new()
    {
        SchemaReady = true, DatabaseName = "pems_pr3_test",
        MissingTables = new List<string>(), MissingColumns = new List<string>(), UnexpectedV1Columns = new List<string>(),
    };

    private static PureV2ReadinessResult NotReady() => new()
    {
        SchemaReady = false, DatabaseName = "pems_pr3_test",
        MissingTables = new List<string> { "visit_instance_form_details" },
        MissingColumns = new List<string>(), UnexpectedV1Columns = new List<string>(),
    };

    [Fact]
    public async Task Ready_schema_is_200()
    {
        var controller = new HealthController(new FakeReadiness(Ready()), new FakeEnv("Development"));
        var result = await controller.Readiness(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task Not_ready_schema_is_503_with_the_missing_detail_outside_production()
    {
        var controller = new HealthController(new FakeReadiness(NotReady()), new FakeEnv("Development"));
        var result = await controller.Readiness(CancellationToken.None);
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
        // The Development body carries the diagnostic detail (missingTables) and the db name, no secret.
        var json = System.Text.Json.JsonSerializer.Serialize(obj.Value);
        Assert.Contains("visit_instance_form_details", json);
        Assert.Contains("pems_pr3_test", json);
        Assert.DoesNotContain("password", json, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("123456", json);
    }

    [Fact]
    public async Task Production_hides_the_schema_detail_and_the_database_name()
    {
        var controller = new HealthController(new FakeReadiness(NotReady()), new FakeEnv("Production"));
        var result = await controller.Readiness(CancellationToken.None);
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(obj.Value);
        // Production reports ready/not-ready only — never which tables are missing nor the database name.
        Assert.DoesNotContain("visit_instance_form_details", json);
        Assert.DoesNotContain("pems_pr3_test", json);
        Assert.Contains("schemaReady", json);
    }
}
