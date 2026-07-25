using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Locates, verifies and retargets the ONE canonical PEMS schema script.
///
/// Every rule here is fail-closed by design. The previous bootstrap pointed at a filename that no longer
/// existed and wrapped the import in <c>if (File.Exists(...))</c> with no <c>else</c>, so a renamed script
/// silently produced an EMPTY disposable database and the suite "ran" against nothing.
///
/// It also only replaced the single literal <c>USE `pems_db`;</c>, while the script still contains
/// <c>CREATE DATABASE IF NOT EXISTS `pems_db`</c> — enough to create/touch a real database on a shared
/// MySQL server. Retargeting therefore rewrites EVERY database-selection statement and then re-scans the
/// produced text before anything is sent to the server.
/// </summary>
public static class CanonicalSqlScript
{
    /// <summary>The only accepted schema script. No wildcards, no fallback to historical names.</summary>
    public const string FileName =
        "PEMS_FULL_V2_NO_SEED_DATA_GALLERY.sql";

    /// <summary>
    /// SHA-256 of the canonical script this test suite is written against. Changing the schema is allowed,
    /// but it MUST be a deliberate act: update this constant in the same commit as the .sql change so a
    /// silent drift between schema and tests is impossible.
    /// (2026-07-25) Bumped with the P0 #1 change: users.status ENUM + PENDING_EMAIL_CONFIRMATION and the new
    /// account_email_confirmations table. Also realigns FileName to the canonical file that commit ebf0d69a
    /// left on disk (the previous LATEST name had been removed, so the schema-contract check could not resolve).
    /// </summary>
    /// (2026-07-25, second bump) Re-pinned after commit 59c86766 appended the demo-data refresh block
    /// (deletes + re-inserts of the 3001-3090 / 5001-5160 demo ranges) to the canonical script. The schema
    /// DDL itself is unchanged; only the trailing data block is new, and retargeting still rewrites every
    /// database-selection statement, so a disposable run never touches pems_db.
    public const string ExpectedSha256 =
        "84680cfaf21582af763ebddf5b1b2568a4a22970ac1d60d4f5bce469d4d918fa";

    /// <summary>The database name the canonical script targets by default — never usable from tests.</summary>
    private const string ForbiddenTargetDatabase = "pems_db";

    /// <summary>Disposable databases must match this shape before we ever create or drop one.</summary>
    public static readonly Regex DisposableNamePattern =
        new(@"^pems_test_run_[0-9a-fA-F]{32}$", RegexOptions.Compiled);

