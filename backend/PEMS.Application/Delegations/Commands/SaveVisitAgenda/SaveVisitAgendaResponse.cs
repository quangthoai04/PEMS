using System.Collections.Generic;

namespace PEMS.Application.Delegations.Commands.SaveVisitAgenda;

public sealed record SaveVisitAgendaResponse(
    ulong VisitInstanceId,
    int Count,
    IReadOnlyList<SavedAgendaItem> Items,
    string Message);

public sealed record SavedAgendaItem(
    ulong AgendaId,
    string Title,
    DateTime StartTime,
    DateTime? EndTime,
    string? Description,
    string? Location);
