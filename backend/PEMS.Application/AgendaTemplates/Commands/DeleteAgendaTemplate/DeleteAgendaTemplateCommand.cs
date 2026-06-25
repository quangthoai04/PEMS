using MediatR;

namespace PEMS.Application.AgendaTemplates.Commands.DeleteAgendaTemplate;

/// <summary>Soft-delete an agenda template (sets deleted_at/deleted_by). Items are kept; the
/// template is excluded from lists/defaults once deleted.</summary>
public class DeleteAgendaTemplateCommand : IRequest<DeleteAgendaTemplateResponse>
{
    public ulong AgendaTemplateId { get; set; }
}
