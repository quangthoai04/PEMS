using System;

namespace PEMS.Application.Partners.Commands.EditPartnerInformation;

public sealed class EditPartnerInformationResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}