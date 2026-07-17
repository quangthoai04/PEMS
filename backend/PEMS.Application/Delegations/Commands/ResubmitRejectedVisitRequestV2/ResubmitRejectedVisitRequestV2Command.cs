using System.Collections.Generic;
using MediatR;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

namespace PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequestV2;

/// <summary>
/// Per-campus form v2 RESUBMIT after full rejection (plan §6.4): request REJECTED + every campus REJECTED.
/// The campus set is FIXED and every visitInstanceId is KEPT (no delete/recreate — downstream history/FKs stay
/// intact); old campus decisions are snapshotted to audit before being cleared; every instance goes back to
/// WAITING_REQUEST_APPROVAL routed to the CURRENT campus Staff Leader. Same two-flag gate, editor policy and
/// optimistic-concurrency contract as pending-edit v2.
/// </summary>
public sealed record ResubmitRejectedVisitRequestV2Command(ulong VisitRequestId, VisitRequestEditV2Dto Edit)
    : IRequest<ResubmitRejectedVisitRequestV2Response>;

public sealed record ResubmitRejectedVisitRequestV2Response(
    ulong VisitRequestId,
    string Status,
    int ResubmissionCount,
    string VisitScope,
    bool HasMixedCampusDetails,
    int RequestRowVersion,
    IReadOnlyList<CreateVisitRequestV2CampusRef> Instances,
    string Message);
