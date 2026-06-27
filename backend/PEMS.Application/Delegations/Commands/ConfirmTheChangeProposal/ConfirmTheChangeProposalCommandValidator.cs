using FluentValidation;

namespace PEMS.Application.Delegations.Commands.ConfirmTheChangeProposal;

public sealed class ConfirmTheChangeProposalCommandValidator : AbstractValidator<ConfirmTheChangeProposalCommand>
{
    public ConfirmTheChangeProposalCommandValidator()
    {
        RuleFor(x => x.LogisticsItemId).NotEmpty();
        When(x => !x.Accepted, () =>
        {
            RuleFor(x => x.Note).NotEmpty().MaximumLength(1000);
        });
    }
}
