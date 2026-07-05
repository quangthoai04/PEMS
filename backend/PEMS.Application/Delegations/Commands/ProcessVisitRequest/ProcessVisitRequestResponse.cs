namespace PEMS.Application.Delegations.Commands.ProcessVisitRequest;

public sealed record ProcessVisitRequestResponse(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string RequestStatus,
    string CampusStatus,
    ulong HostUserId,
    string Message,
    // Email mời host (best-effort — lỗi SMTP không làm hỏng việc gán host).
    bool EmailQueued = false,
    string? EmailStatus = null);
