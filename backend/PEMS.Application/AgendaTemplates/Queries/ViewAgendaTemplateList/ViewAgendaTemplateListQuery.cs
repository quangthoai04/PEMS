using MediatR;

namespace PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateList;

/// <summary>List agenda templates visible to the caller's scope. Optional filters narrow by
/// campus, visit type or status. Staff Leader sees GLOBAL + own campus; HO sees everything.</summary>
public class ViewAgendaTemplateListQuery : IRequest<ViewAgendaTemplateListDto>
{
    public ulong? CampusId { get; set; }
    public string? VisitType { get; set; }
    public string? Status { get; set; }
    public bool IncludeDeleted { get; set; }
}
