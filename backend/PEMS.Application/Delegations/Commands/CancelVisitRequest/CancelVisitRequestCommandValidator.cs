using FluentValidation;

namespace PEMS.Application.Delegations.Commands.CancelVisitRequest;

public sealed class CancelVisitRequestCommandValidator
    : AbstractValidator<CancelVisitRequestCommand>
{
    public CancelVisitRequestCommandValidator()
    {
        RuleFor(x => x.VisitRequestId)
            .GreaterThan(0ul).WithMessage("VisitRequestId không hợp lệ.");

        RuleFor(x => x.CancellationReason)
            .NotEmpty().WithMessage("Lý do hủy không được để trống.")
            .MaximumLength(2000).WithMessage("Lý do hủy quá dài.");
    }
}
