using System;
using System.IO;
using System.Linq;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Infrastructure;

/// <summary>
/// The target-database guard, pinned.
///
/// <para>
/// These tests exist because of an actual incident, not a hypothetical one. The canonical script opens
/// with <c>CREATE DATABASE IF NOT EXISTS `pems_db`; USE `pems_db`;</c> and then drops eighty-odd tables,
/// so running it "into a scratch database" without retargeting rebuilds the developer's working database
/// from seed. Everything here asserts that no path in the suite can reach that outcome, and — the part a
/// reviewer should look hardest at — that the guard fails CLOSED on the malformed inputs the previous
/// regular expressions silently passed through.
/// </para>
///
/// <para>
/// None of these touch a server. They are about which database a string names, which is decidable
/// without connecting, and which is exactly where the old rewrite went wrong.
/// </para>
/// </summary>
public sealed class TestDatabaseTargetTests
{
    private const string Disposable = "pems_test_run_0123456789abcdef0123456789abcdef";

    // ── The two spellings the regular expressions could not see ─────────────────────────────────

    /// <summary>
    /// The bug in one line. <c>Regex.Replace(s, "database=[^;]+;", ...)</c> needs a trailing semicolon;
    /// without one it matches nothing, returns the input unchanged, and reports success. A "disposable"
    /// connection string then still named the real database.
    /// </summary>
    [Fact]
    public void A_connection_string_with_no_trailing_semicolon_is_still_retargeted()
    {
        const string original = "server=localhost;port=3306;user=root;password=123456;database=pems_db";

        Assert.Equal("pems_db", TestDatabaseTarget.DatabaseOf(original));

        var retargeted = TestDatabaseTarget.ForDisposable(original, Disposable);

        Assert.Equal(Disposable, TestDatabaseTarget.DatabaseOf(retargeted));
        Assert.False(TestDatabaseTarget.IsProtected(TestDatabaseTarget.DatabaseOf(retargeted)));
    }

    /// <summary>
    /// <c>Initial Catalog</c> is the driver's own synonym for <c>Database</c>. The old pattern did not
    /// know it, so a string spelled this way survived both the retarget AND the server-connection strip.
    /// </summary>
    [Fact]
    public void Initial_catalog_is_recognised_as_the_database()
    {
        const string original = "server=localhost;port=3306;user=root;password=123456;Initial Catalog=pems_db;";

        Assert.Equal("pems_db", TestDatabaseTarget.DatabaseOf(original));
        Assert.Equal(Disposable, TestDatabaseTarget.DatabaseOf(
            TestDatabaseTarget.ForDisposable(original, Disposable)));
        Assert.Equal("", TestDatabaseTarget.DatabaseOf(TestDatabaseTarget.ForServer(original)));
    }

    // ── The server connection carries no schema ─────────────────────────────────────────────────

    /// <summary>
    /// A server connection with a default schema is what lets a statement issued before the script's
    /// first <c>USE</c> land in a real database. Empty is the only value that cannot land anywhere.
    /// </summary>
    [Theory]
    [InlineData("server=localhost;port=3306;database=pems_db;user=root;password=123456;")]
    [InlineData("server=localhost;port=3306;database=pems_db;user=root;password=123456")]
    [InlineData("server=localhost;Initial Catalog=pems_db;user=root;password=123456;")]
    [InlineData("server=localhost;user=root;password=123456;GuidFormat=None;database=pems_db")]
    public void A_server_connection_names_no_database_at_all(string original)
    {
        var server = TestDatabaseTarget.ForServer(original);

        Assert.Equal("", TestDatabaseTarget.DatabaseOf(server));
        TestDatabaseTarget.AssertNotProtected(server, "this assertion");
    }

