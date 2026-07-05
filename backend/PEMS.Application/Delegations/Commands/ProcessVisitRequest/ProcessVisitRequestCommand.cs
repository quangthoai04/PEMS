using MediatR;

namespace PEMS.Application.Delegations.Commands.ProcessVisitRequest;

/// <summary>
/// UC-22 Process Visit Request — the Staff Leader picks the host for a campus instance.
/// Two modes (resolved from the request scope/status):
///   • SINGLE_CAMPUS + request PENDING: approve the request AND assign the chosen host
///     staff member; instance stays ASSIGNED.
/// Gán host chỉ tạo thông báo trong hệ thống (không gửi email mời host) — Staff xem việc
/// được gán qua notification/lịch của tôi và tự vào "Setup đoàn khách" để chuẩn bị.
/// </summary>
public sealed record ProcessVisitRequestCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    ulong HostUserId) : IRequest<ProcessVisitRequestResponse>;
