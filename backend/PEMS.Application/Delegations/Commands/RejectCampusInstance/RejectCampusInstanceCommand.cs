using MediatR;

namespace PEMS.Application.Delegations.Commands.RejectCampusInstance;

/// <summary>
/// Campus-independent approval (SQL v10): the Staff Leader of the instance's campus rejects
/// ONLY their campus instance — WAITING_REQUEST_APPROVAL → REJECTED with a mandatory reason
/// stored in the instance's decision_note. Other campuses of the same request are untouched;
/// the request's aggregate status is recalculated afterwards.
/// </summary>
/// <param name="ExpectedInstanceRowVersion">
/// The campus <c>rowVersion</c> the review screen rendered. A rejection is a judgement on the CONTENT
/// the reviewer read — refusing a visit whose purpose or schedule has since been corrected is as wrong
/// as approving one — so a stale version is refused with a 409
/// (<c>VISIT_INSTANCE_VERSION_CONFLICT</c>). Nullable so an older client still functions.
/// </param>
public sealed record RejectCampusInstanceCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string DecisionNote,
    int? ExpectedInstanceRowVersion = null) : IRequest<RejectCampusInstanceResponse>;
