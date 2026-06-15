using System;

namespace PEMS.Application.Partners.Queries.ViewPartnerDetails;

public sealed class ViewPartnerDetailsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}