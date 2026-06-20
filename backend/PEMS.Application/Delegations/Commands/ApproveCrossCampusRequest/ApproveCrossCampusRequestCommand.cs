using MediatR;

namespace PEMS.Application.Delegations.Commands.ApproveCrossCampusRequest;

/// <summary>
/// UC-18 Approve Cross-Campus Request. HO approves a MULTI_CAMPUS request. On approval the
/// request becomes APPROVED and every campus instance is auto-assigned to the campus IC head
/// (Staff Leader) as interim host and moved to ASSIGNED; each Staff Leader can later hand the
/// host off to a real staff member via UC-22.
/// </summary>
public sealed record ApproveCrossCampusRequestCommand(ulong VisitRequestId)
    : IRequest<ApproveCrossCampusRequestResponse>;
