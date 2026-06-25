using System.Collections.Generic;
using MediatR;
using PEMS.Application.AgendaTemplates.Common;

namespace PEMS.Application.AgendaTemplates.Commands.CreateAgendaTemplate;

/// <summary>
/// Create an agenda template (header + timeline items) for a (campus|GLOBAL) scope and visit type.
/// campus_scope_key is derived server-side from <see cref="CampusId"/> (the DB trigger normalises it
/// too); it is never accepted from the client.
/// </summary>
public class CreateAgendaTemplateCommand : IRequest<CreateAgendaTemplateResponse>
{
    /// <summary>Null = GLOBAL template; otherwise the campus the template belongs to.</summary>
    public ulong? CampusId { get; set; }

    public string VisitType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public List<AgendaTemplateItemInput> Items { get; set; } = new();
}
