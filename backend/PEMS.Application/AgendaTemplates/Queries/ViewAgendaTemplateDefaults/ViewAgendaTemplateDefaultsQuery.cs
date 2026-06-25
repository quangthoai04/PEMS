using MediatR;

namespace PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateDefaults;

/// <summary>List the default-template mappings visible to the caller. Staff Leader sees GLOBAL +
/// own campus; HO sees everything. Optional <see cref="CampusId"/> narrows the result.</summary>
public class ViewAgendaTemplateDefaultsQuery : IRequest<ViewAgendaTemplateDefaultsDto>
{
    public ulong? CampusId { get; set; }
}
