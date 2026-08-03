using FluentValidation.TestHelper;
using PEMS.Application.Faqs.Commands.UpdateFAQ;
using Xunit;

namespace PEMS.UnitTests.Faqs.UpdateFaq;

/// <summary>
/// Unit tests for <see cref="UpdateFAQCommandValidator"/>.
/// Pure validator rules only — no database, no API, no MediatR pipeline.
///
/// Unlike CreateFAQCommand, UpdateFAQCommand has no Status field: Update FAQ never changes
/// PUBLISHED/HIDDEN visibility (that's ChangeFAQVisibility's job), confirmed against
/// UpdateFAQCommand.cs — so there is nothing to test for status here.
/// </summary>
public class UpdateFaqCommandValidatorTests
{
    private readonly UpdateFAQCommandValidator _validator = new();

    private static UpdateFAQCommand ValidCommand(ulong faqId = 1) =>
        new(faqId, "Làm sao để đăng nhập hệ thống?", "Bạn dùng email FPT để đăng nhập.");

    [Fact]
    public void ValidCommand_NoErrors()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // UpdateFAQCommand.FaqId is ulong, so a negative id cannot even be constructed —
    // zero is the only real invalid boundary (RuleFor(x => x.FaqId).GreaterThan(0)).
    [Fact]
    public void FaqId_Zero_HasError()
    {
        var command = ValidCommand() with { FaqId = 0 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FaqId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Question_Missing_HasError(string? question)
    {
        var command = ValidCommand() with { Question = question! };
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Answer_Missing_HasError(string? answer)
    {
        var command = ValidCommand() with { Answer = answer! };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Answer);
    }

    // UpdateFAQCommand carries no FaqType: editing an FAQ never re-files it under a different type
    // (that is a separate operation), so the four FaqType cases that used to live here tested a field
    // the command no longer has.
}
