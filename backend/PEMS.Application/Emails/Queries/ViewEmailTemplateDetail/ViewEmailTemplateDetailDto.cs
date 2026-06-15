using System;

namespace PEMS.Application.Emails.Queries.ViewEmailTemplateDetail;

public sealed class ViewEmailTemplateDetailDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}