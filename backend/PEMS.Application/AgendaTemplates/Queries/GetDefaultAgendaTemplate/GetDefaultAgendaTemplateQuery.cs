using MediatR;

namespace PEMS.Application.AgendaTemplates.Queries.GetDefaultAgendaTemplate;

/// <summary>Resolve the default agenda template (header + items) for a (campus|GLOBAL) scope and
/// visit type. Campus-specific default wins; otherwise GLOBAL fallback.</summary>
public class GetDefaultAgendaTemplateQuery : IRequest<GetDefaultAgendaTemplateDto>
{
    public string VisitType { get; set; } = string.Empty;
    public ulong? CampusId { get; set; }
}
