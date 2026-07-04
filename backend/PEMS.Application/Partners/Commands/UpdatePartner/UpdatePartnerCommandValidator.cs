using System;
using FluentValidation;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Commands.UpdatePartner;

public sealed class UpdatePartnerCommandValidator : AbstractValidator<UpdatePartnerCommand>
{
    private static readonly string[] CooperationStatuses = { "POTENTIAL", "ACTIVE", "INACTIVE", "BLACKLISTED" };

    public UpdatePartnerCommandValidator()
    {
        RuleFor(x => x.PartnerId).GreaterThan(0UL);
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

        RuleFor(x => x.CooperationStatus)
            .Must(s => string.IsNullOrWhiteSpace(s) || Array.IndexOf(CooperationStatuses, s) >= 0)
            .WithMessage("Trạng thái hợp tác không hợp lệ.");

        RuleFor(x => x.Visibility)
            .Must(v => string.IsNullOrWhiteSpace(v) || Array.IndexOf(PartnerVisibilities.All, v) >= 0)
            .WithMessage("Chế độ hiển thị không hợp lệ.");
    }
}
