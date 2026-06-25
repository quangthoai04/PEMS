using FluentValidation;
using PEMS.Domain.Constants;

namespace PEMS.Application.AgendaTemplates.Commands.SetAgendaTemplateDefault;

public sealed class SetAgendaTemplateDefaultCommandValidator : AbstractValidator<SetAgendaTemplateDefaultCommand>
{
    public SetAgendaTemplateDefaultCommandValidator()
    {
        RuleFor(x => x.VisitType)
            .Must(VisitTypes.IsValid).WithMessage("Loại hình visit không hợp lệ.");
        RuleFor(x => x.AgendaTemplateId).GreaterThan(0ul);
    }
}
