using FluentValidation;

namespace PEMS.Application.Partners.Commands.RejectPartner;

public sealed class RejectPartnerCommandValidator : AbstractValidator<RejectPartnerCommand>
{
    public RejectPartnerCommandValidator()
    {
        RuleFor(x => x.PartnerId).GreaterThan(0UL);
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do từ chối là bắt buộc.")
            .MaximumLength(2000);
    }
}
