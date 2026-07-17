using System.Collections.Generic;
using MediatR;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

namespace PEMS.Application.Delegations.Commands.UpdatePendingVisitRequestV2;

/// <summary>
/// Per-campus form v2 PENDING EDIT (plan §6.4): edits a still-fully-pending v2 request per campus — per-campus
/// content and schedule, full-replace of the target instance's members (copy-on-write for legacy shared rows),
/// add campus, remove pending campus. Explicit optimistic concurrency (request + per-instance expected
/// row_version → stable 409). Gated by BOTH feature flags exactly like create-v2 (write OFF → 404; write ON +
/// read OFF → reject). Editors: the registrant or the ACTIVE primary contact. Account-binding emails are
/// immutable here (identity changes are Phase D).
/// </summary>
public sealed record UpdatePendingVisitRequestV2Command(ulong VisitRequestId, VisitRequestEditV2Dto Edit)
    : IRequest<UpdatePendingVisitRequestV2Response>;

public sealed record UpdatePendingVisitRequestV2Response(
    ulong VisitRequestId,
    string Status,
    string VisitScope,
    bool HasMixedCampusDetails,
    int RequestRowVersion,
    IReadOnlyList<CreateVisitRequestV2CampusRef> Instances,
    string Message);
