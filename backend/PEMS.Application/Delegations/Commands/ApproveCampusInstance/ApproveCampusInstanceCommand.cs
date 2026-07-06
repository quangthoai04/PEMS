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
public sealed record ApproveCampusInstanceCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    ulong HostUserId,
    string? DecisionNote) : IRequest<ApproveCampusInstanceResponse>;
