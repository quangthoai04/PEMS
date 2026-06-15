using System;

namespace PEMS.Application.AgendaTemplates.Commands.DeleteAgendaTemplate;

public sealed class DeleteAgendaTemplateResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}