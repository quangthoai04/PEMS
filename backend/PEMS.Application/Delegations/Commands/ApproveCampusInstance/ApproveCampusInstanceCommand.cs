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
/// <see cref="PEMS.Application.Delegations.Services.VisitInstanceConcurrencyGuard"/>.
/// <para>
/// REQUIRED, and deliberately not nullable with a default. While it was optional, an omitted value was
/// read as "no expectation" and the decision went through against whatever the row had become — so the
/// protection was one absent JSON field away from being switched off, by an old client, a script or a
/// caller that simply forgot. A decision that cannot say which revision it judged is not a decision
/// worth recording, so there is no longer a way to express one: in-process callers cannot compile
/// without it, and the HTTP boundary refuses a missing field with
/// <c>VISIT_INSTANCE_VERSION_REQUIRED</c>.
/// </para>
/// </param>
public sealed record ApproveCampusInstanceCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    ulong HostUserId,
    string? DecisionNote,
    int ExpectedInstanceRowVersion) : IRequest<ApproveCampusInstanceResponse>;
