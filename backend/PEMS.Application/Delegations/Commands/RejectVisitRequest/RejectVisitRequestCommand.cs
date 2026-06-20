using MediatR;

namespace PEMS.Application.Delegations.Commands.RejectVisitRequest;

/// <summary>
/// Reject a PENDING visit request (decision stage). Shared by:
///   • HO rejecting a MULTI_CAMPUS request   (UC-18, endpoint /ho-reject)
///   • Staff Leader rejecting a SINGLE_CAMPUS request of their campus (UC-22, endpoint /campus-reject)
/// A reason is mandatory. Reject is the PRE-approval counterpart of Cancel (UC-136, post-approval).
/// </summary>
public sealed record RejectVisitRequestCommand(ulong VisitRequestId, string Reason)
    : IRequest<RejectVisitRequestResponse>;
