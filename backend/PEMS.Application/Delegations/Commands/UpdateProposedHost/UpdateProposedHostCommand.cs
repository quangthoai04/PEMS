using MediatR;

namespace PEMS.Application.Delegations.Commands.UpdateProposedHost;

/// <summary>
/// Change or clear the reception-host arrangement of ONE campus while the request is still
/// pre-decision (plan §5.4). Campus-scoped on purpose: the arrangement is a per-campus fact, and a
/// request-level version of this command would let one campus's Staff Leader touch a sibling's.
/// </summary>
/// <param name="ExpectedRowVersion">
/// The campus instance's row_version as the caller last read it. Required: two Leaders editing the
/// same campus must not silently overwrite each other, and the second one deserves a 409 rather than
/// a surprise.
/// </param>
public sealed record UpdateProposedHostCommand(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string HostSelectionMode,
    ulong? ProposedHostUserId,
    int ExpectedRowVersion) : IRequest<UpdateProposedHostResponse>;

public sealed record UpdateProposedHostResponse(
    ulong VisitRequestId,
    ulong VisitInstanceId,
    string HostSelectionMode,
    ulong? ProposedHostUserId,
    string? ProposedHostName,
    string? ProposalStatus,
    int RowVersion,
    string Message);
