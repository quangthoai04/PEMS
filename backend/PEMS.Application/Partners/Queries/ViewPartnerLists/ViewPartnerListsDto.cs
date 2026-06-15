using System;

namespace PEMS.Application.Partners.Queries.ViewPartnerLists;

public sealed class ViewPartnerListsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}