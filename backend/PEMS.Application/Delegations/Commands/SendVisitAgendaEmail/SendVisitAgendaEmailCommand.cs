using MediatR;

namespace PEMS.Application.Delegations.Commands.SendVisitAgendaEmail;

/// <summary>
/// Emails the campus's operational contact the instance's current agenda (VISIT_AGENDA_PROPOSAL) so
/// both sides can discuss/confirm it before the visit. Host-only, editable-window-only — same guards as
/// <see cref="PEMS.Application.Delegations.Commands.SaveVisitAgenda.SaveVisitAgendaCommand"/>. Reply-To
/// and Cc are the assigned Host, resolved server-side; nothing about the recipient is caller-supplied.
/// </summary>
public sealed record SendVisitAgendaEmailCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId) : IRequest<SendVisitAgendaEmailResponse>;
