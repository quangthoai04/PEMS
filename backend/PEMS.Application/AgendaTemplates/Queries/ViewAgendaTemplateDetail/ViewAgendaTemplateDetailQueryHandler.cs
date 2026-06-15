using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateDetail;

public sealed class ViewAgendaTemplateDetailQueryHandler : IRequestHandler<ViewAgendaTemplateDetailQuery, ViewAgendaTemplateDetailDto>
{
    public Task<ViewAgendaTemplateDetailDto> Handle(ViewAgendaTemplateDetailQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Agenda Template Detail has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}