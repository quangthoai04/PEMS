using FluentValidation;

namespace PEMS.Application.Partners.Aliases.Commands.CreatePartnerAlias;

public sealed class CreatePartnerAliasCommandValidator : AbstractValidator<CreatePartnerAliasCommand>
{
    public CreatePartnerAliasCommandValidator()
    {
        RuleFor(x => x.PartnerId).GreaterThan(0UL);
        RuleFor(x => x.AliasName)
            .NotEmpty().WithMessage("Tên gọi khác không được để trống.")
            .MaximumLength(255);
    }
}
