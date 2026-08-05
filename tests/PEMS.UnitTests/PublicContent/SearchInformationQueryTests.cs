using PEMS.Application.PublicContent.Queries.SearchInformation;
using PEMS.UnitTests.PublicContent.TestInfrastructure;
using static PEMS.UnitTests.PublicContent.TestInfrastructure.PublicSearchTestData;

namespace PEMS.UnitTests.PublicContent;

/// <summary>
/// GET /api/public/search. Covers the three properties the popup depends on and that nothing else
/// enforces: only public content is reachable, a search in one language never shows the other
/// language's text, and relevance ordering puts title matches above body/metadata matches.
/// </summary>
public class SearchInformationQueryTests
{
    private static SearchInformationQueryHandler HandlerFor(PublicSearchTestDbContext db) => new(db);

    private static SearchInformationQuery Query(string keyword, string lang = "vi", int limit = 5) =>
        new() { Keyword = keyword, LanguageCode = lang, Limit = limit };

    // ══ Visibility ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task News_that_is_not_published_is_not_searchable()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.News.Add(News(1, status: "PUBLISHED"));
        db.News.Add(News(2, status: "PENDING_REVIEW"));
        db.NewsTranslations.Add(NewsTranslation(1, 1, "vi", "Hội thảo công nghệ"));
        db.NewsTranslations.Add(NewsTranslation(2, 2, "vi", "Hội thảo nháp"));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("hội thảo"), default);

        Assert.Equal(new ulong[] { 1 }, result.News.Select(n => n.NewsId));
    }

    [Theory]
    [InlineData("PENDING", "PUBLIC")]   // not approved yet
    [InlineData("APPROVED", "PRIVATE")] // approved but not published to the public site
    public async Task Partner_that_is_not_approved_and_public_is_not_searchable(
        string profileStatus, string visibility)
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Campuses.Add(Campus());
        db.Partners.Add(Partner(1, "Acme Corporation", profileStatus: "APPROVED", visibility: "PUBLIC"));
        db.Partners.Add(Partner(2, "Acme Hidden", profileStatus: profileStatus, visibility: visibility));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("acme"), default);

        Assert.Equal(new ulong[] { 1 }, result.Partners.Select(p => p.PartnerId));
    }

    [Fact]
    public async Task Hidden_faq_is_not_searchable()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Faqs.Add(Faq(1, "Làm sao đăng ký tham quan?", "Truy cập trang đăng ký."));
        db.Faqs.Add(Faq(2, "Câu hỏi tham quan bị ẩn", "Nội dung ẩn.", status: "HIDDEN"));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("tham quan"), default);

        Assert.Equal(new ulong[] { 1 }, result.Faqs.Select(f => f.FaqId));
    }

    [Fact]
    public async Task Gallery_item_that_is_draft_or_deleted_is_not_searchable()
    {
        using var db = PublicSearchTestDbContext.Create();
        await SeedGallerySpineAsync(db);
        await AddVisibleItemAsync(db, 1, "Thư viện trung tâm");

        db.GalleryItems.Add(Item(2, "Thư viện nháp", status: "DRAFT"));
        db.GalleryItemMedia.Add(Media(20, 2, 100));
        db.GalleryItems.Add(Item(3, "Thư viện đã xóa", deletedAt: Jan1));
        db.GalleryItemMedia.Add(Media(30, 3, 100));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("thư viện"), default);

        Assert.Equal(new ulong[] { 1 }, result.Galleries.Select(g => g.GalleryItemId));
    }

    [Theory]
    [InlineData("location")]
    [InlineData("area")]
    [InlineData("campus")]
    public async Task Gallery_item_under_an_inactive_ancestor_is_not_searchable(string inactiveLevel)
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Campuses.Add(Campus(status: inactiveLevel == "campus" ? "INACTIVE" : "ACTIVE"));
        db.GalleryAreas.Add(Area(status: inactiveLevel == "area" ? "INACTIVE" : "ACTIVE"));
        db.GalleryLocations.Add(Location(status: inactiveLevel == "location" ? "INACTIVE" : "ACTIVE"));
        db.Files.Add(File(100));
        await db.SaveChangesAsync();
        await AddVisibleItemAsync(db, 1, "Thư viện trung tâm");

        var result = await HandlerFor(db).Handle(Query("thư viện"), default);

        Assert.Empty(result.Galleries);
    }

    [Fact]
    public async Task Gallery_item_without_active_media_is_not_searchable()
    {
        using var db = PublicSearchTestDbContext.Create();
        await SeedGallerySpineAsync(db);

        // No media at all.
        db.GalleryItems.Add(Item(1, "Thư viện không ảnh"));
        // Media rows exist but none is usable.
        db.GalleryItems.Add(Item(2, "Thư viện ảnh tắt"));
        db.GalleryItemMedia.Add(Media(20, 2, 100, status: "INACTIVE"));
        db.GalleryItems.Add(Item(3, "Thư viện ảnh xóa"));
        db.GalleryItemMedia.Add(Media(30, 3, 100, deletedAt: Jan1));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("thư viện"), default);

        Assert.Empty(result.Galleries);
    }

    [Fact]
    public async Task Campuses_are_not_part_of_the_search_contract()
    {
        // Campus was removed from the result shape (§6): a campus-only keyword returns nothing.
        using var db = PublicSearchTestDbContext.Create();
        db.Campuses.Add(Campus(name: "FPTU Đà Nẵng", code: "DN"));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("Đà Nẵng"), default);

        Assert.Equal(0, result.TotalCount);
        Assert.DoesNotContain(
            typeof(SearchInformationDto).GetProperties(),
            p => p.Name.Contains("Campus", StringComparison.OrdinalIgnoreCase));
    }

    // ══ Language ═════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Vietnamese_search_finds_vietnamese_news_and_english_search_does_not()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.News.Add(News(1));
        db.NewsTranslations.Add(NewsTranslation(1, 1, "vi", "Lễ khai giảng năm học"));
        db.NewsTranslations.Add(NewsTranslation(2, 1, "en", "Academic year opening ceremony"));
        await db.SaveChangesAsync();

        var vi = await HandlerFor(db).Handle(Query("khai giảng", "vi"), default);
        var en = await HandlerFor(db).Handle(Query("khai giảng", "en"), default);

        Assert.Equal("Lễ khai giảng năm học", Assert.Single(vi.News).Title);
        Assert.Empty(en.News); // the VI phrase is not English content
    }

    [Fact]
    public async Task English_search_returns_english_text_only()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.News.Add(News(1));
        db.NewsTranslations.Add(NewsTranslation(1, 1, "vi", "Lễ khai giảng năm học"));
        db.NewsTranslations.Add(NewsTranslation(2, 1, "en", "Academic year opening ceremony"));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("ceremony", "en"), default);

        Assert.Equal("Academic year opening ceremony", Assert.Single(result.News).Title);
    }

    [Fact]
    public async Task News_without_an_english_translation_is_absent_from_english_search()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.News.Add(News(1));
        db.News.Add(News(2));
        db.NewsTranslations.Add(NewsTranslation(1, 1, "vi", "Robotics Việt Nam"));
        db.NewsTranslations.Add(NewsTranslation(2, 1, "en", "Robotics Vietnam"));
        db.NewsTranslations.Add(NewsTranslation(3, 2, "vi", "Robotics chỉ tiếng Việt"));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("robotics", "en"), default);

        var hit = Assert.Single(result.News);
        Assert.Equal(1ul, hit.NewsId);
        Assert.Equal("Robotics Vietnam", hit.Title);
    }

    [Fact]
    public async Task Partner_without_an_english_translation_is_absent_from_english_search()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Campuses.Add(Campus());
        db.Partners.Add(Partner(1, "Tập đoàn Robotics", country: "Việt Nam"));
        db.Partners.Add(Partner(2, "Robotics Bilingual", country: "Việt Nam"));
        db.PartnerTranslations.Add(PartnerTranslation(1, 2, "en", "Robotics Bilingual Group", country: "Vietnam"));
        await db.SaveChangesAsync();

        var en = await HandlerFor(db).Handle(Query("robotics", "en"), default);
        var vi = await HandlerFor(db).Handle(Query("robotics", "vi"), default);

        var enHit = Assert.Single(en.Partners);
        Assert.Equal(2ul, enHit.PartnerId);
        Assert.Equal("Robotics Bilingual Group", enHit.Name);
        Assert.Equal("Vietnam", enHit.Country);

        // VI still sees both, using the legacy Vietnamese columns as its fallback.
        Assert.Equal(new ulong[] { 1, 2 }, vi.Partners.Select(p => p.PartnerId).OrderBy(id => id));
        Assert.Contains(vi.Partners, p => p.Name == "Tập đoàn Robotics");
    }

    [Fact]
    public async Task Faq_without_an_english_translation_is_absent_from_english_search()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Faqs.Add(Faq(1, "Quy trình visa cho khách?", "Liên hệ phòng hợp tác."));
        db.Faqs.Add(Faq(2, "Quy trình visa song ngữ?", "Nội dung song ngữ."));
        db.FaqTranslations.Add(FaqTranslation(1, 2, "en", "Visa process for guests?", "Contact the office."));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("visa", "en"), default);

        var hit = Assert.Single(result.Faqs);
        Assert.Equal(2ul, hit.FaqId);
        Assert.Equal("Visa process for guests?", hit.Question);
    }

    [Fact]
    public async Task Gallery_item_whose_translation_is_not_ready_is_absent_from_english_search()
    {
        using var db = PublicSearchTestDbContext.Create();
        await SeedGallerySpineAsync(db);
        await AddVisibleItemAsync(db, 1, "Thư viện", titleEn: "Central Library", translationStatus: "READY");
        await AddVisibleItemAsync(db, 2, "Phòng lab", titleEn: "Library Lab", translationStatus: "PENDING");

        var result = await HandlerFor(db).Handle(Query("library", "en"), default);

        var hit = Assert.Single(result.Galleries);
        Assert.Equal(1ul, hit.GalleryItemId);
        Assert.Equal("Central Library", hit.Title);
    }

    [Fact]
    public async Task English_response_never_carries_vietnamese_fallback_text()
    {
        using var db = PublicSearchTestDbContext.Create();
        await SeedGallerySpineAsync(db);
        db.News.Add(News(1));
        db.NewsTranslations.Add(NewsTranslation(1, 1, "vi", "Trung tâm Robotics"));
        db.NewsTranslations.Add(NewsTranslation(2, 1, "en", "Robotics Centre"));
        db.Partners.Add(Partner(1, "Công ty Robotics"));
        db.PartnerTranslations.Add(PartnerTranslation(1, 1, "en", "Robotics Company"));
        db.Faqs.Add(Faq(1, "Robotics là gì?", "Giải thích."));
        db.FaqTranslations.Add(FaqTranslation(1, 1, "en", "What is Robotics?", "Explanation."));
        await db.SaveChangesAsync();
        await AddVisibleItemAsync(db, 1, "Sân Robotics", titleEn: "Robotics Yard");

        var result = await HandlerFor(db).Handle(Query("robotics", "en"), default);

        var allText = string.Join(" | ",
            result.News.Select(n => n.Title)
                .Concat(result.Partners.Select(p => p.Name))
                .Concat(result.Faqs.Select(f => f.Question))
                .Concat(result.Galleries.Select(g => g.Title)));

        Assert.Equal(4, result.TotalCount);
        foreach (var vietnamese in new[] { "Trung tâm", "Công ty", "là gì", "Sân" })
        {
            Assert.DoesNotContain(vietnamese, allText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Faq_type_label_follows_the_requested_language()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Faqs.Add(Faq(1, "Đăng ký tham quan?", "Nội dung.", faqType: "VISIT_REQUEST"));
        db.FaqTranslations.Add(FaqTranslation(1, 1, "en", "Visit registration?", "Content."));
        await db.SaveChangesAsync();

        var vi = await HandlerFor(db).Handle(Query("tham quan", "vi"), default);
        var en = await HandlerFor(db).Handle(Query("registration", "en"), default);

        Assert.Equal("Đăng ký tham quan", Assert.Single(vi.Faqs).FaqTypeLabel);
        Assert.Equal("Visit Registration", Assert.Single(en.Faqs).FaqTypeLabel);
        Assert.Equal("VISIT_REQUEST", Assert.Single(en.Faqs).FaqType);
    }

    // ══ Ranking ══════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Exact_title_outranks_starts_with_outranks_contains_outranks_body()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.News.Add(News(1));
        db.News.Add(News(2));
        db.News.Add(News(3));
        db.News.Add(News(4));
        db.NewsTranslations.Add(NewsTranslation(4, 4, "vi", "Tin khác", summary: "nhắc tới FPTU ở phần tóm tắt"));
        db.NewsTranslations.Add(NewsTranslation(3, 3, "vi", "Trường FPTU hôm nay"));
        db.NewsTranslations.Add(NewsTranslation(2, 2, "vi", "FPTU mở rộng hợp tác"));
        db.NewsTranslations.Add(NewsTranslation(1, 1, "vi", "FPTU"));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("fptu"), default);

        Assert.Equal(new ulong[] { 1, 2, 3, 4 }, result.News.Select(n => n.NewsId));
    }

    [Fact]
    public async Task Primary_field_match_outranks_metadata_match()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Campuses.Add(Campus());
        // Name match (contains) must beat a country-only match.
        db.Partners.Add(Partner(1, "Đối tác Nhật Bản", country: "Hàn Quốc"));
        db.Partners.Add(Partner(2, "Alpha Group", country: "Nhật Bản"));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("nhật bản"), default);

        Assert.Equal(new ulong[] { 1, 2 }, result.Partners.Select(p => p.PartnerId));
    }

    [Fact]
    public async Task Gallery_title_match_outranks_description_and_location_match()
    {
        using var db = PublicSearchTestDbContext.Create();
        await SeedGallerySpineAsync(db);
        await AddVisibleItemAsync(db, 3, "Phòng học", descriptionVi: "gần thư viện trung tâm", displayOrder: 0);
        await AddVisibleItemAsync(db, 2, "Khu thư viện mở", displayOrder: 1);
        await AddVisibleItemAsync(db, 1, "Thư viện", displayOrder: 2);

        var result = await HandlerFor(db).Handle(Query("thư viện"), default);

        Assert.Equal(new ulong[] { 1, 2, 3 }, result.Galleries.Select(g => g.GalleryItemId));
    }

    // ══ Limit and hasMore ════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(3, false)] // fewer than the limit
    [InlineData(5, false)] // exactly the limit — nothing beyond it
    [InlineData(6, true)]  // one more than the limit
    public async Task HasMore_reports_whether_a_section_was_truncated(int seeded, bool expectedHasMore)
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Campuses.Add(Campus());
        for (var i = 1; i <= seeded; i++)
        {
            db.Partners.Add(Partner((ulong)i, $"Acme {i:D2}"));
        }
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("acme", limit: 5), default);

        Assert.Equal(Math.Min(seeded, 5), result.Partners.Count);
        Assert.Equal(expectedHasMore, result.HasMore.Partners);
    }

    [Fact]
    public async Task Limit_and_hasMore_are_tracked_per_section()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Campuses.Add(Campus());
        for (var i = 1; i <= 7; i++)
        {
            db.Partners.Add(Partner((ulong)i, $"Delta {i:D2}"));
        }
        db.Faqs.Add(Faq(1, "Delta là gì?", "Giải thích."));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("delta", limit: 5), default);

        Assert.True(result.HasMore.Partners);
        Assert.False(result.HasMore.Faqs);
        Assert.False(result.HasMore.News);
        Assert.False(result.HasMore.Galleries);
        // TotalCount is what the popup renders: 5 partners + 1 faq.
        Assert.Equal(6, result.TotalCount);
    }

    // ══ Deep-link payload ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Gallery_result_carries_the_three_fields_the_deep_link_needs()
    {
        using var db = PublicSearchTestDbContext.Create();
        await SeedGallerySpineAsync(db);
        await AddVisibleItemAsync(db, 88, "Thư viện trung tâm", descriptionVi: "Không gian đọc sách");

        var result = await HandlerFor(db).Handle(Query("thư viện"), default);

        var hit = Assert.Single(result.Galleries);
        Assert.Equal(88ul, hit.GalleryItemId);
        Assert.Equal("HN", hit.CampusCode);
        Assert.Equal(1ul, hit.LocationId);
        Assert.Equal("Thư viện", hit.LocationName);
        Assert.Equal("Khu học tập", hit.AreaName);
        Assert.Equal("FPTU Hà Nội", hit.CampusName);
        Assert.Equal("Không gian đọc sách", hit.DescriptionPreview);
        // Public proxy route only — never an internal Drive/file URL.
        Assert.Equal("/api/public/visit-fptu/media/100/content", hit.ThumbnailUrl);
    }

    [Fact]
    public async Task Partner_result_carries_the_slug_used_for_its_detail_route()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Campuses.Add(Campus());
        db.Partners.Add(Partner(7, "Acme Corporation", description: "Nhà cung cấp thiết bị",
            country: "Việt Nam", publicSlug: "acme-corporation"));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(Query("acme"), default);

        var hit = Assert.Single(result.Partners);
        Assert.Equal("acme-corporation", hit.PublicSlug);
        Assert.Equal("Nhà cung cấp thiết bị", hit.DescriptionPreview);
    }

    // ══ Input handling ═══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Blank_keyword_returns_an_empty_result_without_querying(string? keyword)
    {
        using var db = PublicSearchTestDbContext.Create();
        db.News.Add(News(1));
        db.NewsTranslations.Add(NewsTranslation(1, 1, "vi", "Bất kỳ tin nào"));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(
            new SearchInformationQuery { Keyword = keyword, LanguageCode = "vi" }, default);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.News);
        Assert.False(result.HasMore.News);
    }

    [Fact]
    public async Task Keyword_matching_is_case_insensitive_and_treats_wildcards_literally()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Campuses.Add(Campus());
        db.Partners.Add(Partner(1, "ACME Corporation"));
        await db.SaveChangesAsync();

        var cased = await HandlerFor(db).Handle(Query("acme cor"), default);
        // '%' is a LIKE wildcard; as a typed character it must match nothing here.
        var wildcard = await HandlerFor(db).Handle(Query("acme%corporation"), default);

        Assert.Single(cased.Partners);
        Assert.Empty(wildcard.Partners);
    }

    [Theory]
    [InlineData(0, 5)]   // invalid → default
    [InlineData(-3, 5)]  // invalid → default
    [InlineData(50, 20)] // above the ceiling → clamped
    [InlineData(8, 8)]   // in range → kept
    public void Limit_is_clamped_to_its_supported_range(int requested, int expected)
    {
        var query = new SearchInformationQuery { Limit = requested };

        Assert.Equal(expected, query.Limit);
    }

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    [InlineData("EN")]
    [InlineData(null)]
    [InlineData("")]
    public void Validator_accepts_the_supported_languages(string? languageCode)
    {
        var result = new SearchInformationQueryValidator()
            .Validate(new SearchInformationQuery { Keyword = "x", LanguageCode = languageCode });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("ja")]
    [InlineData("en-US")] // callers must normalise before sending (§8.1)
    [InlineData("vi-VN")]
    public void Validator_rejects_unsupported_languages(string languageCode)
    {
        var result = new SearchInformationQueryValidator()
            .Validate(new SearchInformationQuery { Keyword = "x", LanguageCode = languageCode });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchInformationQuery.LanguageCode));
    }

    [Fact]
    public void Validator_rejects_an_over_long_keyword()
    {
        var result = new SearchInformationQueryValidator().Validate(new SearchInformationQuery
        {
            Keyword = new string('a', SearchInformationQueryValidator.KeywordMaxLength + 1),
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchInformationQuery.Keyword));
    }

    [Fact]
    public void Search_result_dtos_expose_no_admin_or_audit_fields()
    {
        var leaky = new[] { "Status", "DeletedAt", "CreatedBy", "UpdatedBy", "TranslationStatus", "FileId" };

        foreach (var dto in new[]
                 {
                     typeof(SearchNewsResultDto), typeof(SearchPartnerResultDto),
                     typeof(SearchFaqResultDto), typeof(SearchGalleryResultDto),
                 })
        {
            var props = dto.GetProperties().Select(p => p.Name).ToArray();
            foreach (var forbidden in leaky)
            {
                Assert.DoesNotContain(forbidden, props);
            }
        }
    }
}
