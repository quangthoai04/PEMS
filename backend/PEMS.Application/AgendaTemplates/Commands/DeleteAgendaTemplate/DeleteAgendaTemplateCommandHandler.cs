using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.AgendaTemplates.Commands.DeleteAgendaTemplate;

public sealed class DeleteAgendaTemplateCommandHandler : IRequestHandler<DeleteAgendaTemplateCommand, DeleteAgendaTemplateResponse>
{
    public Task<DeleteAgendaTemplateResponse> Handle(DeleteAgendaTemplateCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Delete Agenda Template has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}