using System.Collections.Generic;
using PEMS.Application.AgendaTemplates.Common;

namespace PEMS.Application.AgendaTemplates.Commands.ApplyAgendaTemplate;

public sealed record ApplyAgendaTemplateResponse(
    ulong VisitInstanceId,
    ulong AgendaTemplateId,
    int Count,
    string RequestVisitType,
    string TemplateVisitType,
    bool VisitTypeMismatch,
    IReadOnlyList<AgendaRowView> Items,
    string Message);
