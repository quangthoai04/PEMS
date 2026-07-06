using System.Threading.Tasks;
using Xunit;

namespace PEMS.ApplicationTests.PublicContent;

/// <summary>
/// Tests for UC-05: View FAQ — GET /api/public/faqs
/// These tests verify business rules for ViewFaqQueryHandler.
/// NOTE: Requires a test project (.csproj) referencing PEMS.Application,
/// Microsoft.EntityFrameworkCore.InMemory, and xunit to compile and run.
/// </summary>
public class ViewFAQQueryTests
{
    // VC-01: Only PUBLISHED FAQs are returned; HIDDEN are excluded.
    [Fact(Skip = "Requires test project setup with EF InMemory provider")]
    public async Task Handle_Returns_OnlyPublishedFaqs()
    {
        // Arrange
        // Seed: 3 PUBLISHED + 1 HIDDEN FAQ
        // Act: Send ViewFaqQuery(Keyword: null, FaqType: null, Page: 1, PageSize: 10)
        // Assert: result.Items.Count == 3
        //         result.Items.All(x => x.FaqType != null)  (no Status field exposed)
        await Task.CompletedTask;
    }

    // VC-02: HIDDEN FAQ must not appear even when keyword matches.
    [Fact(Skip = "Requires test project setup with EF InMemory provider")]
    public async Task Handle_DoesNotReturn_HiddenFaq_EvenWhenKeywordMatches()
    {
        // Arrange
        // Seed: 1 FAQ HIDDEN with question = "secret question"
        // Act: Send ViewFaqQuery(Keyword: "secret", ...)
        // Assert: result.Items.Count == 0
        await Task.CompletedTask;
    }

    // VC-03: Keyword search in Answer field.
    [Fact(Skip = "Requires test project setup with EF InMemory provider")]
    public async Task Handle_Returns_Faq_WhenKeywordMatchesAnswer()
    {
        // Arrange
        // Seed: 1 FAQ PUBLISHED with answer = "dùng OTP để xác thực"
        // Act: Send ViewFaqQuery(Keyword: "OTP", ...)
        // Assert: result.Items.Count == 1
        await Task.CompletedTask;
    }

    // VC-04: faqType filter returns only matching type.
    [Fact(Skip = "Requires test project setup with EF InMemory provider")]
    public async Task Handle_FiltersBy_FaqType()
    {
        // Arrange
        // Seed: 1 FAQ ACCOUNT_ACCESS + 1 FAQ VISIT_REQUEST (both PUBLISHED)
        // Act: Send ViewFaqQuery(FaqType: "ACCOUNT_ACCESS", ...)
        // Assert: result.Items.Count == 1
        //         result.Items[0].FaqType == "ACCOUNT_ACCESS"
        await Task.CompletedTask;
    }

    // VC-05: keyword + faqType combined with AND logic.
    [Fact(Skip = "Requires test project setup with EF InMemory provider")]
    public async Task Handle_AppliesAndLogic_For_KeywordAndFaqType()
    {
        // Arrange
        // Seed: FAQ-A (ACCOUNT_ACCESS, answer="OTP") + FAQ-B (VISIT_REQUEST, answer="OTP")
        // Act: Send ViewFaqQuery(Keyword: "OTP", FaqType: "ACCOUNT_ACCESS", ...)
        // Assert: result.Items.Count == 1
        //         result.Items[0].FaqType == "ACCOUNT_ACCESS"
        await Task.CompletedTask;
    }

    // VC-07: Public DTO must NOT contain status/audit/admin fields.
    [Fact]
    public void ViewFaqDto_DoesNotExpose_AuditOrAdminFields()
    {
        var dtoType = typeof(PEMS.Application.PublicContent.Queries.ViewFAQ.ViewFaqDto);
        var props = dtoType.GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain("Status", props);
        Assert.DoesNotContain("LanguageCode", props);
        Assert.DoesNotContain("CreatedBy", props);
        Assert.DoesNotContain("UpdatedBy", props);
        Assert.DoesNotContain("UpdatedAt", props);
        Assert.DoesNotContain("InternalNote", props);

        Assert.Contains("FaqId", props);
        Assert.Contains("FaqType", props);
        Assert.Contains("FaqTypeLabel", props);
        Assert.Contains("Question", props);
        Assert.Contains("Answer", props);
        Assert.Contains("DisplayOrder", props);
        Assert.Contains("CreatedAt", props);
    }

    // VC-06: Invalid faqType enum (old category) must fail validator.
    [Fact]
    public void ViewFaqQueryValidator_Rejects_InvalidFaqType()
    {
        var validator = new PEMS.Application.PublicContent.Queries.ViewFAQ.ViewFaqQueryValidator();

        var invalidTypes = new[] { "VISA", "PROGRAM", "TUITION_FEE", "DORMITORY", "Program", "Visa" };
        foreach (var invalid in invalidTypes)
        {
            var query = new PEMS.Application.PublicContent.Queries.ViewFAQ.ViewFaqQuery(
                Keyword: null, FaqType: invalid, Page: 1, PageSize: 10);
            var result = validator.Validate(query);
            Assert.False(result.IsValid, $"Expected INVALID for faqType='{invalid}'");
        }
    }

    // VC-06: Valid faqType enum (v10) must pass validator.
    [Fact]
    public void ViewFaqQueryValidator_Accepts_ValidFaqTypes()
    {
        var validator = new PEMS.Application.PublicContent.Queries.ViewFAQ.ViewFaqQueryValidator();

        var validTypes = new[]
        {
            "ACCOUNT_ACCESS", "VISIT_REQUEST", "DELEGATION_MANAGEMENT",
            "LOGISTICS_RESOURCE", "DOCUMENT_MEDIA", "NOTIFICATION_EMAIL", "OTHER",
            "ALL", null, ""
        };

        foreach (var valid in validTypes)
        {
            var query = new PEMS.Application.PublicContent.Queries.ViewFAQ.ViewFaqQuery(
                Keyword: null, FaqType: valid, Page: 1, PageSize: 10);
            var result = validator.Validate(query);
            Assert.True(result.IsValid, $"Expected VALID for faqType='{valid ?? "null"}'");
        }
    }

    // VC-10: page < 1 must fail validator.
    [Fact]
    public void ViewFaqQueryValidator_Rejects_InvalidPage()
    {
        var validator = new PEMS.Application.PublicContent.Queries.ViewFAQ.ViewFaqQueryValidator();

        var query = new PEMS.Application.PublicContent.Queries.ViewFAQ.ViewFaqQuery(
            Keyword: null, FaqType: null, Page: 0, PageSize: 10);
        var result = validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Page");
    }

    // VC-10: pageSize > 50 must fail validator.
    [Fact]
    public void ViewFaqQueryValidator_Rejects_PageSizeOver50()
    {
        var validator = new PEMS.Application.PublicContent.Queries.ViewFAQ.ViewFaqQueryValidator();

        var query = new PEMS.Application.PublicContent.Queries.ViewFAQ.ViewFaqQuery(
            Keyword: null, FaqType: null, Page: 1, PageSize: 500);
        var result = validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "PageSize");
    }
}
