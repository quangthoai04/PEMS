using System.Collections.Generic;
using System.Linq;
using FluentValidation.TestHelper;
using PEMS.Application.Faqs.Commands.CreateFAQ;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.Faqs.CreateFaq;

/// <summary>
/// Unit tests for <see cref="CreateFAQCommandValidator"/> (UC-63 Create FAQ).
/// Pure validator rules only — no database, no API, no MediatR pipeline.
///
/// FaqType is a dropdown and Status a toggle on the frontend, so the UI never sends
/// null/empty/invalid values for them today. These tests still exist because the validator
/// is the actual contract for the API endpoint, not the current UI: any client (Postman, a
/// future mobile app, a frontend bug) can call POST /api/faqs directly with any payload.
/// </summary>
public class CreateFaqCommandValidatorTests
{
    private readonly CreateFAQCommandValidator _validator = new();

    private static CreateFAQCommand ValidCommand(string? status = FaqConstants.Status.Published) =>
        new(FaqConstants.Type.AccountAccess, "Làm sao để đăng nhập hệ thống?", "Bạn dùng email FPT để đăng nhập.", status);

    public static IEnumerable<object[]> AllValidFaqTypes() =>
        FaqConstants.Type.All.Select(t => new object[] { t });

    [Fact]
    public void AllFieldsValid_StatusPublished_NoErrors()
    {
        var result = _validator.TestValidate(ValidCommand(FaqConstants.Status.Published));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AllFieldsValid_StatusHidden_NoErrors()
    {
        var result = _validator.TestValidate(ValidCommand(FaqConstants.Status.Hidden));
        result.ShouldNotHaveAnyValidationErrors();
    }

    // Null and whitespace-only status both hit the same validator branch
    // (Must(s => string.IsNullOrWhiteSpace(s) || ...)) — the validator treats a whitespace-only
    // status as "missing", exactly like null. Handler then defaults it to PUBLISHED.
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Status_Missing_NoError(string? status)
    {
        var result = _validator.TestValidate(ValidCommand(status));
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData("VISIBLE")] // legacy/incorrect value — not PUBLISHED or HIDDEN
    [InlineData("published")] // right word, wrong case — comparison is exact after trim (case-sensitive)
    public void Status_Invalid_HasError(string status)
    {
        var result = _validator.TestValidate(ValidCommand(status));
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FaqType_Missing_HasError(string? faqType)
    {
        var command = ValidCommand() with { FaqType = faqType! };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FaqType);
    }

    [Theory]
    [InlineData("PROGRAM")] // legacy enum value, removed in the v10 schema
    [InlineData("VISA")] // legacy enum value, removed in the v10 schema
    [InlineData("account_access")] // lower-case of a valid type — comparison is Ordinal (case-sensitive)
    public void FaqType_NotAllowed_HasError(string faqType)
    {
        var command = ValidCommand() with { FaqType = faqType };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FaqType);
    }

    [Theory]
    [MemberData(nameof(AllValidFaqTypes))]
    public void FaqType_AnyAllowedValue_NoError(string faqType)
    {
        var command = ValidCommand() with { FaqType = faqType };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.FaqType);
    }

    // Tests the validator's own Trim() behavior before comparing against the allowed set —
    // not a claim that the dropdown UI can send surrounding whitespace.
    [Fact]
    public void FaqType_SurroundingWhitespace_TrimmedThenValid()
    {
        var command = ValidCommand() with { FaqType = $"  {FaqConstants.Type.Other}  " };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.FaqType);
    }

    [Fact]
    public void Question_Empty_HasError()
    {
        var command = ValidCommand() with { Question = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Question);
    }

    [Fact]
    public void Question_Whitespace_HasError()
    {
        var command = ValidCommand() with { Question = "    " };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Question);
    }

    [Fact]
    public void Question_Null_HasError()
    {
        var command = ValidCommand() with { Question = null! };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Question);
    }

    [Fact]
    public void Question_TooLong_HasError()
    {
        var command = ValidCommand() with { Question = new string('a', 501) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Question);
    }

    [Fact]
    public void Question_MaxLength_NoError()
    {
        var command = ValidCommand() with { Question = new string('a', 500) };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Question);
    }

    [Fact]
    public void Answer_Empty_HasError()
    {
        var command = ValidCommand() with { Answer = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Answer);
    }

    [Fact]
    public void Answer_Whitespace_HasError()
    {
        var command = ValidCommand() with { Answer = "   " };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Answer);
    }

    [Fact]
    public void Answer_Null_HasError()
    {
        var command = ValidCommand() with { Answer = null! };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Answer);
    }
}
