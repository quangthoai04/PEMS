using System;

namespace PEMS.Application.Delegations.Commands.TagFacesonPhotos;

public sealed class TagFacesonPhotosResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}