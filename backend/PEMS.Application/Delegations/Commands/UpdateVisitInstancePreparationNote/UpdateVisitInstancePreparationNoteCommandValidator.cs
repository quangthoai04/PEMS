using FluentValidation;

namespace PEMS.Application.Delegations.Commands.UpdateVisitInstancePreparationNote;

public sealed class UpdateVisitInstancePreparationNoteCommandValidator
    : AbstractValidator<UpdateVisitInstancePreparationNoteCommand>
{
    public const int MaxLength = 5000;

    public UpdateVisitInstancePreparationNoteCommandValidator()
    {
        RuleFor(x => x.VisitInstanceId).GreaterThan(0ul);

        // The note is optional (null/empty clears it) but bounded.
        RuleFor(x => x.Note)
            .MaximumLength(MaxLength)
            .WithMessage($"Ghi chú chung không được vượt quá {MaxLength} ký tự.");
    }
}
