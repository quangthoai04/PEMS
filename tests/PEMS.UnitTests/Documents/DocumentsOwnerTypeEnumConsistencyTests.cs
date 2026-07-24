using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PEMS.UnitTests.Documents;

/// <summary>
/// Schema/code consistency guard for <c>documents.owner_type</c>.
///
/// Regression for a real defect: the visit-instance photo-upload feature writes and reads
/// <c>documents.owner_type = "VISIT_INSTANCE_MEDIA"</c>, but the master schema's ENUM did not
/// include that value, so every upload failed at the database with MySQL 1265
/// "Data truncated for column 'owner_type'". EF maps the column as a plain string, so an
/// EnsureCreated/InMemory test can NOT catch this — only the real ENUM does. This test locks the
/// enum in the authoritative SQL to the values the C# code actually writes.
///
/// If a new <c>OwnerType = "..."</c> literal is added in code, add it to the enum in the master and
/// fresh-target SQL (and ship a widening ALTER patch for existing databases) — this test will fail
/// until the schema and the code agree.
/// </summary>
public sealed class DocumentsOwnerTypeEnumConsistencyTests
{
    // Every value the code assigns to Document.OwnerType, plus the entity default.
    private static readonly string[] RequiredOwnerTypes =
    {
        "GENERAL", "PARTNER", "VISIT_INSTANCE_MEDIA",
    };

    /// <summary>
    /// The canonical schema script. Resolved by globbing rather than hard-coding a filename: the previous
    /// hard-coded name was renamed out from under this test, which then failed on a missing file instead of
    /// guarding the enum. Exactly one canonical script must exist.
    /// </summary>
    public static IEnumerable<object[]> SchemaFiles()
    {
        var scriptsDir = Path.Combine(RepoRoot(), "docs", "database", "scripts");

        var canonical = Directory.GetFiles(scriptsDir, "PEMS_FULL_*.sql", SearchOption.TopDirectoryOnly);
        Assert.True(canonical.Length == 1,
            $"Expected exactly one canonical PEMS_FULL_*.sql in {scriptsDir}, found {canonical.Length}.");
        yield return new object[] { canonical[0] };

        var freshTarget = Path.Combine(scriptsDir, "phase_1_candidate", "00_fresh_target.sql");
        if (File.Exists(freshTarget))
            yield return new object[] { freshTarget };
    }

    [Theory]
    [MemberData(nameof(SchemaFiles))]
    public void Documents_owner_type_enum_covers_every_value_the_code_writes(string sqlPath)
    {
        Assert.True(File.Exists(sqlPath), $"Schema file not found: {sqlPath}");
        var sql = File.ReadAllText(sqlPath);

        // Pull the owner_type ENUM(...) member list out of the CREATE TABLE documents definition.
        var match = Regex.Match(sql, @"owner_type\s+ENUM\s*\(([^)]*)\)", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"documents.owner_type ENUM(...) not found in {Path.GetFileName(sqlPath)}");

        var members = Regex.Matches(match.Groups[1].Value, @"'([^']*)'")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var required in RequiredOwnerTypes)
            Assert.True(members.Contains(required),
                $"documents.owner_type in {Path.GetFileName(sqlPath)} is missing '{required}'. " +
                $"The code writes it, so a row insert would fail with MySQL 1265. Enum has: {string.Join(",", members)}");
    }

    private static string RepoRoot([CallerFilePath] string thisFile = "")
    {
        var dir = Path.GetDirectoryName(thisFile);
        while (dir != null && !(Directory.Exists(Path.Combine(dir, "backend")) && Directory.Exists(Path.Combine(dir, "tests"))))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new DirectoryNotFoundException($"Could not locate the repo root from {thisFile}.");
    }
}
