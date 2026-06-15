using System;

namespace PEMS.Application.PublicContent.Queries.ViewPolicyAndTerms;

public sealed class ViewPolicyAndTermsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}