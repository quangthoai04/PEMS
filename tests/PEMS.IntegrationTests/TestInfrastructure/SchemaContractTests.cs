using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Proves the EF model and the canonical schema describe the SAME database.
///
/// This project is database-first with no EF migrations, so nothing forces the two to agree: the model can
/// name a column the schema does not have (which only surfaces at runtime as
/// <c>Unknown column '…' in 'field list'</c>), or declare a delete behaviour the database does not enforce.
/// Both classes of drift shipped undetected before — the Pure V2 cut left twelve phantom mappings behind.
///
/// Everything runs against the disposable database built from the pinned canonical script, so the schema
/// under test is exactly the one the repository ships. Real databases are never touched.
/// </summary>
public sealed class SchemaContractTests
{
    private static string ConnString => DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static bool? _dbUp;

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }

        Assert.True(_dbUp!.Value, "The disposable schema database could not be created — see the bootstrap error.");
    }

    // ── Live schema snapshot ──────────────────────────────────────────────────

    private sealed record ColumnInfo(string Table, string Column, bool IsNullable, string DataType);

    private sealed record ForeignKeyInfo(
        string Constraint, string Table, string Column, int Position, string ReferencedTable, string DeleteRule);

    /// <summary>
    /// Reads through the context's OWN connection rather than opening a second one. The suite's connection
    /// string carries MySqlConnector options (GuidFormat) that the Oracle client used by the bootstrap
    /// rejects outright, and reusing EF's connection also guarantees we inspect the very database EF is
    /// mapped against.
    /// </summary>
    private static List<T> Query<T>(ApplicationDbContext db, string sql, Func<System.Data.Common.DbDataReader, T> read)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            var rows = new List<T>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) rows.Add(read(reader));
            return rows;
        }
        finally
        {
            if (opened) connection.Close();
        }
    }

    private static List<ColumnInfo> ReadColumns(ApplicationDbContext db) => Query(db,
        "SELECT table_name, column_name, is_nullable, data_type FROM information_schema.columns " +
        "WHERE table_schema = DATABASE();",
        r => new ColumnInfo(
            r.GetString(0), r.GetString(1),
            string.Equals(r.GetString(2), "YES", StringComparison.OrdinalIgnoreCase),
            r.GetString(3)));

    /// <summary>
    /// One row per foreign-key COLUMN, carrying its constraint name, ordinal and referenced table.
    ///
    /// Grouping by constraint rather than by column matters: a column can belong to more than one foreign
    /// key (<c>visit_photo_face_detections.visit_instance_id</c> does), and a column-keyed comparison then
    /// pairs every EF relationship against every rule on that column and invents mismatches that do not exist.
    /// </summary>
    private static List<ForeignKeyInfo> ReadForeignKeys(ApplicationDbContext db) => Query(db,
        "SELECT r.constraint_name, k.table_name, k.column_name, k.ordinal_position, " +
        "       k.referenced_table_name, r.delete_rule " +
        "FROM information_schema.referential_constraints r " +
        "JOIN information_schema.key_column_usage k " +
        "  ON k.constraint_schema = r.constraint_schema AND k.constraint_name = r.constraint_name " +
        "WHERE r.constraint_schema = DATABASE();",
        r => new ForeignKeyInfo(
            r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetString(4), r.GetString(5)));

    private static HashSet<string> ReadBaseTables(ApplicationDbContext db) => new(
        Query(db,
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE';",
            r => r.GetString(0)),
        StringComparer.OrdinalIgnoreCase);

    // ── 1. The schema itself ──────────────────────────────────────────────────

    [Fact]
    public void Canonical_schema_imports_with_the_expected_shape()
    {
        RequireDb();
        using var db = NewContext();

        var tables = ReadBaseTables(db);
        Assert.Equal(DisposableDatabaseManager.ExpectedBaseTableCount, tables.Count);

        // The bootstrap asserts these too; repeating them here means a weakened bootstrap cannot pass
        // silently. Both are Pure V2's defining property.
        var columns = ReadColumns(db);
        Assert.DoesNotContain(columns, c => c.Column == "form_schema_version");

        var legacyGlobalFormColumns = new[]
        {
            "delegation_name", "visit_type", "visit_type_other", "purpose", "working_content",
            "working_language", "transportation_note", "media_consent_status", "media_consent_note",
            "note_to_fptu",
        };
        Assert.DoesNotContain(columns,
            c => c.Table == "visit_requests" && legacyGlobalFormColumns.Contains(c.Column));

        // Form content must live on the per-campus detail instead.
        Assert.Contains(columns, c => c.Table == "visit_instance_form_details" && c.Column == "delegation_name");

        // The media-consent-note cutover, asserted on the table that actually changed. `media_consent_note`
        // appears twice more in this file — in the legacy list above — but that list is about `visit_requests`,
        // where the column was already gone, so it could not have caught the per-campus one still being there.
        // The consent STATUS stays: only the note was dropped, replaced by one general note to FPTU.
        Assert.DoesNotContain(columns,
            c => c.Table == "visit_instance_form_details" && c.Column == "media_consent_note");
        Assert.Contains(columns, c => c.Table == "visit_instance_form_details" && c.Column == "notes");
        Assert.Contains(columns, c => c.Table == "visit_instance_form_details" && c.Column == "media_consent_status");

        // No staging/seed helper objects may survive an import.
        Assert.DoesNotContain(tables, t => t.StartsWith("pems_seed_", StringComparison.OrdinalIgnoreCase));
    }

    // ── 2. Every mapped table and column must exist ───────────────────────────

    /// <summary>
    /// The direct guard against the phantom-mapping class of defect: a property EF maps to a column the
    /// canonical schema does not have. Such a mapping compiles, passes unit tests against the in-memory
    /// provider, and only fails when a real query reaches MySQL.
    /// </summary>
    [Fact]
    public void Every_mapped_table_and_column_exists_in_the_canonical_schema()
    {
        RequireDb();
        using var db = NewContext();

        var tables = ReadBaseTables(db);
        var columns = ReadColumns(db)
            .ToLookup(c => c.Table, StringComparer.OrdinalIgnoreCase);

        var missing = new List<string>();

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null) continue; // owned/keyless projections with no table of their own

            if (!tables.Contains(table))
            {
                missing.Add($"{entity.ClrType.Name} → missing table `{table}`");
                continue;
            }

            var actual = new HashSet<string>(
                columns[table].Select(c => c.Column), StringComparer.OrdinalIgnoreCase);

            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(
                    StoreObjectIdentifier.Table(table, entity.GetSchema()));

                if (column is not null && !actual.Contains(column))
                    missing.Add($"{entity.ClrType.Name}.{property.Name} → missing column `{table}`.`{column}`");
            }
        }

        Assert.True(missing.Count == 0,
            "The EF model maps columns the canonical schema does not have. Every one of these throws " +
            $"'Unknown column' the first time it is queried:{Environment.NewLine}  " +
            string.Join(Environment.NewLine + "  ", missing));
    }

    // ── 3. Nullability (GAP-017) ──────────────────────────────────────────────

    /// <summary>
    /// A column the database allows to be NULL, mapped as required, makes EF materialise existing rows into
    /// a null in a non-nullable CLR property; the reverse silently accepts writes the database will reject.
    /// Generated columns are skipped: their nullability is a property of the expression, not of the model.
    /// </summary>
    [Fact]
    public void Mapped_nullability_matches_the_canonical_schema()
    {
        RequireDb();
        using var db = NewContext();

        var columns = ReadColumns(db)
            .ToDictionary(c => (c.Table, c.Column), c => c, TableColumnComparer.Instance);

        var mismatches = new List<string>();

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null) continue;

            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(StoreObjectIdentifier.Table(table, entity.GetSchema()));
                if (column is null) continue;
                if (!columns.TryGetValue((table, column), out var info)) continue; // reported by the test above

                // A store-generated value is written by the database, so EF's requiredness says nothing
                // about what a caller must supply.
                if (property.ValueGenerated != ValueGenerated.Never) continue;

                if (property.IsNullable != info.IsNullable)
                {
                    mismatches.Add(
                        $"`{table}`.`{column}`: database is {(info.IsNullable ? "NULL-able" : "NOT NULL")} " +
                        $"but {entity.ClrType.Name}.{property.Name} is mapped " +
                        $"{(property.IsNullable ? "optional" : "required")}");
                }
            }
        }

        Assert.True(mismatches.Count == 0,
            $"EF nullability disagrees with the canonical schema:{Environment.NewLine}  " +
            string.Join(Environment.NewLine + "  ", mismatches));
    }

    // ── 4. Delete behaviour (GAP-014) ─────────────────────────────────────────

    /// <summary>
    /// Maps an EF delete behaviour to the SQL rules that are consistent with it.
    ///
    /// EF does not create these constraints here (database-first, no migrations); it decides what happens
    /// to entities it is TRACKING when a principal is deleted. If the two disagree, the same delete produces
    /// different outcomes depending on whether the graph happened to be loaded — the worst kind of bug to
    /// reproduce. <c>NO ACTION</c> and <c>RESTRICT</c> are the same thing in InnoDB.
    /// </summary>
    private static string[] AcceptableSqlRules(DeleteBehavior behavior) => behavior switch
    {
        DeleteBehavior.Cascade => new[] { "CASCADE" },
        DeleteBehavior.SetNull => new[] { "SET NULL" },
        DeleteBehavior.Restrict => new[] { "RESTRICT", "NO ACTION" },
        DeleteBehavior.NoAction => new[] { "RESTRICT", "NO ACTION" },

        // Client-side variants leave the database rule untouched on purpose: EF fixes up the tracked graph
        // and the database is expected to refuse or ignore the rest.
        DeleteBehavior.ClientSetNull => new[] { "SET NULL", "RESTRICT", "NO ACTION" },
        DeleteBehavior.ClientCascade => new[] { "CASCADE", "RESTRICT", "NO ACTION" },
        DeleteBehavior.ClientNoAction => new[] { "RESTRICT", "NO ACTION" },
        _ => Array.Empty<string>(),
    };

    [Fact]
    public void Mapped_delete_behaviour_matches_the_canonical_foreign_keys()
    {
        RequireDb();
        using var db = NewContext();

        // constraint → (table, ordered columns, referenced table, rule)
        var sqlConstraints = ReadForeignKeys(db)
            .GroupBy(fk => fk.Constraint, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var ordered = g.OrderBy(x => x.Position).ToList();
                return (
                    Table: ordered[0].Table,
                    Columns: string.Join(",", ordered.Select(x => x.Column.ToLowerInvariant())),
                    Referenced: ordered[0].ReferencedTable,
                    Rule: ordered[0].DeleteRule);
            })
            .ToLookup(
                x => (x.Table.ToLowerInvariant(), x.Columns),
                x => (x.Referenced, x.Rule));

        var mismatches = new List<string>();

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null) continue;

            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());

            foreach (var fk in entity.GetForeignKeys())
            {
                var columns = fk.Properties
                    .Select(p => p.GetColumnName(store)?.ToLowerInvariant())
                    .ToList();
                if (columns.Any(c => c is null)) continue;

                var key = (table.ToLowerInvariant(), string.Join(",", columns));
                var candidates = sqlConstraints[key].ToList();
                if (candidates.Count == 0) continue; // no database-level constraint over these columns

                // A column set can carry more than one constraint; the relationship's own principal table
                // selects the right one, and only if that is ambiguous do we accept any of them.
                var principal = fk.PrincipalEntityType.GetTableName();
                var matching = candidates
                    .Where(c => string.Equals(c.Referenced, principal, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matching.Count == 0) matching = candidates;

                var acceptable = AcceptableSqlRules(fk.DeleteBehavior);

                // Satisfied by any constraint that agrees — the ambiguous case must not fail spuriously.
                if (!matching.Any(m => acceptable.Contains(m.Rule, StringComparer.OrdinalIgnoreCase)))
                {
                    var rules = string.Join(" / ", matching.Select(m => $"{m.Rule} → {m.Referenced}").Distinct());
                    mismatches.Add(
                        $"`{table}`.({string.Join(", ", columns)}) → {entity.ClrType.Name}: " +
                        $"database says ON DELETE {rules}, EF says {fk.DeleteBehavior}");
                }
            }
        }

        Assert.True(mismatches.Count == 0,
            "EF delete behaviour disagrees with the canonical foreign keys. The database rule is " +
            $"authoritative — change the model, not the schema:{Environment.NewLine}  " +
            string.Join(Environment.NewLine + "  ", mismatches));
    }

    /// <summary>A SET NULL relationship over a non-nullable column can never be satisfied.</summary>
    [Fact]
    public void Set_null_relationships_only_target_nullable_columns()
    {
        RequireDb();
        using var db = NewContext();

        var columns = ReadColumns(db).ToDictionary(c => (c.Table, c.Column), c => c, TableColumnComparer.Instance);
        var offenders = new List<string>();

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null) continue;

            foreach (var fk in entity.GetForeignKeys()
                         .Where(f => f.DeleteBehavior is DeleteBehavior.SetNull or DeleteBehavior.ClientSetNull))
            {
                foreach (var property in fk.Properties)
                {
                    var column = property.GetColumnName(StoreObjectIdentifier.Table(table, entity.GetSchema()));
                    if (column is null || !columns.TryGetValue((table, column), out var info)) continue;

                    if (!info.IsNullable)
                        offenders.Add($"`{table}`.`{column}` is NOT NULL but {entity.ClrType.Name} declares {fk.DeleteBehavior}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            $"SET NULL declared over a non-nullable column:{Environment.NewLine}  " +
            string.Join(Environment.NewLine + "  ", offenders));
    }

    private sealed class TableColumnComparer : IEqualityComparer<(string Table, string Column)>
    {
        public static readonly TableColumnComparer Instance = new();

        public bool Equals((string Table, string Column) x, (string Table, string Column) y)
            => StringComparer.OrdinalIgnoreCase.Equals(x.Table, y.Table)
               && StringComparer.OrdinalIgnoreCase.Equals(x.Column, y.Column);

        public int GetHashCode((string Table, string Column) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Table),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Column));
    }
}
