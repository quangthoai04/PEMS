using System.Collections.Generic;
using System.Linq;
using FluentValidation.TestHelper;
using PEMS.Application.Faqs.Queries.ViewListFAQ;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.Faqs.ViewListFaq;

/// <summary>
/// Unit tests for <see cref="ViewListFAQQueryValidator"/> (UC-62 View List FAQ — HO management
/// list, <c>GET /api/faqs</c>). Pure validator rules only — no database, no API, no MediatR
/// pipeline, no HTTP.
/// </summary>
public class ViewListFaqQueryValidatorTests
{
    private readonly ViewListFAQQueryValidator _validator = new();

    private static ViewListFAQQuery ValidQuery() =>
        new(Keyword: null, FaqType: null, Status: null, SortBy: null, SortDirection: null, Page: 1, PageSize: 5);

    public static IEnumerable<object[]> AllValidFaqTypes() =>
        FaqConstants.Type.All.Select(t => new object[] { t });

    [Fact]
    public void ValidQuery_NoErrors()
    {
        var result = _validator.TestValidate(ValidQuery());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Page_NotPositive_HasError(int page)
    {
        var query = ValidQuery() with { Page = page };
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)] // above the InclusiveBetween(1, 50) max
    public void PageSize_OutOfRange_HasError(int pageSize)
    {
        var query = ValidQuery() with { PageSize = pageSize };
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void PageSize_Boundary_NoError(int pageSize)
    {
        var query = ValidQuery() with { PageSize = pageSize };
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void FaqType_Null_NoError()
    {
        var result = _validator.TestValidate(ValidQuery() with { FaqType = null });
        result.ShouldNotHaveValidationErrorFor(x => x.FaqType);
    }

    [Theory]
    [InlineData("ALL")]
    [InlineData("all")] // BeValidFaqType checks the ALL keyword case-insensitively
    public void FaqType_AllKeyword_NoError(string value)
    {
        var result = _validator.TestValidate(ValidQuery() with { FaqType = value });
        result.ShouldNotHaveValidationErrorFor(x => x.FaqType);
    }

    [Theory]
    [MemberData(nameof(AllValidFaqTypes))]
    public void FaqType_AnyAllowedValue_NoError(string faqType)
    {
        var result = _validator.TestValidate(ValidQuery() with { FaqType = faqType });
        result.ShouldNotHaveValidationErrorFor(x => x.FaqType);
    }

    [Theory]
    [InlineData("PROGRAM")] // legacy enum value, removed in the v10 schema
    [InlineData("VISA")] // legacy enum value, removed in the v10 schema
    [InlineData("account_access")] // lower-case of a valid type — comparison is Ordinal (case-sensitive)
    public void FaqType_Invalid_HasError(string faqType)
    {
        var result = _validator.TestValidate(ValidQuery() with { FaqType = faqType });
        result.ShouldHaveValidationErrorFor(x => x.FaqType);
    }

    [Fact]
    public void Status_Null_NoError()
    {
        var result = _validator.TestValidate(ValidQuery() with { Status = null });
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData("ALL")]
    [InlineData("all")]
    public void Status_AllKeyword_NoError(string value)
    {
        var result = _validator.TestValidate(ValidQuery() with { Status = value });
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData(FaqConstants.Status.Published)]
    [InlineData(FaqConstants.Status.Hidden)]
    public void Status_AnyAllowedValue_NoError(string status)
    {
        var result = _validator.TestValidate(ValidQuery() with { Status = status });
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData("VISIBLE")] // legacy/incorrect value
    [InlineData("DRAFT")] // not a value this schema uses
    [InlineData("published")] // right word, wrong case — comparison is exact (case-sensitive), unlike the ALL keyword
    public void Status_Invalid_HasError(string status)
    {
        var result = _validator.TestValidate(ValidQuery() with { Status = status });
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void SortBy_Null_NoError()
    {
        var result = _validator.TestValidate(ValidQuery() with { SortBy = null });
        result.ShouldNotHaveValidationErrorFor(x => x.SortBy);
    }

    [Theory]
    [InlineData("createdAt")]
    [InlineData("displayOrder")]
    public void SortBy_Allowed_NoError(string sortBy)
    {
        var result = _validator.TestValidate(ValidQuery() with { SortBy = sortBy });
        result.ShouldNotHaveValidationErrorFor(x => x.SortBy);
    }

    [Theory]
    [InlineData("answer")]
    [InlineData("faqType")]
    public void SortBy_NotAllowed_HasError(string sortBy)
    {
        var result = _validator.TestValidate(ValidQuery() with { SortBy = sortBy });
        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void SortDirection_Null_NoError()
    {
        var result = _validator.TestValidate(ValidQuery() with { SortDirection = null });
        result.ShouldNotHaveValidationErrorFor(x => x.SortDirection);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("DESC")] // BeValidSortDirection lower-cases before comparing — case-insensitive
    public void SortDirection_Allowed_NoError(string sortDirection)
    {
        var result = _validator.TestValidate(ValidQuery() with { SortDirection = sortDirection });
        result.ShouldNotHaveValidationErrorFor(x => x.SortDirection);
    }

    [Fact]
    public void SortDirection_Invalid_HasError()
    {
        var result = _validator.TestValidate(ValidQuery() with { SortDirection = "ascending" });
        result.ShouldHaveValidationErrorFor(x => x.SortDirection);
    }
}
