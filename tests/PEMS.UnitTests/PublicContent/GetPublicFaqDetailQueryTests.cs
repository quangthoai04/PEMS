using PEMS.Application.Common.Exceptions;
using PEMS.Application.PublicContent.Queries.GetPublicFaqDetail;
using PEMS.UnitTests.PublicContent.TestInfrastructure;
using static PEMS.UnitTests.PublicContent.TestInfrastructure.PublicSearchTestData;

namespace PEMS.UnitTests.PublicContent;

/// <summary>
/// GET /api/public/faqs/{faqId} — the endpoint behind the /faq?faqId= deep link. Its job is to be
/// exactly as strict as the search that produced the link: anything the search would not have shown
/// must 404 here rather than open an accordion on non-public content.
/// </summary>
public class GetPublicFaqDetailQueryTests
{
    private static GetPublicFaqDetailQueryHandler HandlerFor(PublicSearchTestDbContext db) => new(db);

    [Fact]
    public async Task Returns_the_published_faq_in_vietnamese()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Faqs.Add(Faq(1, "Đăng ký tham quan thế nào?", "Truy cập trang đăng ký.", faqType: "VISIT_REQUEST"));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(new GetPublicFaqDetailQuery(1, "vi"), default);

        Assert.Equal(1ul, result.FaqId);
        Assert.Equal("Đăng ký tham quan thế nào?", result.Question);
        Assert.Equal("Truy cập trang đăng ký.", result.Answer);
        Assert.Equal("VISIT_REQUEST", result.FaqType);
        Assert.Equal("Đăng ký tham quan", result.FaqTypeLabel);
    }

    [Fact]
    public async Task Returns_english_content_when_the_translation_exists()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Faqs.Add(Faq(1, "Đăng ký tham quan thế nào?", "Truy cập trang đăng ký."));
        db.FaqTranslations.Add(FaqTranslation(1, 1, "en", "How do I register a visit?", "Open the form."));
        await db.SaveChangesAsync();

        var result = await HandlerFor(db).Handle(new GetPublicFaqDetailQuery(1, "en"), default);

        Assert.Equal("How do I register a visit?", result.Question);
        Assert.Equal("Open the form.", result.Answer);
        Assert.Equal("Visit Registration", result.FaqTypeLabel);
    }

    [Fact]
    public async Task Hidden_faq_is_not_found()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Faqs.Add(Faq(1, "Câu hỏi ẩn", "Nội dung ẩn.", status: "HIDDEN"));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(
            () => HandlerFor(db).Handle(new GetPublicFaqDetailQuery(1, "vi"), default));
    }

    [Fact]
    public async Task Missing_faq_is_not_found()
    {
        using var db = PublicSearchTestDbContext.Create();

        await Assert.ThrowsAsync<NotFoundException>(
            () => HandlerFor(db).Handle(new GetPublicFaqDetailQuery(404, "vi"), default));
    }

    [Fact]
    public async Task English_request_without_an_english_translation_is_not_found_rather_than_vietnamese()
    {
        using var db = PublicSearchTestDbContext.Create();
        db.Faqs.Add(Faq(1, "Chỉ có tiếng Việt", "Nội dung tiếng Việt."));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(
            () => HandlerFor(db).Handle(new GetPublicFaqDetailQuery(1, "en"), default));

        // Same row is perfectly reachable in Vietnamese.
        var vi = await HandlerFor(db).Handle(new GetPublicFaqDetailQuery(1, "vi"), default);
        Assert.Equal("Chỉ có tiếng Việt", vi.Question);
    }
}
