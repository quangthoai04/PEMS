using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PEMS.Infrastructure.Persistence;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Removes a suite's fixture rows by following the foreign keys the database actually declares, rather
/// than a hand-written list of DELETE statements.
///
/// <para>
/// The list is the thing that keeps breaking. A suite deletes <c>files</c>, some other suite leaves a
/// <c>documents</c> row pointing at one, and because the constraint is ON DELETE RESTRICT the whole class
/// dies in setup before it reaches a line of product code. Adding the missing DELETE fixes that one run
/// and moves the failure to the next referrer — <c>files</c> alone has eleven, <c>users</c> twelve,
/// <c>campuses</c> six. Nobody can keep four such lists correct by hand, and a new foreign key silently
/// invalidates all of them.
/// </para>
/// <para>
/// So the order is read from <c>information_schema</c> at run time and re-derived on every schema change
/// for free. A caller declares which rows it owns; everything reachable from those rows by an incoming
/// reference is removed depth-first, and the declared rows go last.
/// </para>
///
/// <example>
/// <code>
/// await FixtureCleanup.For(db)
///     .Root("files", $"uploaded_by BETWEEN {Base} AND {Base + 100}")
///     .Root("users", $"user_id BETWEEN {Base} AND {Base + 100}")
///     .RunAsync();
/// </code>
/// </example>
/// </summary>
public sealed class FixtureCleanup
{
    private readonly ApplicationDbContext _db;
    private readonly List<(string Table, string Where)> _roots = new();

    private FixtureCleanup(ApplicationDbContext db) => _db = db;

    public static FixtureCleanup For(ApplicationDbContext db) => new(db);

    /// <summary>
    /// Declares rows this fixture owns: every row of <paramref name="table"/> matching
    /// <paramref name="where"/>.
    ///
    /// <para>
    /// Roots are processed in declaration order, and the order can matter. <c>files.uploaded_by</c>
    /// references <c>users</c> ON DELETE SET NULL, so deleting the users first blanks the very column a
    /// later <c>files</c> root identifies its rows by — the rows survive, unowned and invisible. Declare
    /// <c>files</c> before <c>users</c>.
    /// </para>
    /// </summary>
    public FixtureCleanup Root(string table, string where)
    {
        RequireIdentifier(table, nameof(table));
        if (string.IsNullOrWhiteSpace(where))
            throw new ArgumentException("A root needs a predicate; cleaning a whole table is never correct.", nameof(where));

        _roots.Add((table, where));
        return this;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        RequireDisposableDatabase();

        var graph = await ForeignKeyGraph.LoadAsync(_db, ct);

        foreach (var (table, where) in _roots)
        {
            var key = graph.SingleColumnKeyOf(table);

            // Resolved once, up front, and deleted by id afterwards. Re-running the predicate at the end
            // would be wrong whenever it reads a table the traversal has meanwhile emptied — a root of
            // "sent_emails addressed to <marker>" identifies its rows through sent_email_recipients, and
            // those recipients are gone by then, so the predicate would match nothing and the root would
            // silently survive.
            var ids = await ScalarsAsync(_db, $"SELECT `{key}` FROM `{table}` WHERE {where}", ct);
            if (ids.Count == 0)
                continue;

            var scope = $"`{key}` IN ({Literals(ids)})";
            await ClearReferrersAsync(graph, table, scope, new List<string> { table }, ct);
            await _db.Database.ExecuteSqlRawAsync($"DELETE FROM `{table}` WHERE {scope}", ct);
        }
    }

