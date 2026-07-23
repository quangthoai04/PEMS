using FluentValidation;
using PEMS.Shared;

namespace PEMS.Application.Emails.Commands.CreateEmailTemplate;

public sealed class CreateEmailTemplateCommandValidator : AbstractValidator<CreateEmailTemplateCommand>
{
    /// <summary>The only two values <c>email_templates.purpose</c> can store.</summary>
    internal static readonly string[] AllowedPurposes =
    {
        OtpPurpose.VisitRequestVerify,
        OtpPurpose.ChangeSensitiveAction,
    };

    public CreateEmailTemplateCommandValidator()
    {
        // purpose is a NOT NULL ENUM. Without these rules a missing or unknown value reached MySQL and came
        // back as a 500 the caller could not act on.
        RuleFor(x => x.Purpose)
            .NotEmpty().WithMessage("Purpose là bắt buộc.")
            .Must(p => AllowedPurposes.Contains(p))
            .WithMessage($"Purpose phải là một trong: {string.Join(", ", AllowedPurposes)}.");
    }
}