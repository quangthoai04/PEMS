using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateList;

public sealed class ViewAgendaTemplateListQueryHandler : IRequestHandler<ViewAgendaTemplateListQuery, ViewAgendaTemplateListDto>
{
    public Task<ViewAgendaTemplateListDto> Handle(ViewAgendaTemplateListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Agenda Template List has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}