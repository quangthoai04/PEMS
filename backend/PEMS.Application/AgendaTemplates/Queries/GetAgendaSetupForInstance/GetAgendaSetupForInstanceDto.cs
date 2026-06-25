using System.Collections.Generic;
using PEMS.Application.AgendaTemplates.Common;

namespace PEMS.Application.AgendaTemplates.Queries.GetAgendaSetupForInstance;

/// <summary>A selectable template for the setup dropdown, with its items embedded so the host can
/// preview it without a second (management-gated) detail call.</summary>
public sealed record AgendaSetupTemplateOption(
    ulong AgendaTemplateId,
    ulong? CampusId,
    string CampusScopeKey,
    string VisitType,
    string Name,
    string? Description,
    string Status,
    int ItemCount,
    bool IsDefault,
    IReadOnlyList<AgendaTemplateItemView> Items);

public sealed record GetAgendaSetupForInstanceDto(
    ulong VisitInstanceId,
    ulong VisitRequestId,
    ulong CampusId,
    string VisitType,
    DateTime PlannedStartAt,
    DateTime PlannedEndAt,
    string Relation,
    bool CanApply,
    ulong? DefaultTemplateId,
    string? DefaultScope,
    bool HasExistingAgenda,
    IReadOnlyList<AgendaSetupTemplateOption> SelectableTemplates,
    IReadOnlyList<AgendaRowView> CurrentAgenda);
