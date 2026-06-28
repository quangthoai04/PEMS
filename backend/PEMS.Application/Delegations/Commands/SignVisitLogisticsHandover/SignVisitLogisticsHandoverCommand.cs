using MediatR;

namespace PEMS.Application.Delegations.Commands.SignVisitLogisticsHandover;

/// <summary>
/// VisitProcess "Đang/Sau tiếp khách": the instance Host signs the BORROWER side of a logistics
/// borrow/return handover (visit_logistics_item_handovers). The Department signs the PROVIDER side
/// from its own portal — both roles are recorded on the same handover row (unique per item+type).
/// No schema change: borrower_signed_by / provider_signed_by already model the two sides.
/// </summary>
public sealed record SignVisitLogisticsHandoverCommand(
    ulong VisitInstanceId,
    ulong LogisticsItemId,
    string HandoverType,        // BORROW | RETURN
    string? ItemCondition,      // GOOD | DAMAGED | MISSING | OTHER (optional; defaults GOOD on create)
    string? Note) : IRequest<SignVisitLogisticsHandoverResponse>;

public sealed class SignVisitLogisticsHandoverResponse
{
    public ulong LogisticsItemId { get; set; }
    public ulong HandoverId { get; set; }
    public string HandoverType { get; set; } = default!;
    public string Status { get; set; } = default!;      // logistics item status after signing
    public string SignedByName { get; set; } = default!;
    public string SignedAt { get; set; } = default!;
    public string Message { get; set; } = default!;
}
