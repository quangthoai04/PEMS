using MediatR;

namespace PEMS.Application.AgendaTemplates.Commands.SetAgendaTemplateDefault;

/// <summary>Upsert the default template for a (campus|GLOBAL) scope + visit type. The target template
/// must be ACTIVE, not deleted, the same visit type, and the same scope as the default.</summary>
public class SetAgendaTemplateDefaultCommand : IRequest<SetAgendaTemplateDefaultResponse>
{
    public ulong? CampusId { get; set; }
    public string VisitType { get; set; } = string.Empty;
    public ulong AgendaTemplateId { get; set; }
}
