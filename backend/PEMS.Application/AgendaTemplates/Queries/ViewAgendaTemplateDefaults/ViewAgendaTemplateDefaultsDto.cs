using System.Collections.Generic;

namespace PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateDefaults;

public sealed record AgendaTemplateDefaultRow(
    ulong AgendaTemplateDefaultId,
    ulong? CampusId,
    string CampusScopeKey,
    string VisitType,
    ulong AgendaTemplateId,
    string TemplateName,
    string TemplateStatus,
    bool TemplateDeleted);

public sealed record ViewAgendaTemplateDefaultsDto(
    IReadOnlyList<AgendaTemplateDefaultRow> Defaults,
    int Total);
