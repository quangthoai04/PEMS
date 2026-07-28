using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Execution guards for the integration suite itself.
///
/// The defect these exist for is not "a test failed" — it is "no test ran and nobody noticed". On Dev,
/// <see cref="CanonicalSqlScript.FileName"/> pointed at a script that had been renamed away. The
/// preflight could not resolve it, the suite produced no meaningful run, and the green result meant
/// nothing. The merge review only discovered this because two concurrency tests started failing the
/// moment the path was corrected — that is, the schema contract had been unverified for as long as the
/// name was wrong.
///
/// <para>
/// The guards below are deliberately independent of <see cref="CanonicalSqlScript"/>'s own helpers.
/// <see cref="CanonicalSqlScriptTests"/> asserts the same two facts THROUGH <c>ResolvePath</c> and
/// <c>ComputeSha256</c>; these re-derive them from the literal expected path and a fresh hash, so a bug
/// in the resolver cannot make both suites agree with each other while both are wrong.
/// </para>
/// </summary>
public sealed class CanonicalSqlScriptPathTests
{
    /// <summary>The path the suite claims to be written against, built from the constant, not searched for.</summary>
    private static string ExpectedPath => Path.Combine(
        CanonicalSqlScript.FindRepositoryRoot(), "docs", "database", "scripts", CanonicalSqlScript.FileName);

    [Fact]
    public void CanonicalSqlFileMustExist()
    {
        var path = ExpectedPath;

        Assert.True(
            File.Exists(path),
            $"The canonical schema script is missing.{Environment.NewLine}" +
            $"  expected at: {path}{Environment.NewLine}" +
            $"  FileName constant: {CanonicalSqlScript.FileName}{Environment.NewLine}" +
            "The integration suite cannot import a schema and MUST NOT be treated as having run. " +
            $"If the file was renamed, update {nameof(CanonicalSqlScript)}.{nameof(CanonicalSqlScript.FileName)} " +
            "in the same commit as the rename.");

        // Non-empty: an existing-but-truncated file is the same failure wearing a different hat.
        Assert.True(new FileInfo(path).Length > 100_000,
            $"The canonical schema at {path} is implausibly small ({new FileInfo(path).Length} bytes).");
    }

    [Fact]
    public void ExpectedHashMustMatch()
    {
        var path = ExpectedPath;
        Assert.True(File.Exists(path), $"Canonical schema missing at {path} — see {nameof(CanonicalSqlFileMustExist)}.");

        // Re-derived here rather than delegating to CanonicalSqlScript.ComputeNormalizedSha256, on purpose:
        // if the shared helper's normalisation were wrong, a test that used it would agree with it and both
        // would be wrong together. This spells the rule out independently — strip one leading BOM, fold CRLF
        // and lone CR to LF, encode UTF-8 without a BOM — so the two implementations have to agree.
        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(File.ReadAllBytes(path));
        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        var actual = Convert.ToHexString(
            SHA256.HashData(new UTF8Encoding(false).GetBytes(text))).ToLowerInvariant();

        Assert.True(
            string.Equals(actual, CanonicalSqlScript.ExpectedSha256, StringComparison.OrdinalIgnoreCase),
            $"Canonical schema hash mismatch.{Environment.NewLine}" +
            $"  file:     {path}{Environment.NewLine}" +
            $"  expected: {CanonicalSqlScript.ExpectedSha256}{Environment.NewLine}" +
            $"  actual:   {actual}{Environment.NewLine}" +
            "The schema and the tests written against it have drifted. If the .sql change is deliberate, " +
            $"update {nameof(CanonicalSqlScript)}.{nameof(CanonicalSqlScript.ExpectedSha256)} in the SAME commit " +
            "and record why in the bump comment.");
    }

    /// <summary>
    /// A suite that discovers zero tests exits 0. That is the shape the original defect took, so the
    /// count is asserted rather than assumed. Not pinned to an exact number — this suite is expected to
    /// grow — only to "substantially more than none".
    /// </summary>
    [Fact]
    public void IntegrationSuiteMustDiscoverTests()
    {
        var testMethods = typeof(CanonicalSqlScriptPathTests).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Count(m => m.GetCustomAttributes<FactAttribute>(inherit: true).Any());

        Assert.True(
            testMethods > 100,
            $"The integration assembly discovered only {testMethods} test methods. A suite that finds no " +
            "tests still exits 0, which is exactly how a broken preflight passes for green.");
    }

    /// <summary>
    /// No test in this assembly may be permanently skipped.
    ///
    /// <c>Skip</c> is how a failing integration test becomes invisible without deleting it — the suite
    /// stays green, the count barely moves, and the behaviour is simply no longer checked. If a test
    /// cannot run it should be fixed or removed with the reason recorded, not muted in place.
    /// </summary>
    [Fact]
    public void NoTestMayBeSilentlySkipped()
    {
        var skipped = typeof(CanonicalSqlScriptPathTests).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => new { Type = t, Method = m }))
            .Select(x => new
            {
                x.Type,
                x.Method,
                Skip = x.Method.GetCustomAttributes<FactAttribute>(inherit: true)
                    .Select(a => a.Skip)
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
            })
            .Where(x => x.Skip is not null)
            .Select(x => $"{x.Type.Name}.{x.Method.Name} (Skip = \"{x.Skip}\")")
            .ToList();

        Assert.True(
            skipped.Count == 0,
            $"{skipped.Count} integration test(s) are skipped:{Environment.NewLine}  " +
            string.Join(Environment.NewLine + "  ", skipped));
    }
}
