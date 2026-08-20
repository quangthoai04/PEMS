using FluentValidation;
using PEMS.Domain.Policies;

namespace PEMS.Application.Delegations.Commands.ApproveCampusInstance;

public sealed class ApproveCampusInstanceCommandValidator : AbstractValidator<ApproveCampusInstanceCommand>
{
    public ApproveCampusInstanceCommandValidator()
    {
        RuleFor(x => x.VisitRequestId).GreaterThan(0UL);
        RuleFor(x => x.VisitInstanceId).GreaterThan(0UL);
        RuleFor(x => x.HostUserId)
            .GreaterThan(0UL).WithMessage("Khi duyệt yêu cầu, bạn phải chọn host chính thức.");
        RuleFor(x => x.DecisionNote).MaximumLength(VisitMutationPolicy.DecisionNoteMaxLength);
    }
}
