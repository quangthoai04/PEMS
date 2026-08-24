using MediatR;
using PEMS.Application.Common.DTOs;

namespace PEMS.Application.Delegations.Commands.ResubmitRejectedVisitInstanceV2;

/// <summary>
/// Sends ONE rejected campus back for review (plan v9 §5).
///
/// <para>
/// Distinct from <c>ResubmitRejectedVisitRequestV2Command</c>, which revives a request every campus
/// refused. That command cannot serve this case at all: it demands that ALL campuses be REJECTED, and a
/// campus refused beside one that was approved and has a host assigned is precisely the situation the
/// person running the refused campus is in.
/// </para>
/// </summary>
public sealed record ResubmitRejectedVisitInstanceV2Command(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    InstanceResubmitScheduleDto Content) : IRequest<ResubmitRejectedVisitInstanceV2Response>;

/// <param name="VisitRequestStatus">
/// Recomputed from the campuses, never assumed — PARTIALLY_APPROVED when a sibling is already approved.
/// </param>
public sealed record ResubmitRejectedVisitInstanceV2Response(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string VisitRequestStatus,
    string VisitInstanceStatus,
    int InstanceRowVersion,
    string Message);
