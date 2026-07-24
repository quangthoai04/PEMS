using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Api.Controllers;

/// <summary>
/// Stack + Pure V2 database readiness surface. It answers the one question a browser or an operator needs
/// to trust the running stack: is this the expected environment, on the expected database, with the
/// per-campus schema actually in place. It returns NO secret, connection string, token or credential —
/// only the environment name, the database name, a commit marker and the readiness verdict.
///
/// The detailed diagnostics (which tables/columns are missing) are exposed ONLY outside Production, so a
/// live deployment never advertises its schema shape; Production still reports a plain ready/not-ready.
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IPureV2SchemaReadiness _readiness;
    private readonly IWebHostEnvironment _environment;

    public HealthController(IPureV2SchemaReadiness readiness, IWebHostEnvironment environment)
    {
        _readiness = readiness;
        _environment = environment;
    }

    /// <summary>Liveness: the process is up. Never touches the database.</summary>
    [AllowAnonymous]
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "alive" });

    /// <summary>
    /// Readiness: the Pure V2 schema is in place on the connected database. Returns 200 when ready and
    /// 503 when not, so a load balancer / the frontend can gate on it. The detail of what is missing is
    /// included only outside Production.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("readiness")]
    public async Task<IActionResult> Readiness(CancellationToken cancellationToken)
    {
        var result = await _readiness.CheckAsync(cancellationToken);
        var isProduction = _environment.IsProduction();

        object body = isProduction
            ? new
            {
                environment = _environment.EnvironmentName,
                apiCommit = CommitMarker,
                pureV2 = true,
                schemaReady = result.SchemaReady,
            }
            : new
            {
                environment = _environment.EnvironmentName,
                apiCommit = CommitMarker,
                databaseName = result.DatabaseName,
                pureV2 = true,
                schemaReady = result.SchemaReady,
                missingTables = result.MissingTables,
                missingColumns = result.MissingColumns,
                unexpectedV1Columns = result.UnexpectedV1Columns,
            };

        return result.SchemaReady ? Ok(body) : StatusCode(503, body);
    }

    // Build-stamped commit if present (assembly informational version), else "unknown". Never a secret.
    private static string CommitMarker =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}
