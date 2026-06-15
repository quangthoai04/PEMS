using System;

namespace PEMS.Application.Feedbacks.Queries.ViewFeedbackSummary;

public sealed class ViewFeedbackSummaryDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}