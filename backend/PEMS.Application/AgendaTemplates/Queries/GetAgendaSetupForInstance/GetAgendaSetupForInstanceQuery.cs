using MediatR;

namespace PEMS.Application.AgendaTemplates.Queries.GetAgendaSetupForInstance;

/// <summary>Everything the agenda-setup screen of a campus instance needs: the request's visit type,
/// the resolved default template, the selectable templates (campus scope + GLOBAL) and the current
/// concrete agenda. visit_type is read from the original form (visit_requests.visit_type) via the
/// instance's visit_request_id; visit_request_campuses is read-only here.</summary>
public class GetAgendaSetupForInstanceQuery : IRequest<GetAgendaSetupForInstanceDto>
{
    public ulong VisitInstanceId { get; set; }

    public GetAgendaSetupForInstanceQuery() { }
    public GetAgendaSetupForInstanceQuery(ulong visitInstanceId) => VisitInstanceId = visitInstanceId;
}
