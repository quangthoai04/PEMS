using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// The Pure V2 database-readiness check is the difference between "MySQL answered" and "this is actually
/// the per-campus schema". Against the canonical disposable database it must report ready; against a
/// database that is missing a required table or column, or that carries a dropped V1 column again, it must
/// report NOT ready and name exactly what is wrong — without ever leaking a connection string or secret.
///
/// The failure cases run against throwaway databases with a deliberately broken minimal schema, created
/// and dropped here, so the shared canonical database is never mutated.
/// </summary>
public sealed class PureV2SchemaReadinessTests
{
    private const string Original =
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None";

    private static string ConnString => DisposableDatabaseManager.GetDisposableConnectionString(Original);

    private static bool? _dbUp;

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try
            {
                using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);
                _dbUp = db.Database.CanConnect();
            }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private static ApplicationDbContext ContextFor(string connString)
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connString, ServerVersion.AutoDetect(connString)).Options);

    [Fact]
    public async Task The_canonical_schema_reports_ready_with_nothing_missing()
    {
        RequireDb();
        using var db = ContextFor(ConnString);
        var result = await new PureV2SchemaReadinessService(db).CheckAsync();

        Assert.True(result.SchemaReady);
        Assert.Empty(result.MissingTables);
        Assert.Empty(result.MissingColumns);
        Assert.Empty(result.UnexpectedV1Columns);
        Assert.False(string.IsNullOrEmpty(result.DatabaseName));
        // The report carries the database NAME, never the connection string / password.
        Assert.DoesNotContain("password", result.DatabaseName!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("123456", result.DatabaseName!);
    }

    // ── Failure cases on throwaway databases ─────────────────────────────────────

    private static string ServerConnString()
    {
        var b = new MySqlConnectionStringBuilder(Original) { Database = "" };
        return b.ConnectionString;
    }

    private static string ConnStringFor(string dbName)
    {
        var b = new MySqlConnectionStringBuilder(Original) { Database = dbName };
        return b.ConnectionString;
    }

    /// <summary>Builds a throwaway DB with the five required tables (minimal), applying one deliberate break.</summary>
    private static async Task<string> BuildBrokenDbAsync(Action<MySqlConnection> mutate)
    {
        RequireDb();
        var dbName = "pems_readiness_" + Guid.NewGuid().ToString("N")[..12];
        await using (var master = new MySqlConnection(ServerConnString()))
        {
            await master.OpenAsync();
            await Exec(master, $"CREATE DATABASE `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        }
        await using (var conn = new MySqlConnection(ConnStringFor(dbName)))
        {
            await conn.OpenAsync();
            // A minimal-but-complete Pure V2 skeleton; the mutate callback then breaks exactly one thing.
            await Exec(conn, "CREATE TABLE visit_requests (visit_request_id BIGINT UNSIGNED PRIMARY KEY)");
            await Exec(conn, "CREATE TABLE visit_request_campuses (visit_instance_id BIGINT UNSIGNED PRIMARY KEY, current_host_user_id BIGINT UNSIGNED NULL)");
            await Exec(conn, "CREATE TABLE visit_instance_form_details (visit_instance_id BIGINT UNSIGNED PRIMARY KEY, delegation_name VARCHAR(255) NULL, visit_type VARCHAR(50) NULL, purpose TEXT NULL, media_consent_status VARCHAR(20) NULL)");
            await Exec(conn, "CREATE TABLE visit_guest_members (guest_member_id BIGINT UNSIGNED PRIMARY KEY)");
            await Exec(conn, "CREATE TABLE visit_instance_guest_members (id BIGINT UNSIGNED PRIMARY KEY)");
            mutate(conn);
        }
        return dbName;
    }

    private static async Task DropDbAsync(string dbName)
    {
        await using var master = new MySqlConnection(ServerConnString());
        await master.OpenAsync();
        await Exec(master, $"DROP DATABASE IF EXISTS `{dbName}`");
    }

    private static async Task Exec(MySqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static void ExecSync(MySqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task A_missing_required_table_reports_not_ready_and_names_it()
    {
        var dbName = await BuildBrokenDbAsync(conn => ExecSync(conn, "DROP TABLE visit_instance_form_details"));
        try
        {
            using var db = ContextFor(ConnStringFor(dbName));
            var result = await new PureV2SchemaReadinessService(db).CheckAsync();
            Assert.False(result.SchemaReady);
            Assert.Contains("visit_instance_form_details", result.MissingTables);
        }
        finally { await DropDbAsync(dbName); }
    }

    [Fact]
    public async Task A_missing_required_column_reports_not_ready_and_names_it()
    {
        var dbName = await BuildBrokenDbAsync(conn =>
            ExecSync(conn, "ALTER TABLE visit_instance_form_details DROP COLUMN delegation_name"));
        try
        {
            using var db = ContextFor(ConnStringFor(dbName));
            var result = await new PureV2SchemaReadinessService(db).CheckAsync();
            Assert.False(result.SchemaReady);
            Assert.Contains("visit_instance_form_details.delegation_name", result.MissingColumns);
        }
        finally { await DropDbAsync(dbName); }
    }

    [Fact]
    public async Task A_reintroduced_v1_column_reports_not_ready_as_a_dual_version_regression()
    {
        var dbName = await BuildBrokenDbAsync(conn =>
            ExecSync(conn, "ALTER TABLE visit_requests ADD COLUMN form_schema_version TINYINT NOT NULL DEFAULT 1"));
        try
        {
            using var db = ContextFor(ConnStringFor(dbName));
            var result = await new PureV2SchemaReadinessService(db).CheckAsync();
            Assert.False(result.SchemaReady);
            Assert.Contains("visit_requests.form_schema_version", result.UnexpectedV1Columns);
        }
        finally { await DropDbAsync(dbName); }
    }
}
