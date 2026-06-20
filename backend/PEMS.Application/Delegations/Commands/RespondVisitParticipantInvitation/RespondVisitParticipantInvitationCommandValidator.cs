using FluentValidation;

namespace PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;

public sealed class RespondVisitParticipantInvitationCommandValidator
    : AbstractValidator<RespondVisitParticipantInvitationCommand>
{
    public RespondVisitParticipantInvitationCommandValidator()
    {
        RuleFor(x => x.ParticipantId).GreaterThan(0UL);

        // A decline must carry a reason; an accept must not be blocked by a missing one.
        RuleFor(x => x.DeclineReason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do từ chối lời mời.")
            .MaximumLength(2000)
            .When(x => !x.Accept);
    }
}
