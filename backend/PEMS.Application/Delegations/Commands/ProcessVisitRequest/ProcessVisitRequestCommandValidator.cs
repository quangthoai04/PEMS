using FluentValidation;

namespace PEMS.Application.Delegations.Commands.ProcessVisitRequest;

public sealed class ProcessVisitRequestCommandValidator : AbstractValidator<ProcessVisitRequestCommand>
{
    public ProcessVisitRequestCommandValidator()
    {
        RuleFor(x => x.VisitRequestId).GreaterThan(0UL);
        RuleFor(x => x.VisitInstanceId).GreaterThan(0UL);
        RuleFor(x => x.HostUserId).GreaterThan(0UL).WithMessage("Vui lòng chọn host phụ trách.");
    }
}
