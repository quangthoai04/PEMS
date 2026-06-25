using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Commands.SaveVisitAgenda;

/// <summary>
/// Upsert the agenda (lịch trình) of a campus instance. The provided list fully replaces the
/// instance's agenda: items with an existing <see cref="SaveVisitAgendaItem.AgendaId"/> are
/// updated, new items are inserted, and existing items not present are removed. Host-only,
/// editable only while the instance is in the preparation window (ASSIGNED/BEFORE_VISIT).
/// </summary>
public sealed record SaveVisitAgendaCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    IReadOnlyList<SaveVisitAgendaItem> Items) : IRequest<SaveVisitAgendaResponse>;

public sealed record SaveVisitAgendaItem(
    ulong? AgendaId,
    string Title,
    DateTime StartTime,
    DateTime? EndTime,
    string? Description,
    string? Location);
