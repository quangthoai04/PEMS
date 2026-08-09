using MediatR;

namespace PEMS.Application.Delegations.Commands.ApproveCampusInstance;

/// <summary>
/// Campus-independent approval (SQL v10): the Staff Leader of the instance's campus approves
/// the campus instance and MUST pick the official host in the same action —
/// WAITING_REQUEST_APPROVAL → ASSIGNED, decision fields recorded on the instance, and the
/// request's aggregate status recalculated. The host may be an IC Staff of the campus or the
/// approving Staff Leader themself (self-host). Gán host chỉ tạo thông báo trong hệ thống
/// (không gửi email mời host).
/// </summary>
/// <param name="ExpectedInstanceRowVersion">
/// The campus <c>rowVersion</c> the review screen rendered. An approval is a statement about the
/// CONTENT the approver read, so the decision is refused with a 409
/// (<c>VISIT_INSTANCE_VERSION_CONFLICT</c>) when the guest has edited the campus since — see
/// <see cref="PEMS.Application.Delegations.Services.VisitInstanceConcurrencyGuard"/>. Nullable so an
/// older client still functions; every decision UI sends it.
/// </param>
public sealed record ApproveCampusInstanceCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    ulong HostUserId,
    string? DecisionNote,
    int? ExpectedInstanceRowVersion = null) : IRequest<ApproveCampusInstanceResponse>;
