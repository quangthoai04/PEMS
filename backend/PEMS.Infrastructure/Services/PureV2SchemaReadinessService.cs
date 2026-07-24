using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Infrastructure.Persistence;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Reads information_schema through the DbContext's own connection to confirm the database is the Pure V2
/// schema. It never opens a second provider and never reads any credential — only table/column names and
/// SELECT DATABASE().
/// </summary>
public sealed class PureV2SchemaReadinessService : IPureV2SchemaReadiness
{
    private readonly ApplicationDbContext _db;

    public PureV2SchemaReadinessService(ApplicationDbContext db) => _db = db;

    // The tables the per-campus runtime cannot function without.
    private static readonly string[] RequiredTables =
    {
        "visit_requests",
        "visit_request_campuses",
        "visit_instance_form_details",
        "visit_guest_members",
        "visit_instance_guest_members",
    };

    // Columns whose absence means the per-campus content has no home.
    private static readonly (string Table, string Column)[] RequiredColumns =
    {
        ("visit_instance_form_details", "delegation_name"),
        ("visit_instance_form_details", "visit_type"),
        ("visit_instance_form_details", "purpose"),
        ("visit_instance_form_details", "media_consent_status"),
        ("visit_request_campuses", "visit_instance_id"),
        ("visit_request_campuses", "current_host_user_id"),
    };

    // Dropped V1 columns whose reappearance would signal a dual-version regression.
    private static readonly (string Table, string Column)[] ForbiddenV1Columns =
    {
        ("visit_requests", "form_schema_version"),
        ("visit_requests", "delegation_name"),
        ("visit_requests", "visit_type"),
        ("visit_requests", "purpose"),
    };

    public async Task<PureV2ReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var connection = _db.Database.GetDbConnection();
        var opened = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            opened = true;
        }

        try
        {
            var databaseName = await ScalarAsync(connection, "SELECT DATABASE()", cancellationToken);

            var tables = await SchemaNamesAsync(connection,
                "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE()",
                cancellationToken);

            var columns = await ColumnPairsAsync(connection,
                "SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = DATABASE()",
                cancellationToken);

            var missingTables = RequiredTables
                .Where(t => !tables.Contains(t))
                .ToList();

            var missingColumns = RequiredColumns
                .Where(c => !columns.Contains((c.Table, c.Column)))
                .Select(c => $"{c.Table}.{c.Column}")
                .ToList();

            var unexpectedV1 = ForbiddenV1Columns
                .Where(c => columns.Contains((c.Table, c.Column)))
                .Select(c => $"{c.Table}.{c.Column}")
                .ToList();

            return new PureV2ReadinessResult
            {
                DatabaseName = databaseName,
                MissingTables = missingTables,
                MissingColumns = missingColumns,
                UnexpectedV1Columns = unexpectedV1,
                SchemaReady = missingTables.Count == 0 && missingColumns.Count == 0 && unexpectedV1.Count == 0,
            };
        }
        finally
        {
            if (opened)
                await connection.CloseAsync();
        }
    }

    private static async Task<string?> ScalarAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct);
        return result?.ToString();
    }

    private static async Task<HashSet<string>> SchemaNamesAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        var names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            names.Add(reader.GetString(0));
        return names;
    }

    private static async Task<HashSet<(string, string)>> ColumnPairsAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        var pairs = new HashSet<(string, string)>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            pairs.Add((reader.GetString(0).ToLowerInvariant(), reader.GetString(1).ToLowerInvariant()));
        return pairs;
    }
}
