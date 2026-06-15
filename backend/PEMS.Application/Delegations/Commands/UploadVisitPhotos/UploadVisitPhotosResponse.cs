using System;

namespace PEMS.Application.Delegations.Commands.UploadVisitPhotos;

public sealed class UploadVisitPhotosResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}