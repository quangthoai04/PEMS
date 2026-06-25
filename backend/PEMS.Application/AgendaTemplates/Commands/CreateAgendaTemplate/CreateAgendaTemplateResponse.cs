namespace PEMS.Application.AgendaTemplates.Commands.CreateAgendaTemplate;

public sealed record CreateAgendaTemplateResponse(
    ulong AgendaTemplateId,
    int ItemCount,
    string Message);