    /// <summary>
    /// Empties every table holding a reference to <paramref name="parentIds"/>, deepest first.
    ///
    /// <para>
    /// SET NULL edges are skipped deliberately. The database blanks those columns by itself, and the
    /// referring row belongs to somebody else — a <c>partners</c> row whose logo happens to be a fixture
    /// file must survive with a null logo, not be deleted. CASCADE edges are followed and deleted
    /// explicitly: the outcome is what the database would have done anyway, and doing it here keeps one
    /// ordering rule instead of two.
    /// </para>
    /// </summary>
    private async Task ClearReferrersAsync(
        ForeignKeyGraph graph, string table, string scope, List<string> path, CancellationToken ct)
    {
        var edges = graph.ReferrersOf(table);
        if (edges.Count == 0)
            return;

        // A foreign key names the column it points at, which is not always a primary key — several link
        // tables here have composite keys and no addressable id at all. Reading the referenced column is
        // therefore both more general and more accurate than insisting on a surrogate key, and it is what
        // lets the traversal pass through tables like visit_instance_guest_members.
        var referencedValues = new Dictionary<string, IReadOnlyList<object>>(StringComparer.Ordinal);

        foreach (var edge in edges)
        {
            if (!referencedValues.TryGetValue(edge.ParentColumn, out var values))
            {
                values = await ScalarsAsync(
                    _db, $"SELECT DISTINCT `{edge.ParentColumn}` FROM `{table}` WHERE {scope}", ct);
                referencedValues[edge.ParentColumn] = values;
            }

            if (values.Count == 0)
                continue;

            if (path.Contains(edge.ChildTable, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    "Foreign-key cycle reached while cleaning fixture rows, so no safe delete order exists: "
                    + string.Join(" -> ", path.Append(edge.ChildTable))
                    + $" (via {edge.ChildTable}.{edge.ChildColumn} -> {edge.ParentTable}.{edge.ParentColumn}). "
                    + "Break the cycle or narrow the fixture root; this helper will not widen the delete to compensate.");

            var childScope = $"`{edge.ChildColumn}` IN ({Literals(values)})";

            path.Add(edge.ChildTable);
            await ClearReferrersAsync(graph, edge.ChildTable, childScope, path, ct);
            path.RemoveAt(path.Count - 1);

            await _db.Database.ExecuteSqlRawAsync($"DELETE FROM `{edge.ChildTable}` WHERE {childScope}", ct);
        }
    }

    /// <summary>
    /// Refuses to run anywhere but the disposable database this run created.
    ///
    /// <para>
    /// The helper deletes by following foreign keys, so on a real database a single mis-declared root
    /// would reach a long way. The name pattern is the same one <see cref="DisposableDatabaseManager"/>
    /// requires before it will drop a database.
    /// </para>
    /// </summary>
    private void RequireDisposableDatabase()
    {
        var database = _db.Database.GetDbConnection().Database;

        if (!CanonicalSqlScript.DisposableNamePattern.IsMatch(database))
            throw new InvalidOperationException(
                $"FixtureCleanup refuses to run against '{database}': it is not a disposable "
                + "pems_test_run_<32hex> database. Integration fixtures must never clean a real schema.");
    }

    private static async Task<IReadOnlyList<object>> ScalarsAsync(
        ApplicationDbContext db, string sql, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        if (db.Database.CurrentTransaction is { } tx)
            cmd.Transaction = tx.GetDbTransaction();

        var values = new List<object>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (!reader.IsDBNull(0))
                values.Add(reader.GetValue(0));
        }

        return values;
    }

