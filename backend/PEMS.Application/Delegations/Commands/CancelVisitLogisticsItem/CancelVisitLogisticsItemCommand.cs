using MediatR;

namespace PEMS.Application.Delegations.Commands.CancelVisitLogisticsItem;

/// <summary>
/// Host cancels one of their logistics requests (e.g. switching Welcome LED back to "Không cần màn
/// LED"). Soft-cancel only — sets status CANCELLED (no hard delete, so the audit trail / any sent
/// request stays). Host-only, prep window, and only while the department has not yet taken the item
/// (ASSIGNED/ACCEPTED/IN_PROGRESS are locked).
/// </summary>
public sealed record CancelVisitLogisticsItemCommand(
    ulong VisitInstanceId,
    ulong LogisticsItemId) : IRequest<CancelVisitLogisticsItemResponse>;

public sealed record CancelVisitLogisticsItemResponse(
    bool Success,
    ulong LogisticsItemId,
    string Status,
    string Message);
