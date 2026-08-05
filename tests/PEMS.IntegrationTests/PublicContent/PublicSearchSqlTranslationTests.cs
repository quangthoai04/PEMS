using Microsoft.EntityFrameworkCore;
using PEMS.Application.PublicContent.Queries.SearchInformation;
using PEMS.Infrastructure.Persistence;

namespace PEMS.IntegrationTests.PublicContent;

/// <summary>
/// Proves the four public-search section queries translate to MySQL <b>SQL</b> — that visibility,
/// language, keyword match, ranking and the row limit run in the database, not in memory after loading
/// the table (§12/§13: "không tải toàn bộ ... về memory rồi mới .Contains()").
///
/// The InMemory unit tests in PEMS.UnitTests prove the semantics but cannot prove this: their provider
/// happily evaluates anything client-side, so a query that would be untranslatable against MySQL still
/// passes there. Conversely EF Core 3+ throws rather than silently falling back to client evaluation,
/// so an untranslatable query is a runtime 500 in production — worth catching at build time.
///
/// No database is contacted: <see cref="RelationalQueryableExtensions.ToQueryString"/> generates SQL
/// from the model and the provider, and the server version is stated explicitly so nothing probes a
/// connection. That is why this test needs neither Docker nor the shared pems_test schema.
/// </summary>
public class PublicSearchSqlTranslationTests
{
    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(
                "server=localhost;database=pems_translation_probe;user=probe;password=probe",
                new MySqlServerVersion(new Version(8, 0, 36)))
            .Options);

    public static TheoryData<string, bool> Languages => new() { { "vi", false }, { "en", true } };

    [Theory]
    [MemberData(nameof(Languages))]
    public void News_query_translates_to_sql(string lang, bool _)
    {
        using var db = CreateContext();

        var sql = SearchInformationQueryHandler.BuildNewsQuery(db, "fptu", lang, 6).ToQueryString();

        AssertPushedDown(sql, "news_translations");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Partner_query_translates_to_sql(string lang, bool isEnglish)
    {
        using var db = CreateContext();

        var sql = SearchInformationQueryHandler.BuildPartnerQuery(db, "acme", lang, isEnglish, 6).ToQueryString();

        AssertPushedDown(sql, "partner_translations");
        Assert.Contains("APPROVED", sql, StringComparison.Ordinal);
        Assert.Contains("PUBLIC", sql, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Faq_query_translates_to_sql(string lang, bool isEnglish)
    {
        using var db = CreateContext();

        var sql = SearchInformationQueryHandler.BuildFaqQuery(db, "visa", lang, isEnglish, 6).ToQueryString();

        AssertPushedDown(sql, "faq_translations");
        Assert.Contains("PUBLISHED", sql, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Gallery_query_translates_to_sql(string lang, bool isEnglish)
    {
        _ = lang;
        using var db = CreateContext();

        var sql = SearchInformationQueryHandler.BuildGalleryQuery(db, "thư viện", isEnglish, 6).ToQueryString();

        AssertPushedDown(sql, "gallery_items");
        // The whole visibility chain must be part of the SQL, not applied afterwards.
        foreach (var table in new[] { "gallery_locations", "gallery_areas", "campuses", "gallery_item_media" })
        {
            Assert.Contains(table, sql, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Every section must show the same four things in its SQL: it reads the expected table, it filters
    /// with LIKE, it orders by the relevance CASE, and it caps rows server-side with LIMIT.
    /// </summary>
    private static void AssertPushedDown(string sql, string expectedTable)
    {
        Assert.Contains(expectedTable, sql, StringComparison.Ordinal);
        Assert.Contains("LIKE", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
        Assert.Contains("CASE", sql, StringComparison.Ordinal);  // the relevance score
        Assert.Contains("LIMIT", sql, StringComparison.Ordinal); // Take(limit + 1)
    }
}
