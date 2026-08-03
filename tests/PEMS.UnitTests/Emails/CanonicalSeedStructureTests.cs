using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PEMS.Application.Emails.Common;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// Structural checks on the canonical seed script, without a database.
///
/// <para>
/// These exist because of a defect that reached a commit and survived a full verification round. An
/// edit that appended the <c>email_contact_policies</c> seed pasted it INTO the middle of a string
/// literal in the <c>email_templates</c> INSERT — inside row 70030's <c>body_vi</c> — splitting the
/// 31-row statement in half. MySQL stops the import at <c>ERROR 1064</c>, so the catalog rebuild, the
/// policy seed and the closing ALTERs never run, and a fresh database is left holding the 16-template
/// demo catalog seeded much earlier in the same file.
/// </para>
///
/// <para>
/// Nothing caught it. The integration suite verifies the script by importing it, but it pins the file
/// by SHA-256 and the pin had been updated to the broken content in the same commit; the integration
/// project itself had also stopped compiling. Both are fixed, but both need MySQL and a working build
/// to say anything. These tests need neither — they read the file and look at its shape, so the same
/// mistake fails in the fast suite, on any machine, in under a second.
/// </para>
/// </summary>
public class CanonicalSeedStructureTests
{
    private static string Script() => File.ReadAllText(
        Path.Combine(RepositoryRoot(), "docs", "database", "scripts", "PEMS_FULL_VS_31_07_NEW.sql"));

    /// <summary>The final catalog rebuild: everything from the R0 DELETE to the end of the file.</summary>
    private static string RebuildBlock()
    {
        var sql = Script();
        var at = sql.IndexOf("DELETE FROM email_templates;", StringComparison.Ordinal);
        Assert.True(at >= 0, "The canonical script no longer contains the R0 catalog rebuild.");
        return sql[at..];
    }

    /// <summary>The single <c>INSERT INTO email_templates</c> of the rebuild, up to its terminator.</summary>
    private static string CatalogInsert()
    {
        var block = RebuildBlock();
        var start = block.IndexOf("INSERT INTO email_templates", StringComparison.Ordinal);
        Assert.True(start >= 0, "The catalog rebuild no longer contains an INSERT INTO email_templates.");

        var end = block.IndexOf("NULL);", start, StringComparison.Ordinal);
        Assert.True(end >= 0, "The catalog INSERT has no terminator — the statement is unterminated.");
        return block[start..(end + "NULL);".Length)];
    }

    [Fact]
    public void The_catalog_insert_contains_no_other_statement()
    {
        var insert = CatalogInsert();

        // The exact shape of the defect: a second statement living inside the first. Searched for by
        // keyword rather than by name, so pasting ANY statement in there fails, not just this one.
        foreach (var keyword in new[] { "INSERT INTO", "UPDATE ", "DELETE FROM", "CREATE TABLE", "ALTER TABLE" })
        {
            var occurrences = Regex.Matches(insert, Regex.Escape(keyword)).Count;
            var allowed = keyword == "INSERT INTO" ? 1 : 0;   // the statement's own opening
            Assert.True(
                occurrences == allowed,
                $"'{keyword}' appears {occurrences} time(s) inside the email_templates INSERT " +
                $"(expected {allowed}). A statement pasted inside the row list splits the INSERT and " +
                "the whole import stops there.");
        }
    }

