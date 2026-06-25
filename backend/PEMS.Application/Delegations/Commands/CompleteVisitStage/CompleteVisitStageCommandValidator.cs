using FluentValidation;

namespace PEMS.Application.Delegations.Commands.CompleteVisitStage;

public sealed class CompleteVisitStageCommandValidator
    : AbstractValidator<CompleteVisitStageCommand>
{
    public CompleteVisitStageCommandValidator()
    {
        RuleFor(x => x.VisitRequestId)
            .GreaterThan(0ul).WithMessage("VisitRequestId không hợp lệ.");

        RuleFor(x => x.VisitInstanceId)
            .GreaterThan(0ul).WithMessage("VisitInstanceId không hợp lệ.");

        RuleFor(x => x.Stage)
            .Must(s => s == VisitStageKeys.Before || s == VisitStageKeys.During || s == VisitStageKeys.After)
            .WithMessage("Giai đoạn không hợp lệ.");
    }
}
