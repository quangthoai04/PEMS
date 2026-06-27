using MediatR;

namespace PEMS.Application.Delegations.Commands.ConfirmTheChangeProposal;

public class ConfirmTheChangeProposalCommand : IRequest<ConfirmTheChangeProposalResponse>
{
    public ulong LogisticsItemId { get; set; }
    public bool Accepted { get; set; }
    public string? Note { get; set; }
}
