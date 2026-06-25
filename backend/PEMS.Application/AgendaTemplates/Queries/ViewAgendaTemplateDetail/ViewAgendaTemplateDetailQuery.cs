using MediatR;

namespace PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateDetail;

public class ViewAgendaTemplateDetailQuery : IRequest<ViewAgendaTemplateDetailDto>
{
    public ulong AgendaTemplateId { get; set; }
}
