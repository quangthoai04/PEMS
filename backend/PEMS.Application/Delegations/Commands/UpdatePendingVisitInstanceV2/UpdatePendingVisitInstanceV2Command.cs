using MediatR;
using PEMS.Application.Common.DTOs;

namespace PEMS.Application.Delegations.Commands.UpdatePendingVisitInstanceV2;

/// <summary>
/// Edits ONE campus that is still waiting for its decision (plan §17).
///
/// <para>
/// The whole-request <c>UpdatePendingVisitRequestV2Command</c> cannot serve this: it is refused the
/// moment ANY campus has been decided, because it rewrites data every campus shares. In a mixed request
/// — one campus approved, one waiting, one refused — that left the WAITING campus with nothing at all,
/// while the refused one had resubmit and the approved one had safe-edit and amendments. This command is
/// that missing door, and it names the campus so nothing else can move with it.
/// </para>
/// </summary>
/// <param name="ApproveAfterSave">
/// The "Lưu và duyệt" case: the actor is the campus's Staff Leader AND is fixing something before
/// approving. Both happen in ONE transaction — an edit that commits beside an approval that fails would
/// leave the campus holding a schedule nobody agreed to. Null means save only, which is the default even
/// for a Staff Leader: editing is not deciding.
/// </param>
public sealed record UpdatePendingVisitInstanceV2Command(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    CampusVisitEditV2Dto Content,
    bool OverrideLeadTimeConfirmed,
    ApproveAfterSaveDto? ApproveAfterSave) : IRequest<UpdatePendingVisitInstanceV2Response>;

public sealed record UpdatePendingVisitInstanceV2Response(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string VisitRequestStatus,
    string VisitInstanceStatus,
    int InstanceRowVersion,
    int RequestRowVersion,
    /// <summary>True when the same call also approved the campus and assigned its Host.</summary>
    bool Approved,
    ulong? HostUserId,
    string Message);
