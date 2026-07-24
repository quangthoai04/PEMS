using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PEMS.Api.Controllers;
using PEMS.Api.Extensions;
using PEMS.Api.Middleware;
using PEMS.Application;
using PEMS.Application.Common.Security;
using PEMS.Infrastructure;
using PEMS.Infrastructure.Persistence;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Boots the real PEMS API pipeline (MediatR, validators, real EF Core against a MySQL
/// <c>pems_test</c> database, real controllers) behind an in-memory <see cref="TestServer"/>,
/// with JWT authentication swapped out for <see cref="TestAuthHandler"/> so tests can simulate
/// any role via headers instead of a real login flow.
///
/// Configuration is read ONLY from <c>backend/PEMS.Api/appsettings.Testing.json</c> (see
/// <c>appsettings.Testing.example.json</c> for the template) — never from
/// <c>appsettings.Development.json</c>, and never falls back to the dev database.
///
/// Uses <see cref="FaqsController"/> only as the generic type parameter <see cref="WebApplicationFactory{TEntryPoint}"/>
/// requires — never instantiated or called directly. It is never a marker type defined in this
/// test project: <c>WebApplicationFactory</c> resolves the SUT's content root from an
/// SDK-generated <c>WebApplicationFactoryContentRootAttribute</c> keyed by
/// <c>typeof(TEntryPoint).Assembly</c>, and that attribute only exists for assemblies the test
/// project references as a web app (PEMS.Api) — not for the test assembly itself. A marker type
/// declared here instead made it guess a wrong path (missing the repo's <c>tests/</c> segment)
/// and crash on startup. <see cref="CreateHostBuilder"/> below still builds the whole test host
/// itself, wiring up the exact same public DI/middleware building blocks Program.cs uses
/// (<see cref="PEMS.Application.DependencyInjection.AddApplication"/>,
/// <see cref="PEMS.Infrastructure.DependencyInjection.AddInfrastructure"/>, etc.), so PEMS.Api's
/// actual <c>Program</c> (internal, compiler-generated) is still never referenced or modified.
/// </summary>
public sealed class PemsWebApplicationFactory : WebApplicationFactory<FaqsController>
{
    /// <summary>Absolute path to the appsettings.Testing.json this factory will load.</summary>
    public static string TestingAppSettingsPath { get; } = ResolveTestingAppSettingsPath();

    protected override IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.UseEnvironment("Testing");

                webBuilder.ConfigureAppConfiguration((_, config) =>
                {
                    config.Sources.Clear();

                    if (!File.Exists(TestingAppSettingsPath))
                    {
                        throw new FileNotFoundException(
                            "appsettings.Testing.json not found. Copy " +
                            "backend/PEMS.Api/appsettings.Testing.example.json to " +
                            "backend/PEMS.Api/appsettings.Testing.json and point ConnectionStrings:DefaultConnection " +
                            $"at your local 'pems_test' database. Expected at: {TestingAppSettingsPath}");
                    }

                    config.AddJsonFile(TestingAppSettingsPath, optional: false, reloadOnChange: false);
                });

                webBuilder.ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;

                    // Same building blocks Program.cs uses — nothing in PEMS.Api is modified.
                    services.AddApplication();
                    services.AddInfrastructure(configuration);

                    var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
                        ?? new AuthOptions();
                    services.AddSingleton(authOptions);

                    // Per-campus form v2 feature flags (default OFF) — mirrors Program.cs so the flag-gated
                    // handlers and the public capability endpoint resolve. Individual tests override these via
                    // WithWebHostBuilder when they need a specific flag combination.
                    var perCampusV2Read = configuration
                        .GetSection(PEMS.Application.Common.Options.PerCampusFormV2Options.SectionName)
                        .Get<PEMS.Application.Common.Options.PerCampusFormV2Options>()
                        ?? new PEMS.Application.Common.Options.PerCampusFormV2Options();
                    services.AddSingleton(perCampusV2Read);

                    var perCampusV2Write = configuration
                        .GetSection(PEMS.Application.Common.Options.PerCampusFormV2WriteOptions.SectionName)
                        .Get<PEMS.Application.Common.Options.PerCampusFormV2WriteOptions>()
                        ?? new PEMS.Application.Common.Options.PerCampusFormV2WriteOptions();
                    services.AddSingleton(perCampusV2Write);

                    var originalConnectionString = configuration.GetConnectionString("DefaultConnection")
                        ?? throw new InvalidOperationException(
                            "ConnectionStrings:DefaultConnection is not configured in appsettings.Testing.json.");

                    var connectionString = DisposableDatabaseManager.GetDisposableConnectionString(originalConnectionString);

                    // Mirrors Program.cs: every pooled MySQL session is pinned to +07:00 so
                    // CURRENT_TIMESTAMP defaults/triggers produce Vietnam wall-clock in tests too.
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                               .AddInterceptors(new VietnamTimeZoneConnectionInterceptor()));

                    // Controllers live in PEMS.Api — this test host's "entry assembly" is the test
                    // project, so the controller assembly must be added explicitly.
                    // JSON options mirror Program.cs: timestamps serialize with the +07:00 offset.
                    services.AddControllers()
                        .AddApplicationPart(typeof(FaqsController).Assembly)
                        .AddJsonOptions(options =>
                        {
                            options.JsonSerializerOptions.Converters.Add(new PEMS.Api.Serialization.VietnamDateTimeJsonConverter());
                            options.JsonSerializerOptions.Converters.Add(new PEMS.Api.Serialization.VietnamNullableDateTimeJsonConverter());
                        });

                    // Real JwtBearer is replaced by the header-driven TestAuthHandler.
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                    services.AddAppAuthorization();
                });

                webBuilder.Configure(app =>
                {
                    // Mirrors Program.cs's pipeline for the pieces CreateFAQ actually exercises.
                    // Swagger/CORS/HSTS/rate-limiting are irrelevant to API correctness tests and
                    app.UseMiddleware<ExceptionHandlingMiddleware>();
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseMiddleware<SessionValidationMiddleware>();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            });
    }

    /// <summary>
    /// Walks up from the test assembly's output directory to find the repository root
    /// (the directory that contains both <c>backend</c> and <c>tests</c>), then resolves
    /// <c>backend/PEMS.Api/appsettings.Testing.json</c> from there. Avoids relying on
    /// WebApplicationFactory's content-root guessing, which targets the test project's own
    /// output folder, not PEMS.Api's.
    /// </summary>
    private static string ResolveTestingAppSettingsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null &&
               !(Directory.Exists(Path.Combine(dir.FullName, "backend")) &&
                 Directory.Exists(Path.Combine(dir.FullName, "tests"))))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not locate the repository root (a directory containing both 'backend' and 'tests') " +
                $"walking up from {AppContext.BaseDirectory}.");
        }

        return Path.Combine(dir.FullName, "backend", "PEMS.Api", "appsettings.Testing.json");
    }


}
