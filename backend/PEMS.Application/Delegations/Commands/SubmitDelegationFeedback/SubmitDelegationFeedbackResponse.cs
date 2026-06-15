using System;

namespace PEMS.Application.Delegations.Commands.SubmitDelegationFeedback;

public sealed class SubmitDelegationFeedbackResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}