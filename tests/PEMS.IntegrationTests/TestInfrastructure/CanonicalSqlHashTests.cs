using System;
using System.IO;
using System.Text;
using Xunit;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Tests for the canonical-schema hashing RULE itself, as opposed to the schema it is applied to.
///
/// <para>
/// The rule used to be "SHA-256 of the file's bytes", which quietly made the schema contract depend on
/// how the repository had been checked out. <c>.gitattributes</c> declares <c>* text=auto</c>, so git
/// stores the script with LF and hands a Windows worktree the same content with CRLF: one file, two
/// hashes, and a pin that can only ever satisfy one platform. Every local gate on this branch passed on
/// Windows while CI failed on Linux for exactly this reason — the schema had not drifted at all.
/// </para>
/// <para>
/// The tests below pin the rule from both directions, and the second direction is the one that matters.
/// It is easy to make a hash "portable" by normalising so aggressively that it stops noticing anything:
/// trim the text, collapse runs of whitespace, and CRLF-vs-LF certainly stops mattering — along with a
/// missing space in a SIGNAL message. So three tests assert that portability noise is ignored, and two
/// assert that real edits, including whitespace-only ones, still change the hash.
/// </para>
/// <para>
/// These are pure string and temp-file checks; nothing here connects to a database.
/// </para>
/// </summary>
public sealed class CanonicalSqlHashTests
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes <paramref name="text"/> verbatim — no encoder-inserted BOM and, critically, no line-ending
    /// rewriting. <c>File.WriteAllText</c> would be wrong here: these tests exist to compare a CRLF file
    /// against an LF one, so the bytes must land exactly as specified.
    /// </summary>
    private static string WriteTemp(string text, bool withBom = false)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pems-hash-{Guid.NewGuid():N}.sql");
        var body = Utf8NoBom.GetBytes(text);

        if (!withBom)
        {
            File.WriteAllBytes(path, body);
            return path;
        }

        var bytes = new byte[3 + body.Length];
        bytes[0] = 0xEF; bytes[1] = 0xBB; bytes[2] = 0xBF;
        body.CopyTo(bytes, 3);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private const string SqlLf = "CREATE TABLE example (\n  id BIGINT NOT NULL\n);\n";

    // ── Portability noise MUST be ignored ──────────────────────────────────────────────────────

    [Fact]
    public void Lf_and_crlf_hash_identically()
    {
        var lf = WriteTemp(SqlLf);
        var crlf = WriteTemp(SqlLf.Replace("\n", "\r\n"));
        try
        {
            // Guard the guard: if these two files were byte-identical the assertion below would be vacuous.
            Assert.NotEqual(File.ReadAllBytes(lf).Length, File.ReadAllBytes(crlf).Length);

            Assert.Equal(
                CanonicalSqlScript.ComputeNormalizedSha256(lf),
                CanonicalSqlScript.ComputeNormalizedSha256(crlf));
        }
        finally { File.Delete(lf); File.Delete(crlf); }
    }

    [Fact]
    public void Lone_cr_and_lf_hash_identically()
    {
        var lf = WriteTemp(SqlLf);
        var cr = WriteTemp(SqlLf.Replace("\n", "\r"));
        try
        {
            Assert.Equal(
                CanonicalSqlScript.ComputeNormalizedSha256(lf),
                CanonicalSqlScript.ComputeNormalizedSha256(cr));
        }
        finally { File.Delete(lf); File.Delete(cr); }
    }

    [Fact]
    public void Bom_and_no_bom_hash_identically()
    {
        var plain = WriteTemp(SqlLf);
        var withBom = WriteTemp(SqlLf, withBom: true);
        try
        {
            Assert.Equal(File.ReadAllBytes(plain).Length + 3, File.ReadAllBytes(withBom).Length);

            Assert.Equal(
                CanonicalSqlScript.ComputeNormalizedSha256(plain),
                CanonicalSqlScript.ComputeNormalizedSha256(withBom));
        }
        finally { File.Delete(plain); File.Delete(withBom); }
    }

    /// <summary>Only the FIRST U+FEFF is a BOM; one appearing later is content and must count.</summary>
    [Fact]
    public void A_later_u_feff_is_content_and_changes_the_hash()
    {
        Assert.NotEqual(
            CanonicalSqlScript.ComputeNormalizedSha256OfText(SqlLf),
            CanonicalSqlScript.ComputeNormalizedSha256OfText(SqlLf.Replace("id BIGINT", "id\uFEFF BIGINT")));
    }

    // ── Real content changes MUST still be caught ──────────────────────────────────────────────

    [Fact]
    public void A_type_change_changes_the_hash()
    {
        var original = WriteTemp(SqlLf);
        var mutated = WriteTemp(SqlLf.Replace("BIGINT", "INT"));
        try
        {
            Assert.NotEqual(
                CanonicalSqlScript.ComputeNormalizedSha256(original),
                CanonicalSqlScript.ComputeNormalizedSha256(mutated));
        }
        finally { File.Delete(original); File.Delete(mutated); }
    }

    /// <summary>
    /// Whitespace that is not a line ending is content. This is the test that stops a future "just make CI
    /// green" change from reaching for <c>Trim()</c> or a whitespace-collapsing regex: both would pass every
    /// portability test above while blinding the guard to real edits — a dropped space in a SIGNAL message,
    /// an altered indent inside a trigger body, a changed seed string.
    /// </summary>
    [Fact]
    public void Whitespace_that_is_not_a_line_ending_changes_the_hash()
    {
        var baseline = CanonicalSqlScript.ComputeNormalizedSha256OfText(SqlLf);

        Assert.NotEqual(baseline, CanonicalSqlScript.ComputeNormalizedSha256OfText(SqlLf.Replace("id BIGINT", "id  BIGINT")));
        Assert.NotEqual(baseline, CanonicalSqlScript.ComputeNormalizedSha256OfText(SqlLf + "\n"));           // trailing newline
        Assert.NotEqual(baseline, CanonicalSqlScript.ComputeNormalizedSha256OfText("  " + SqlLf));           // leading indent
        Assert.NotEqual(baseline, CanonicalSqlScript.ComputeNormalizedSha256OfText(SqlLf.Replace("NOT NULL\n", "NOT NULL \n"))); // trailing space
    }

    // ── The canonical script, and agreement with CI ────────────────────────────────────────────

    [Fact]
    public void Canonical_script_matches_the_pin_under_the_normalized_rule()
    {
        var path = CanonicalSqlScript.ResolvePath();
        var actual = CanonicalSqlScript.ComputeNormalizedSha256(path);

        Assert.True(
            string.Equals(actual, CanonicalSqlScript.ExpectedSha256, StringComparison.OrdinalIgnoreCase),
            $"Canonical schema hash mismatch under the normalized rule.{Environment.NewLine}" +
            $"  normalized expected: {CanonicalSqlScript.ExpectedSha256}{Environment.NewLine}" +
            $"  normalized actual:   {actual}{Environment.NewLine}" +
            "Line endings and a leading BOM cannot cause this, so the SQL content really changed.");
    }

    /// <summary>
    /// The rule is implemented twice — here in C#, and in JavaScript inside
    /// <c>.github/workflows/merge-validation.yml</c>, which checks the pin before spending minutes on a
    /// fresh import. Two implementations that drift apart are worse than one, because the workflow would
    /// then fail on a schema the suite considers fine (or, far worse, pass on one it does not).
    ///
    /// <para>
    /// This fixture contains every construct the rule speaks about — a leading BOM, CRLF, a lone CR, a
    /// plain LF, and multi-byte Vietnamese text — and its hash is pinned. The workflow hashes the same
    /// fixture and asserts the same constant, so a change to either implementation alone turns something
    /// red. Escapes are used rather than literal characters so the fixture does not depend on the encoding
    /// of this source file, which would defeat the point.
    /// </para>
    /// </summary>
    [Fact]
    public void Cross_language_fixture_matches_the_value_the_workflow_asserts()
    {
        const string fixture =
            "\uFEFF" +
            "CREATE TABLE v\u00ED_d\u1EE5 (\r\n" +
            "  id BIGINT,\r" +
            "  t\u00EAn VARCHAR(255),\n" +
            "  ghi_ch\u00FA TEXT -- Ti\u1EBFng Vi\u1EC7t: \u0111\u1EE7 d\u1EA5u\r\n" +
            ");\n";

        Assert.Equal(
            "b8fa13eff82815dedb6c3f17cccc92f8026c1be5584e84d46151b2dd2e937d26",
            CanonicalSqlScript.ComputeNormalizedSha256OfText(fixture));
    }

    /// <summary>
    /// The normalised text is what <see cref="CanonicalSqlScript.ReadVerified"/> hands to the import, so a
    /// disposable database is byte-identical on every platform. Without this, the two CRLFs that sit inside
    /// this script's string literals — the seeded <c>note_to_fptu</c> values for visit requests 1002 and
    /// 3048/3049 — would be stored as <c>\r\n</c> on Windows and <c>\n</c> on Linux.
    /// </summary>
    [Fact]
    public void ReadVerified_returns_text_with_no_carriage_returns()
    {
        Assert.DoesNotContain('\r', CanonicalSqlScript.ReadVerified());
    }
}
