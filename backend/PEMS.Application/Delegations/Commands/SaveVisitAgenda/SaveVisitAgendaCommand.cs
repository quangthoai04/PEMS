using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Commands.SaveVisitAgenda;

/// <summary>
/// Upsert the agenda (lịch trình) of a campus instance. The provided list fully replaces the
/// instance's agenda: items with an existing <see cref="SaveVisitAgendaItem.AgendaId"/> are
/// updated, new items are inserted, and existing items not present are removed. Host-only,
/// editable only while the instance is in the preparation window (ASSIGNED/BEFORE_VISIT).
///
/// <para>
/// <paramref name="PlannedStartAt"/>/<paramref name="PlannedEndAt"/> sync
/// <c>visit_request_campuses.planned_start_at/end_at</c> in the same transaction — the Host
/// renegotiates the actual visit date/time with the delegation while drafting the agenda, and
/// both changes are meant to take effect together on "Lưu lịch trình".
/// </para>
/// </summary>
public sealed record SaveVisitAgendaCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    IReadOnlyList<SaveVisitAgendaItem> Items,
    DateTime PlannedStartAt,
    DateTime PlannedEndAt) : IRequest<SaveVisitAgendaResponse>;

public sealed record SaveVisitAgendaItem(
    ulong? AgendaId,
    string Title,
    DateTime StartTime,
    DateTime? EndTime,
    string? Description,
    string? Location,
    // Free-typed name of the person responsible for this item. Optional (null = unassigned). Plain
    // text — not validated against any user list.
    string? ResponsibleName = null);