    [Fact]
    public void The_catalog_insert_seeds_exactly_the_registered_templates()
    {
        var insert = CatalogInsert();

        var seeded = Regex.Matches(insert, @"\(\d+, '(?<code>[A-Z0-9_]+)'")
            .Select(m => m.Groups["code"].Value)
            .ToList();

        Assert.Equal(seeded.Count, seeded.Distinct().Count());   // no duplicate template_code
        Assert.Equal(
            SystemEmailTemplates.AllCodes.OrderBy(c => c, StringComparer.Ordinal).ToList(),
            seeded.OrderBy(c => c, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void Every_row_of_the_catalog_insert_is_complete()
    {
        var insert = CatalogInsert();

        // Seventeen columns are declared; every row must supply seventeen values. A row cut short — the
        // truncation half of the same defect — is caught here even if the statement still parses.
        var columns = Regex.Match(insert, @"INSERT INTO email_templates\s*\((?<cols>[^)]*)\)").Groups["cols"].Value
            .Split(',').Length;
        Assert.Equal(17, columns);

        foreach (var body in RowBodies(insert))
        {
            var code = Regex.Match(body, @"'(?<c>[A-Z0-9_]+)'").Groups["c"].Value;

            // Counted outside quoted strings, so a comma inside Vietnamese prose is not a separator.
            var values = 1;
            var inString = false;
            for (var i = 0; i < body.Length; i++)
            {
                if (body[i] == '\'')
                {
                    if (inString && i + 1 < body.Length && body[i + 1] == '\'') { i++; continue; }  // '' escape
                    inString = !inString;
                }
                else if (body[i] == ',' && !inString) values++;
            }

            Assert.True(values == columns, $"Row '{code}' supplies {values} values for {columns} columns.");
        }
    }

    /// <summary>
    /// The contents of each <c>(...)</c> row of a VALUES list.
    ///
    /// <para>
    /// Scanned rather than matched with a regular expression. Row bodies contain both parentheses and
    /// commas inside quoted HTML and Vietnamese prose, and the file's line endings are not uniform, so
    /// a pattern anchored on <c>),\n  (</c> silently swallowed two rows as one — which is how the first
    /// version of this test reported 33 values for a 17-column row.
    /// </para>
    /// </summary>
    private static IEnumerable<string> RowBodies(string insert)
    {
        var at = insert.IndexOf("\nVALUES", StringComparison.Ordinal);
        Assert.True(at >= 0, "The INSERT has no VALUES list.");

        var depth = 0;
        var inString = false;
        var start = -1;

        for (var i = at; i < insert.Length; i++)
        {
            var ch = insert[i];

            if (inString)
            {
                if (ch != '\'') continue;
                if (i + 1 < insert.Length && insert[i + 1] == '\'') { i++; continue; }   // '' escape
                inString = false;
                continue;
            }

            switch (ch)
            {
                case '\'': inString = true; break;
                case '(': if (depth++ == 0) start = i + 1; break;
                case ')':
                    if (--depth == 0) yield return insert[start..i];
                    break;
            }
        }
    }

    [Fact]
    public void The_contact_policy_seed_follows_the_catalog_and_names_only_registered_templates()
    {
        var block = RebuildBlock();

        var catalogAt = block.IndexOf("INSERT INTO email_templates", StringComparison.Ordinal);
        var policyAt = block.IndexOf("INSERT INTO email_contact_policies", StringComparison.Ordinal);
        Assert.True(policyAt > catalogAt,
            "The contact-policy seed must come after the catalog INSERT — before or inside it, the " +
            "templates its rows name do not exist yet.");

        var policySection = block[policyAt..];
        var scoped = Regex.Matches(policySection, @"\('TEMPLATE', '(?<code>[A-Z0-9_]+)'")
            .Select(m => m.Groups["code"].Value)
            .ToList();

        Assert.Equal(scoped.Count, scoped.Distinct().Count());
        Assert.Equal(
            SystemEmailTemplates.AllCodes.OrderBy(c => c, StringComparer.Ordinal).ToList(),
            scoped.OrderBy(c => c, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void Only_one_system_scope_policy_row_is_seeded()
    {
        // scope_key is NULL on this row, and a UNIQUE index does not constrain NULLs in MySQL, so
        // nothing in the schema stops a second one. A duplicate leaves the inheritance chain with two
        // different floors and no rule for choosing between them.
        var rows = Regex.Matches(RebuildBlock(), @"\('SYSTEM', NULL,").Count;
        Assert.True(rows == 1, $"Expected exactly one SYSTEM-scope contact policy row, found {rows}.");
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PEMS.slnx")))
            dir = dir.Parent;

        Assert.True(dir is not null, "Could not locate the repository root (PEMS.slnx) from the test output directory.");
        return dir!.FullName;
    }
}
