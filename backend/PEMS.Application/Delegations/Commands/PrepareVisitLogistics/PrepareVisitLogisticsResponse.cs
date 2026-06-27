namespace PEMS.Application.Delegations.Commands.PrepareVisitLogistics;

public sealed record PrepareVisitLogisticsResponse(
    bool Success,
    bool BusinessCreated,
    ulong LogisticsItemId,
    /// <summary>SENT | FAILED.</summary>
    string EmailStatus,
    ulong SentEmailId,
    string Message);
