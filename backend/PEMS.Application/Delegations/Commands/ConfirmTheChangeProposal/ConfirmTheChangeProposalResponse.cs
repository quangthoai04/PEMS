using System;

namespace PEMS.Application.Delegations.Commands.ConfirmTheChangeProposal;

public sealed class ConfirmTheChangeProposalResponse
{
    public ulong LogisticsItemId { get; init; }
    public string Status { get; init; } = "";
    public string Message { get; init; } = "";
}
