using System;
using FluentValidation;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Commands.CreatePartner;

public sealed class CreatePartnerCommandValidator : AbstractValidator<CreatePartnerCommand>
{
    public CreatePartnerCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên đối tác là bắt buộc.")
            .MaximumLength(200);

        RuleFor(x => x.PartnerCode).MaximumLength(50);
        RuleFor(x => x.ShortName).MaximumLength(100);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.Address).MaximumLength(500);

        RuleFor(x => x.WebsiteUrl)
            .Must(url => string.IsNullOrWhiteSpace(url)
                         || Uri.TryCreate(url.Contains("://") ? url : "https://" + url, UriKind.Absolute, out _))
            .WithMessage("Website không hợp lệ.")
            .MaximumLength(500);

        RuleFor(x => x.PartnerType)
            .Must(t => string.IsNullOrWhiteSpace(t) || Array.IndexOf(PartnerTypes.All, t) >= 0)
            .WithMessage("Loại đối tác không hợp lệ.");

        RuleFor(x => x.Visibility)
            .Must(v => string.IsNullOrWhiteSpace(v) || Array.IndexOf(PartnerVisibilities.All, v) >= 0)
            .WithMessage("Chế độ hiển thị không hợp lệ.");

        When(x => x.InitialContact is not null, () =>
        {
            RuleFor(x => x.InitialContact!.FullName)
                .NotEmpty().WithMessage("Họ tên người liên hệ là bắt buộc.")
                .MaximumLength(150);
            RuleFor(x => x.InitialContact!.Email)
                .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.InitialContact!.Email))
                .WithMessage("Email người liên hệ không hợp lệ.");
        });
    }
}
