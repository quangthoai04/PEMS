using FluentValidation;

namespace PEMS.Application.Delegations.Commands.RejectVisitRequest;

public sealed class RejectVisitRequestCommandValidator : AbstractValidator<RejectVisitRequestCommand>
{
    public RejectVisitRequestCommandValidator()
    {
        RuleFor(x => x.VisitRequestId).GreaterThan(0UL);
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do từ chối.")
            .MaximumLength(2000);
    }
}
