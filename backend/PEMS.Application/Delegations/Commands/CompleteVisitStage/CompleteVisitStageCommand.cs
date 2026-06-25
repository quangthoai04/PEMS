using MediatR;

namespace PEMS.Application.Delegations.Commands.CompleteVisitStage;

/// <summary>
/// Advances a campus instance through the operational reception stages (Trước → Đang → Sau →
/// Đóng đoàn). One use case per <see cref="Stage"/>:
/// <list type="bullet">
///   <item><c>BEFORE</c> — finish preparation: ASSIGNED/BEFORE_VISIT → DURING_VISIT.</item>
///   <item><c>DURING</c> — finish the visit:   DURING_VISIT        → AFTER_VISIT.</item>
///   <item><c>AFTER</c>  — close the delegation: AFTER_VISIT        → CLOSED.</item>
/// </list>
/// Only the official current host of the instance may advance it; every transition is
/// re-validated server-side (invalid status → 409, wrong actor → 403).
/// </summary>
public sealed record CompleteVisitStageCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string Stage) : IRequest<CompleteVisitStageResponse>;

/// <summary>The stage the caller is completing (the FROM side of the transition).</summary>
public static class VisitStageKeys
{
    public const string Before = "BEFORE";
    public const string During = "DURING";
    public const string After = "AFTER";
}
