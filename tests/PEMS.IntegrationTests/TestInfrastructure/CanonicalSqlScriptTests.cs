using System;
using System.IO;
using Xunit;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Guards the fail-closed bootstrap contract itself.
///
/// Regression for a real defect: the bootstrap used to reference a schema file that had been renamed and
/// wrapped the import in <c>if (File.Exists(...))</c> with no <c>else</c>. The suite then ran against an
/// EMPTY database and its results proved nothing. It also rewrote only one <c>USE `pems_db`;</c> literal
/// while the script still contained <c>CREATE DATABASE IF NOT EXISTS `pems_db`</c>.
///
/// These tests are pure file/string checks — they never connect to a database.
/// </summary>
public sealed class CanonicalSqlScriptTests
{
    private static string SampleTarget => "pems_test_run_" + new string('a', 32);

    [Fact]
    public void Resolves_the_single_canonical_script()
    {
        var path = CanonicalSqlScript.ResolvePath();

        Assert.True(File.Exists(path), $"Canonical SQL not found at {path}");
        Assert.Equal(CanonicalSqlScript.FileName, Path.GetFileName(path));
    }

    [Fact]
    public void Canonical_script_hash_matches_the_pinned_value()
    {
        var path = CanonicalSqlScript.ResolvePath();
        var actual = CanonicalSqlScript.ComputeSha256(path);

        Assert.True(
            string.Equals(actual, CanonicalSqlScript.ExpectedSha256, StringComparison.OrdinalIgnoreCase),
            $"Canonical SQL changed. Update CanonicalSqlScript.ExpectedSha256 deliberately.{Environment.NewLine}" +
            $"expected {CanonicalSqlScript.ExpectedSha256}{Environment.NewLine}actual   {actual}");
    }

    [Fact]
    public void Missing_repository_root_fails_closed()
        => Assert.ThrowsAny<Exception>(() => CanonicalSqlScript.ResolvePath(Path.Combine(Path.GetTempPath(), "pems-not-a-repo")));

    [Fact]
    public void ReadVerified_returns_the_real_script()
    {
        var sql = CanonicalSqlScript.ReadVerified();

        Assert.Contains("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("visit_instance_form_details", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Retarget_rewrites_every_database_selection_statement()
    {
        var target = SampleTarget;
        var retargeted = CanonicalSqlScript.Retarget(CanonicalSqlScript.ReadVerified(), target);

        // The real script contains BOTH a CREATE DATABASE and a USE for pems_db.
        Assert.Contains($"CREATE DATABASE IF NOT EXISTS `{target}`", retargeted, StringComparison.Ordinal);
        Assert.Contains($"USE `{target}`;", retargeted, StringComparison.Ordinal);

        // No executable statement may still name pems_db (comments are allowed to mention it).
        foreach (var raw in retargeted.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("--", StringComparison.Ordinal))
                continue;

            Assert.DoesNotContain("`pems_db`", line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Retarget_refuses_a_non_disposable_target()
    {
        Assert.Throws<InvalidOperationException>(() => CanonicalSqlScript.Retarget("USE `pems_db`;", "pems_db"));
        Assert.Throws<InvalidOperationException>(() => CanonicalSqlScript.Retarget("USE `pems_db`;", "pems_test"));
        Assert.Throws<InvalidOperationException>(() => CanonicalSqlScript.Retarget("USE `pems_db`;", "pems_test_run_short"));
    }

    [Fact]
    public void Safety_scan_rejects_a_leftover_database_statement()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CanonicalSqlScript.AssertSafeToImport("USE `pems_db`;", SampleTarget));

        Assert.Contains("pems_db", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Safety_scan_rejects_a_qualified_reference_to_the_protected_database()
        => Assert.Throws<InvalidOperationException>(() =>
            CanonicalSqlScript.AssertSafeToImport("DELETE FROM `pems_db`.users;", SampleTarget));

    [Theory]
    [InlineData("SOURCE other.sql;")]
    [InlineData("\\. other.sql")]
    public void Safety_scan_rejects_client_include_directives(string line)
        => Assert.Throws<InvalidOperationException>(() => CanonicalSqlScript.AssertSafeToImport(line, SampleTarget));

    [Fact]
    public void Safety_scan_allows_comments_mentioning_the_protected_database()
    {
        var target = SampleTarget;
        // The canonical script's header legitimately describes pems_db in prose.
        CanonicalSqlScript.AssertSafeToImport(
            $"-- destructive FRESH-CREATE build for disposable `pems_db` only.\nUSE `{target}`;", target);
    }

    [Fact]
    public void Disposable_name_pattern_only_accepts_generated_names()
    {
        Assert.Matches(CanonicalSqlScript.DisposableNamePattern, CanonicalSqlScript.NewDisposableDatabaseName());

        foreach (var protectedName in new[] { "pems_db", "pems_test", "pems_pr3_test", "pems_test_run_invalid" })
            Assert.DoesNotMatch(CanonicalSqlScript.DisposableNamePattern, protectedName);
    }
}
