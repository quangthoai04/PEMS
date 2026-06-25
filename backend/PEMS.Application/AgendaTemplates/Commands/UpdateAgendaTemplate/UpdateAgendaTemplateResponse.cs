namespace PEMS.Application.AgendaTemplates.Commands.UpdateAgendaTemplate;

public sealed record UpdateAgendaTemplateResponse(
    ulong AgendaTemplateId,
    int ItemCount,
    string Message);
