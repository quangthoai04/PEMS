using System.Collections.Generic;
using MediatR;
using PEMS.Application.AgendaTemplates.Common;

namespace PEMS.Application.AgendaTemplates.Commands.UpdateAgendaTemplate;

/// <summary>
/// Full update of an agenda template. The provided <see cref="Items"/> list fully replaces the
/// template's items. campus_scope_key is re-derived from <see cref="CampusId"/>.
/// </summary>
public class UpdateAgendaTemplateCommand : IRequest<UpdateAgendaTemplateResponse>
{
    public ulong AgendaTemplateId { get; set; }
    public ulong? CampusId { get; set; }
    public string VisitType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public List<AgendaTemplateItemInput> Items { get; set; } = new();
}
