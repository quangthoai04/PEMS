using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Runtime readiness of the connected database for the Pure V2 runtime. A live MySQL connection is not
/// enough: the schema must actually be the per-campus one. This is a SCHEMA check (structure), not a
/// per-record data check — a single old request missing its form detail is a 409 on that endpoint, not a
/// system-wide readiness failure.
/// </summary>
public interface IPureV2SchemaReadiness
{
    Task<PureV2ReadinessResult> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Structured readiness outcome. <see cref="SchemaReady"/> is the AND of "every required table/column
/// present" and "no dropped V1 column has crept back". Lists name exactly what is wrong so the health log
/// can say so without leaking any connection detail or secret.
/// </summary>
public sealed class PureV2ReadinessResult
{
    public bool SchemaReady { get; init; }

    /// <summary>The connected database name (from SELECT DATABASE()); never a full connection string.</summary>
    public string? DatabaseName { get; init; }

    /// <summary>Required Pure V2 tables that are absent.</summary>
    public IReadOnlyList<string> MissingTables { get; init; } = new List<string>();

    /// <summary>Required Pure V2 columns (table.column) that are absent.</summary>
    public IReadOnlyList<string> MissingColumns { get; init; } = new List<string>();

    /// <summary>Dropped V1 columns (table.column) that are present again — a dual-version regression.</summary>
    public IReadOnlyList<string> UnexpectedV1Columns { get; init; } = new List<string>();
}
