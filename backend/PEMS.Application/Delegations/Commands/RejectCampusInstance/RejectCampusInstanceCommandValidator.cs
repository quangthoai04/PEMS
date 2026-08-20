using FluentValidation;
using PEMS.Domain.Policies;

namespace PEMS.Application.Delegations.Commands.RejectCampusInstance;

public sealed class RejectCampusInstanceCommandValidator : AbstractValidator<RejectCampusInstanceCommand>
{
    public RejectCampusInstanceCommandValidator()
    {
        RuleFor(x => x.VisitRequestId).GreaterThan(0UL);
        RuleFor(x => x.VisitInstanceId).GreaterThan(0UL);
        RuleFor(x => x.DecisionNote)
            .NotEmpty().WithMessage("Vui lòng nhập lý do từ chối.")
            .MaximumLength(VisitMutationPolicy.DecisionNoteMaxLength)
            .Must(note => string.IsNullOrEmpty(note) || (!note.Contains('<') && !note.Contains('>')))
            .WithMessage("Lý do từ chối không được chứa HTML/script.");
    }
}
