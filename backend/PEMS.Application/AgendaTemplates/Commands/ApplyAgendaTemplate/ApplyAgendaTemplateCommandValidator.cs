using FluentValidation;

namespace PEMS.Application.AgendaTemplates.Commands.ApplyAgendaTemplate;

public sealed class ApplyAgendaTemplateCommandValidator : AbstractValidator<ApplyAgendaTemplateCommand>
{
    public ApplyAgendaTemplateCommandValidator()
    {
        RuleFor(x => x.VisitInstanceId).GreaterThan(0ul);
        RuleFor(x => x.AgendaTemplateId).GreaterThan(0ul);
    }
}