    /// <summary>GuidFormat is Pomelo's; MySql.Data — which MySqlScript requires — rejects the key.</summary>
    [Fact]
    public void A_server_connection_drops_the_pomelo_only_option()
    {
        var server = TestDatabaseTarget.ForServer(
            "server=localhost;database=pems_db;user=root;password=123456;GuidFormat=None;");

        Assert.DoesNotContain("GuidFormat", server, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// …but the DISPOSABLE connection keeps it, because Pomelo is what opens that one.
    ///
    /// <para>
    /// This is not symmetry for its own sake. The first version of this guard parsed with
    /// <c>MySqlConnectionStringBuilder</c>, which rejects an unknown key outright, so every connection
    /// string carrying <c>GuidFormat</c> threw on the way through — and the suites that use one report a
    /// connection failure as "database is not reachable", which reads like the server is down. Twenty-odd
    /// tests went red for a reason that had nothing to do with a database.
    /// </para>
    /// </summary>
    [Fact]
    public void A_disposable_connection_keeps_every_option_it_was_given()
    {
        const string original =
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;" +
            "AllowUserVariables=True;GuidFormat=None";

        var retargeted = TestDatabaseTarget.ForDisposable(original, Disposable);

        Assert.Equal(Disposable, TestDatabaseTarget.DatabaseOf(retargeted));
        Assert.Contains("GuidFormat=None", retargeted, StringComparison.Ordinal);
        Assert.Contains("AllowUserVariables=True", retargeted, StringComparison.Ordinal);
        Assert.Contains("port=3306", retargeted, StringComparison.Ordinal);
        Assert.Contains("password=123456", retargeted, StringComparison.Ordinal);
    }

    /// <summary>A connection string naming no schema still gets one, rather than silently staying schemaless.</summary>
    [Fact]
    public void A_connection_string_with_no_database_gains_the_disposable_one()
    {
        var retargeted = TestDatabaseTarget.ForDisposable(
            "server=localhost;user=root;password=123456;", Disposable);

        Assert.Equal(Disposable, TestDatabaseTarget.DatabaseOf(retargeted));
    }

    // ── Protected names ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("pems_db")]
    [InlineData("PEMS_DB")]
    [InlineData("  pems_db  ")]
    [InlineData("mysql")]
    [InlineData("sys")]
    [InlineData("information_schema")]
    public void A_protected_database_is_recognised_whatever_its_casing_or_padding(string name)
        => Assert.True(TestDatabaseTarget.IsProtected(name));

    [Theory]
    [InlineData("pems_pr3_test")]
    [InlineData(Disposable)]
    [InlineData("")]
    [InlineData(null)]
    public void An_ordinary_test_database_is_not_protected(string? name)
        => Assert.False(TestDatabaseTarget.IsProtected(name));

    [Fact]
    public void Connecting_to_a_protected_database_is_refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseTarget.AssertNotProtected(
                "server=localhost;database=pems_db;user=root;password=123456;", "this assertion"));

        Assert.Contains("pems_db", ex.Message, StringComparison.Ordinal);
    }

    // ── Only a disposable name may be targeted ──────────────────────────────────────────────────

    [Theory]
    [InlineData("pems_db")]
    [InlineData("pems_g8_fresh")]           // the kind of ad-hoc scratch name a human types
    [InlineData("pems_test_run_short")]
    [InlineData("pems_test_run_0123456789abcdef0123456789abcdeZ")]  // not hex
    public void Retargeting_onto_anything_but_a_disposable_name_is_refused(string target)
        => Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseTarget.ForDisposable(
                "server=localhost;database=pems_pr3_test;user=root;password=123456;", target));

    /// <summary>A string the driver cannot parse must fail, not be half-understood and used.</summary>
    [Fact]
    public void An_unparseable_connection_string_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => TestDatabaseTarget.DatabaseOf(""));
        Assert.Throws<InvalidOperationException>(() => TestDatabaseTarget.DatabaseOf("   "));
    }

    // ── The script-level guard still holds ──────────────────────────────────────────────────────

    /// <summary>
    /// The canonical script really does name <c>pems_db</c> in executable statements — this is not a
    /// theoretical risk, and it is why importing it unretargeted is destructive. If this ever stops
    /// being true the guard is still correct, but the reason for it has changed and someone should know.
    /// </summary>
    [Fact]
    public void The_canonical_script_targets_the_protected_database_when_left_alone()
    {
        var sql = File.ReadAllText(CanonicalSqlScript.ResolvePath());

        Assert.Throws<InvalidOperationException>(
            () => CanonicalSqlScript.AssertSafeToImport(sql, Disposable));
    }

    /// <summary>…and that retargeting it removes every one of those statements.</summary>
    [Fact]
    public void Retargeting_the_canonical_script_leaves_no_statement_naming_a_real_database()
    {
        var retargeted = CanonicalSqlScript.Retarget(CanonicalSqlScript.ReadVerified(), Disposable);

        // Retarget calls this itself; asserting it again here states the guarantee this test is about.
        CanonicalSqlScript.AssertSafeToImport(retargeted, Disposable);

        Assert.DoesNotContain("USE `pems_db`", retargeted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE DATABASE IF NOT EXISTS `pems_db`", retargeted, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Dropping is name-checked independently, since a drop needs no script at all.</summary>
    [Theory]
    [InlineData("pems_db")]
    [InlineData("pems_g8_up")]
    [InlineData("")]
    public void Dropping_a_non_disposable_database_is_refused(string name)
        => Assert.Throws<InvalidOperationException>(() =>
            DisposableDatabaseManager.DropDisposableDatabase(
                "server=localhost;database=pems_pr3_test;user=root;password=123456;", name));

    /// <summary>The suite's own base connection must not be a protected database to begin with.</summary>
    [Fact]
    public void The_suites_base_connection_is_not_a_real_database()
        => TestDatabaseTarget.AssertNotProtected(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True",
            "the suite's base connection");
}
