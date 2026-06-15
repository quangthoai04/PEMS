using System;

namespace PEMS.Application.Authentication.Commands.ForgotPassword;

public sealed class ForgotPasswordResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}