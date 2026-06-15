using System;

namespace PEMS.Application.Delegations.Commands.UploadAttachedDocuments;

public sealed class UploadAttachedDocumentsResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}