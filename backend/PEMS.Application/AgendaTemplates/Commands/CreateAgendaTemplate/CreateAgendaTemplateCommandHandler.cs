using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.AgendaTemplates.Commands.CreateAgendaTemplate;

public sealed class CreateAgendaTemplateCommandHandler : IRequestHandler<CreateAgendaTemplateCommand, CreateAgendaTemplateResponse>
{
    public Task<CreateAgendaTemplateResponse> Handle(CreateAgendaTemplateCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Create Agenda Template has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}