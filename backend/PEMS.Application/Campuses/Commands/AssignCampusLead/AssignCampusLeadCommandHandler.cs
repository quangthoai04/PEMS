using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Campuses.Commands.AssignCampusLead;

public sealed class AssignCampusLeadCommandHandler : IRequestHandler<AssignCampusLeadCommand, AssignCampusLeadResponse>
{
    public Task<AssignCampusLeadResponse> Handle(AssignCampusLeadCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Assign Campus Lead has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}