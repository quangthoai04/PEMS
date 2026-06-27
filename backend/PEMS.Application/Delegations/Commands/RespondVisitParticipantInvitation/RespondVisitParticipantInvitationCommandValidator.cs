using FluentValidation;

namespace PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;

public sealed class RespondVisitParticipantInvitationCommandValidator
    : AbstractValidator<RespondVisitParticipantInvitationCommand>
{
    public RespondVisitParticipantInvitationCommandValidator()
    {
        RuleFor(x => x.ParticipantId).GreaterThan(0UL);

        // A decline must carry a reason; an accept must not be blocked by a missing one.
        // Validate the TRIMMED reason so whitespace-only input fails and the 5–1000 bounds are real.
        When(x => !x.Accept, () =>
        {
            RuleFor(x => x.DeclineReason)
                .Cascade(CascadeMode.Stop)
                .Must(r => !string.IsNullOrWhiteSpace(r))
                    .WithMessage("Vui lòng nhập lý do từ chối.")
                .Must(r => r!.Trim().Length >= 5)
                    .WithMessage("Lý do từ chối phải có ít nhất 5 ký tự.")
                .Must(r => r!.Trim().Length <= 1000)
                    .WithMessage("Lý do từ chối không được vượt quá 1000 ký tự.");
        });
    }
}
