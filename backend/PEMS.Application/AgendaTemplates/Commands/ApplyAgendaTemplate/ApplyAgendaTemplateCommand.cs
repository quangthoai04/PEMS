using MediatR;

namespace PEMS.Application.AgendaTemplates.Commands.ApplyAgendaTemplate;

/// <summary>
/// Materialise an agenda template into the concrete <c>visit_agendas</c> of a campus instance.
/// Each item's StartOffsetMinutes/DurationMinutes is a relative baseline, proportionally scaled onto
/// [planned_start_at, planned_end_at] — see <see cref="PEMS.Application.AgendaTemplates.Common.AgendaTemplateTimelineScaler"/>.
/// visit_request_campuses is never written — the source template is traced only via
/// visit_agendas.source_template_item_id.
/// </summary>
public class ApplyAgendaTemplateCommand : IRequest<ApplyAgendaTemplateResponse>
{
    public ulong VisitInstanceId { get; set; }
    public ulong AgendaTemplateId { get; set; }

    /// <summary>When true, the instance's existing agenda is replaced. When false and an agenda
    /// already exists, the command returns 409 Conflict.</summary>
    public bool ReplaceExisting { get; set; }
}
