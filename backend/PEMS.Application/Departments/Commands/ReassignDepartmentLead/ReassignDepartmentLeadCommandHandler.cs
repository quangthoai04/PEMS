using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Commands.ReassignDepartmentLead;

public sealed class ReassignDepartmentLeadCommandHandler : IRequestHandler<ReassignDepartmentLeadCommand, ReassignDepartmentLeadResponse>
{
    public Task<ReassignDepartmentLeadResponse> Handle(ReassignDepartmentLeadCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Reassign Department Lead has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}