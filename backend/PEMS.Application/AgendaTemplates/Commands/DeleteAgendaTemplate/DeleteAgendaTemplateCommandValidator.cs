using FluentValidation;

namespace PEMS.Application.AgendaTemplates.Commands.DeleteAgendaTemplate;

public sealed class DeleteAgendaTemplateCommandValidator : AbstractValidator<DeleteAgendaTemplateCommand>
{
    public DeleteAgendaTemplateCommandValidator()
    {
        RuleFor(x => x.AgendaTemplateId).GreaterThan(0ul);
    }
}
