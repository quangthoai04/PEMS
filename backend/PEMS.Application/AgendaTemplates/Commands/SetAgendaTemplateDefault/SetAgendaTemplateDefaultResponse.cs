namespace PEMS.Application.AgendaTemplates.Commands.SetAgendaTemplateDefault;

public sealed record SetAgendaTemplateDefaultResponse(
    ulong AgendaTemplateDefaultId,
    ulong? CampusId,
    string CampusScopeKey,
    string VisitType,
    ulong AgendaTemplateId,
    string Message);
