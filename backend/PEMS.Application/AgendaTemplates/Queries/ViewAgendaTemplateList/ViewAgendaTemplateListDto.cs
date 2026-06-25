using System.Collections.Generic;
using PEMS.Application.AgendaTemplates.Common;

namespace PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateList;

public sealed record ViewAgendaTemplateListDto(
    IReadOnlyList<AgendaTemplateSummary> Templates,
    int Total);
