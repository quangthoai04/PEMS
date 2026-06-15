using System;

namespace PEMS.Application.Partners.Commands.ProcessPartnerCreationRequest;

public sealed class ProcessPartnerCreationRequestResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}