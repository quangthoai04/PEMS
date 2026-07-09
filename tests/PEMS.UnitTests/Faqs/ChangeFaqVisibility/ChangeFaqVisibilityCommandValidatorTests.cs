using FluentValidation.TestHelper;
using PEMS.Application.Faqs.Commands.ChangeFAQVisibility;
using Xunit;

namespace PEMS.UnitTests.Faqs.ChangeFaqVisibility;

/// <summary>
/// Unit tests for <see cref="ChangeFAQVisibilityCommandValidator"/>.
///
/// Confirmed from ChangeFAQVisibilityCommand.cs: this is a "toggle" API (kiểu B, not explicit
/// target status) — the command only carries <c>FaqId</c>. There is no <c>Status</c> field to
/// validate, so this file intentionally has no Status_* test cases — writing one would test a
/// field that doesn't exist in the real command.
/// </summary>
public class ChangeFaqVisibilityCommandValidatorTests
{
    private readonly ChangeFAQVisibilityCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_NoErrors()
    {
        var command = new ChangeFAQVisibilityCommand { FaqId = 1 };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // FaqId is ulong, so a negative value cannot even be constructed — zero is the only real
    // invalid boundary (RuleFor(x => x.FaqId).GreaterThan(0ul)).
    [Fact]
    public void FaqId_Zero_HasError()
    {
        var command = new ChangeFAQVisibilityCommand { FaqId = 0 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FaqId);
    }
}
