using MediatR;

namespace PEMS.Application.Delegations.Commands.RejectCampusInstance;

/// <summary>
/// Campus-independent approval (SQL v10): the Staff Leader of the instance's campus rejects
/// ONLY their campus instance — WAITING_REQUEST_APPROVAL → REJECTED with a mandatory reason
/// stored in the instance's decision_note. Other campuses of the same request are untouched;
/// the request's aggregate status is recalculated afterwards.
/// </summary>
public sealed record RejectCampusInstanceCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string DecisionNote) : IRequest<RejectCampusInstanceResponse>;