    /// <summary>Walks up from the test binaries to the repository root (the folder holding backend/ and tests/).</summary>
    public static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null &&
               !(Directory.Exists(Path.Combine(dir.FullName, "backend")) &&
                 Directory.Exists(Path.Combine(dir.FullName, "tests"))))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not locate the repository root from '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    /// Resolves the canonical script path. Throws when it is missing, or when more than one candidate
    /// schema script exists in the scripts folder (ambiguity must never be resolved by guessing).
    /// </summary>
    public static string ResolvePath(string? repositoryRoot = null)
    {
        var root = repositoryRoot ?? FindRepositoryRoot();
        var scriptsDir = Path.Combine(root, "docs", "database", "scripts");

        if (!Directory.Exists(scriptsDir))
            throw new FileNotFoundException($"Canonical SQL folder not found: {scriptsDir}");

        // Only top-level scripts count; patches/migrations live in sub-folders.
        var candidates = Directory
            .GetFiles(scriptsDir, "PEMS_FULL_*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (candidates.Count == 0)
            throw new FileNotFoundException(
                $"No canonical schema script found in '{scriptsDir}'. Expected '{FileName}'.");

        if (candidates.Count > 1)
            throw new InvalidOperationException(
                "Ambiguous canonical schema: multiple PEMS_FULL_*.sql scripts exist in " +
                $"'{scriptsDir}' ({string.Join(", ", candidates.Select(Path.GetFileName))}). " +
                "Exactly one canonical script must be present.");

        var path = candidates[0];
        if (!string.Equals(Path.GetFileName(path), FileName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Canonical schema filename changed. Expected '{FileName}' but found " +
                $"'{Path.GetFileName(path)}'. Update {nameof(CanonicalSqlScript)} deliberately.");

        return path;
    }

    /// <summary>Lower-case hex SHA-256 of the file's bytes.</summary>
    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>Reads the canonical script, failing when its hash does not match <see cref="ExpectedSha256"/>.</summary>
    public static string ReadVerified(string? repositoryRoot = null)
    {
        var path = ResolvePath(repositoryRoot);
        var actual = ComputeSha256(path);

        if (!string.Equals(actual, ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Canonical SQL hash mismatch for '{Path.GetFileName(path)}'.{Environment.NewLine}" +
                $"  expected SHA-256: {ExpectedSha256}{Environment.NewLine}" +
                $"  actual   SHA-256: {actual}{Environment.NewLine}" +
                $"If the schema changed on purpose, update {nameof(CanonicalSqlScript)}.{nameof(ExpectedSha256)} " +
                "in the same commit.");

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Rewrites every database-selection statement onto <paramref name="targetDatabase"/> and then proves,
    /// by re-scanning the produced text, that nothing can still reach a real database.
    /// </summary>
    public static string Retarget(string sql, string targetDatabase)
    {
        if (!DisposableNamePattern.IsMatch(targetDatabase))
            throw new InvalidOperationException(
                $"Refusing to retarget onto '{targetDatabase}': it is not a disposable pems_test_run_<32hex> name.");

        // CREATE/DROP/USE DATABASE `x` | 'x' | x  →  the disposable target.
        var rewritten = Regex.Replace(
            sql,
            @"(?im)^[ \t]*(CREATE\s+DATABASE(?:\s+IF\s+NOT\s+EXISTS)?|DROP\s+DATABASE(?:\s+IF\s+EXISTS)?|USE)\s+[`'""]?[A-Za-z0-9_$]+[`'""]?",
            m => $"{m.Groups[1].Value} `{targetDatabase}`");

        AssertSafeToImport(rewritten, targetDatabase);
        return rewritten;
    }

    /// <summary>
    /// Matches a database-selection statement only where MySQL can actually parse one: at the start of a
    /// statement, i.e. the start of a line or immediately after a <c>;</c>. This mirrors <see cref="Retarget"/>,
    /// which rewrites exactly those positions. An unanchored search instead flagged the ordinary English word
    /// "use" inside string literals and SIGNAL messages ("...must use SELF_SERVICE source"), which no server
    /// ever executes. MySQL rejects USE mid-statement and inside stored programs, so anchoring loses no real
    /// reachability.
    /// </summary>
    private static readonly Regex DatabaseStatementPattern = new(
        @"(?im)(?:^|;)[ \t]*(CREATE\s+DATABASE|DROP\s+DATABASE|USE)\b\s+(?:IF\s+(?:NOT\s+)?EXISTS\s+)?[`'""]?([A-Za-z0-9_$]+)[`'""]?",
        RegexOptions.Compiled);

    /// <summary>
    /// Matches the mysql client include directives (<c>SOURCE path</c> and its <c>\.</c> shorthand). The
    /// argument must look like a path, because a bare SOURCE word also begins a legitimate column definition
    /// in this schema (<c>source ENUM('MANUAL','OCR',...)</c>).
    /// </summary>
    private static readonly Regex ClientIncludePattern = new(
        @"(?i)^\s*(?:SOURCE\s+|\\\.\s*)\S*(?:[\\/]|\.sql\b)",
        RegexOptions.Compiled);

    /// <summary>
    /// Post-retarget safety gate. Throws when the script could still touch a protected database or pull in
    /// another file. Comment lines are ignored: the canonical script legitimately mentions pems_db in prose.
    /// </summary>
    public static void AssertSafeToImport(string sql, string targetDatabase)
    {
        var offenders = new List<string>();
        var lines = sql.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("--", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            // Any database-selection statement must name the disposable target.
            foreach (Match dbStatement in DatabaseStatementPattern.Matches(line))
            {
                if (!string.Equals(dbStatement.Groups[2].Value, targetDatabase, StringComparison.Ordinal))
                    offenders.Add($"line {i + 1}: database statement targets '{dbStatement.Groups[2].Value}'");
            }

            // Qualified references to the forbidden database, e.g. `pems_db`.users
            if (Regex.IsMatch(line, $@"(?i)[`'""]?\b{ForbiddenTargetDatabase}\b[`'""]?\s*\."))
                offenders.Add($"line {i + 1}: qualified reference to '{ForbiddenTargetDatabase}'");

            // Client-side include directives must never appear.
            if (ClientIncludePattern.IsMatch(line))
                offenders.Add($"line {i + 1}: client include directive");
        }

        if (offenders.Count > 0)
            throw new InvalidOperationException(
                "Refusing to import: the retargeted script can still reach a database outside the disposable " +
                $"target '{targetDatabase}'.{Environment.NewLine}  " +
                string.Join(Environment.NewLine + "  ", offenders.Take(20)));
    }

    /// <summary>Generates a fresh disposable database name matching the allowlist.</summary>
    public static string NewDisposableDatabaseName() => "pems_test_run_" + Guid.NewGuid().ToString("N");
}
