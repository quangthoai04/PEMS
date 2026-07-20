using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Infrastructure.Persistence;

namespace PEMS.IntegrationTests.Reports;

/// <summary>
/// Real <b>Pomelo/MySQL</b> fixture for the canonical per-campus v2 reader regressions.
///
/// Why not the unit-test harness: <c>PEMS.UnitTests</c> runs EF Core InMemory, which never
/// translates a query. The whole point of these tests is that the v2 reads survive TRANSLATION on
/// the provider production actually uses, so they must run on MySQL — see the prompt's A1.6/A2.9.
///
/// Safety contract (fail-closed, checked before anything is created or dropped):
///   * the target database name must be EXACTLY <see cref="DisposableDbName"/>. Prefix matching is
///     not accepted, because a prefix rule is how a drill ends up pointed at a real database;
///   * <c>pems_db</c>, <c>pems_test</c> and <c>pems_pr3_test</c> are rejected explicitly even if
///     someone edits the constant;
///   * the schema is built by EF's own model (<c>EnsureCreated</c>), so the Phase I migration
///     scripts never run here — this database is NOT a migration drill target;
///   * <see cref="Dispose"/> drops the database again.
///
/// Credentials come from <c>appsettings.Testing.json</c> (untracked) and are never logged.
/// </summary>
public sealed class CanonicalV2ReaderFixture : IDisposable
{
    public const string DisposableDbName = "pems_it_regression";

    private static readonly string[] ProtectedDatabases = { "pems_db", "pems_test", "pems_pr3_test" };

    public ApplicationDbContext Db { get; }

    /// <summary>Set when the local machine has no usable MySQL/appsettings; tests then skip instead of lying.</summary>
    public static string? Unavailable { get; private set; }

    public CanonicalV2ReaderFixture()
    {
        var baseConnection = ResolveBaseConnectionString();
        var connection = RetargetToDisposableDatabase(baseConnection);

        Db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseMySql(connection, ServerVersion.AutoDetect(connection))
                .Options);

        Db.Database.EnsureDeleted();
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        try { Db.Database.EnsureDeleted(); }
        finally { Db.Dispose(); }
    }

    /// <summary>
    /// Reads the connection string from the same untracked file the API integration harness uses.
    /// Never falls back to a hardcoded credential.
    /// </summary>
    private static string ResolveBaseConnectionString()
    {
        var path = ResolveTestingAppSettingsPath();
        if (!File.Exists(path))
            throw new SkipFixtureException($"appsettings.Testing.json not found at {path}.");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var value = doc.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString();

        if (string.IsNullOrWhiteSpace(value))
            throw new SkipFixtureException("ConnectionStrings:DefaultConnection is empty.");

        return value!;
    }

    /// <summary>
    /// Locates <c>backend/PEMS.Api/appsettings.Testing.json</c> from THIS SOURCE FILE's compile-time
    /// path rather than from the output directory. The shared harness walks up from
    /// <c>AppContext.BaseDirectory</c>, which breaks the moment the build is redirected to a temp
    /// <c>BaseOutputPath</c> — the workaround needed on this machine because a running PEMS.Api dev
    /// server holds a lock on the normal bin folder.
    /// </summary>
    private static string ResolveTestingAppSettingsPath([CallerFilePath] string thisFile = "")
    {
        var dir = Path.GetDirectoryName(thisFile);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "backend")) && Directory.Exists(Path.Combine(dir, "tests")))
                return Path.Combine(dir, "backend", "PEMS.Api", "appsettings.Testing.json");
            dir = Path.GetDirectoryName(dir);
        }

        throw new SkipFixtureException($"Could not locate the repository root walking up from {thisFile}.");
    }

    /// <summary>
    /// Replaces whatever database the harness config points at with the disposable one, then
    /// re-validates the result. Rewriting rather than appending matters: a duplicated
    /// <c>database=</c> key would let the original (possibly real) database win.
    /// </summary>
    private static string RetargetToDisposableDatabase(string baseConnection)
    {
        var parts = baseConnection
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !p.StartsWith("database=", StringComparison.OrdinalIgnoreCase)
                        && !p.StartsWith("initial catalog=", StringComparison.OrdinalIgnoreCase))
            .ToList();

        parts.Insert(0, $"database={DisposableDbName}");
        var rebuilt = string.Join(";", parts);

        AssertDisposableTarget(rebuilt);
        return rebuilt;
    }

    private static void AssertDisposableTarget(string connection)
    {
        var database = connection
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.StartsWith("database=", StringComparison.OrdinalIgnoreCase))
            .Select(p => p["database=".Length..].Trim())
            .ToList();

        if (database.Count != 1)
            throw new InvalidOperationException(
                $"Refusing to run: expected exactly one database key, found {database.Count}.");

        var target = database[0];

        if (!string.Equals(target, DisposableDbName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Refusing to run: target '{target}' is not the exact disposable database '{DisposableDbName}'.");

        if (ProtectedDatabases.Contains(target, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to run: '{target}' is a protected database.");
    }

    public sealed class SkipFixtureException : Exception
    {
        public SkipFixtureException(string message) : base(message) => Unavailable = message;
    }
}

/// <summary>Shared per-class so the schema is created once, not per test.</summary>
[CollectionDefinition(Name)]
public sealed class CanonicalV2ReaderCollection : ICollectionFixture<CanonicalV2ReaderFixture>
{
    public const string Name = "canonical-v2-readers";
}

/// <summary>
/// Marks a fake caller for the two handlers. Only the properties the guards read are meaningful.
/// </summary>
public sealed class FakeCurrentUser : ICurrentUserService
{
    public bool IsAuthenticated { get; init; } = true;
    public ulong? UserId { get; init; } = 1;
    public string? Email { get; init; } = "reporter@example.test";
    public ulong? RoleId { get; init; } = 1;
    public string? RoleCode { get; init; }
    public string? SubRole { get; init; }
    public ulong? PrimaryCampusId { get; init; }
    public ulong? DepartmentId { get; init; }
    public ulong? SessionId { get; init; } = 1;
    public string? LoginPortal { get; init; } = "INTERNAL";
}
