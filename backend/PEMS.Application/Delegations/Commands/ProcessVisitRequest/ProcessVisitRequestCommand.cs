using MediatR;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Delegations.Commands.ProcessVisitRequest;

/// <summary>
/// UC-22 Process Visit Request — the Staff Leader picks the host for a campus instance.
/// Two modes (resolved from the request scope/status):
///   • SINGLE_CAMPUS + request PENDING: approve the request AND assign the chosen host
///     staff member; instance stays ASSIGNED.
/// The assignment also sends the HOST_ASSIGNMENT invitation email to the chosen host
/// (sent_emails + sent_email_recipients). <see cref="EmailOverride"/> optionally carries the
/// Staff Leader-edited subject/body from the "Xem trước email" step (assignment is final —
/// the email has no accept/decline action tokens).
/// </summary>
public sealed record ProcessVisitRequestCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    ulong HostUserId,
    EmailOverride? EmailOverride = null) : IRequest<ProcessVisitRequestResponse>;
