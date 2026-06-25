using System.Collections.Generic;
using PEMS.Application.AgendaTemplates.Common;

namespace PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateDetail;

public sealed record ViewAgendaTemplateDetailDto(
    ulong AgendaTemplateId,
    ulong? CampusId,
    string CampusScopeKey,
    string VisitType,
    string Name,
    string? Description,
    string Status,
    bool IsDeleted,
    bool IsDefault,
    IReadOnlyList<AgendaTemplateItemView> Items);
