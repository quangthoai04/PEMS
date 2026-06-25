namespace PEMS.Application.Delegations.Commands.CompleteVisitStage;

public sealed record CompleteVisitStageResponse(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string InstanceStatus,
    string Message);
