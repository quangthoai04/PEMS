using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.AgendaTemplates.Commands.UpdateAgendaTemplate;

public sealed class UpdateAgendaTemplateCommandHandler : IRequestHandler<UpdateAgendaTemplateCommand, UpdateAgendaTemplateResponse>
{
    public Task<UpdateAgendaTemplateResponse> Handle(UpdateAgendaTemplateCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Update Agenda Template has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}