namespace PEMS.Application.AgendaTemplates.Commands.DeleteAgendaTemplate;

public sealed record DeleteAgendaTemplateResponse(
    ulong AgendaTemplateId,
    string Message);
