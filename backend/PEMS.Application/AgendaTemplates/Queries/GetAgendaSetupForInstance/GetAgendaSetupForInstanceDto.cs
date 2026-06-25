using System.Collections.Generic;
using PEMS.Application.AgendaTemplates.Common;

namespace PEMS.Application.AgendaTemplates.Queries.GetAgendaSetupForInstance;

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
    IReadOnlyList<AgendaTemplateSummary> SelectableTemplates,
    IReadOnlyList<AgendaRowView> CurrentAgenda);