    /// <summary>
    /// Ids as SQL literals. They come from the database's own key columns one statement earlier, and any
    /// value that is not a plain integer is rejected rather than quoted — this helper is only ever asked
    /// to follow numeric surrogate keys, and guessing at anything else is how a cleaner starts deleting
    /// rows nobody asked it to.
    /// </summary>
    private static string Literals(IReadOnlyList<object> ids)
    {
        var rendered = ids.Select(id =>
        {
            var text = Convert.ToString(id, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            if (!Regex.IsMatch(text, @"^\d+$"))
                throw new InvalidOperationException(
                    $"FixtureCleanup only follows integer keys; got '{text}'. Declare a narrower root.");
            return text;
        });

        return string.Join(",", rendered);
    }

    private static void RequireIdentifier(string value, string parameter)
    {
        if (!Regex.IsMatch(value ?? "", @"^[A-Za-z_][A-Za-z0-9_]*$"))
            throw new ArgumentException($"'{value}' is not a plain SQL identifier.", parameter);
    }

    /// <summary>
    /// The incoming-reference graph, read once per process from <c>information_schema</c>.
    ///
    /// <para>
    /// Cached because it cannot change during a run: the disposable database is imported once from the
    /// canonical script and no test issues DDL.
    /// </para>
    /// </summary>
    private sealed class ForeignKeyGraph
    {
        private static ForeignKeyGraph? _cached;
        private static readonly SemaphoreSlim _gate = new(1, 1);

        private readonly Dictionary<string, List<Edge>> _referrers;
        private readonly Dictionary<string, string> _singleColumnKeys;

        private ForeignKeyGraph(Dictionary<string, List<Edge>> referrers, Dictionary<string, string> keys)
        {
            _referrers = referrers;
            _singleColumnKeys = keys;
        }

        internal readonly record struct Edge(
            string ParentTable, string ParentColumn, string ChildTable, string ChildColumn);

        public IReadOnlyList<Edge> ReferrersOf(string table)
            => _referrers.TryGetValue(table, out var edges) ? edges : Array.Empty<Edge>();

        /// <summary>
        /// The table's single-column primary key, needed for declared roots only — their ids are pinned
        /// before the traversal starts so the delete cannot depend on a predicate the traversal has since
        /// invalidated. Tables passed through on the way down are addressed by the column their foreign
        /// key names, so a composite key there is fine.
        /// </summary>
        public string SingleColumnKeyOf(string table)
            => _singleColumnKeys.TryGetValue(table, out var key)
                ? key
                : throw new InvalidOperationException(
                    $"Fixture root '{table}' has no single-column primary key, so its rows cannot be "
                    + "pinned before cleaning. Declare the root on a table that has one.");

        public static async Task<ForeignKeyGraph> LoadAsync(ApplicationDbContext db, CancellationToken ct)
        {
            if (_cached is not null)
                return _cached;

            await _gate.WaitAsync(ct);
            try
            {
                if (_cached is not null)
                    return _cached;

                var referrers = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
                var keys = new Dictionary<string, string>(StringComparer.Ordinal);

                var connection = db.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync(ct);

                using (var cmd = connection.CreateCommand())
                {
                    // SET NULL is excluded here rather than at the call site: those rows are not ours.
                    cmd.CommandText = """
                        SELECT rc.REFERENCED_TABLE_NAME, kcu.REFERENCED_COLUMN_NAME,
                               rc.TABLE_NAME, kcu.COLUMN_NAME
                        FROM information_schema.REFERENTIAL_CONSTRAINTS rc
                        JOIN information_schema.KEY_COLUMN_USAGE kcu
                          ON kcu.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA
                         AND kcu.CONSTRAINT_NAME   = rc.CONSTRAINT_NAME
                        WHERE rc.CONSTRAINT_SCHEMA = DATABASE()
                          AND rc.DELETE_RULE <> 'SET NULL'
                        ORDER BY rc.REFERENCED_TABLE_NAME, rc.TABLE_NAME, kcu.COLUMN_NAME
                        """;

                    using var reader = await cmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        var edge = new Edge(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
                        if (!referrers.TryGetValue(edge.ParentTable, out var list))
                            referrers[edge.ParentTable] = list = new List<Edge>();
                        list.Add(edge);
                    }
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = """
                        SELECT k.TABLE_NAME, MIN(k.COLUMN_NAME), COUNT(*)
                        FROM information_schema.KEY_COLUMN_USAGE k
                        WHERE k.CONSTRAINT_SCHEMA = DATABASE() AND k.CONSTRAINT_NAME = 'PRIMARY'
                        GROUP BY k.TABLE_NAME
                        HAVING COUNT(*) = 1
                        """;

                    using var reader = await cmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                        keys[reader.GetString(0)] = reader.GetString(1);
                }

                return _cached = new ForeignKeyGraph(referrers, keys);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
