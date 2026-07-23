using FluentValidation;
using PEMS.Application.Emails.Commands.CreateEmailTemplate;

namespace PEMS.Application.Emails.Commands.UpdateEmailTemplate;

public sealed class UpdateEmailTemplateCommandValidator : AbstractValidator<UpdateEmailTemplateCommand>
{
    public UpdateEmailTemplateCommandValidator()
    {
        // Same NOT NULL ENUM as on create — an update must not be able to blank it out.
        RuleFor(x => x.Purpose)
            .NotEmpty().WithMessage("Purpose là bắt buộc.")
            .Must(p => CreateEmailTemplateCommandValidator.AllowedPurposes.Contains(p))
            .WithMessage(
                "Purpose phải là một trong: " +
                $"{string.Join(", ", CreateEmailTemplateCommandValidator.AllowedPurposes)}.");
    }
}