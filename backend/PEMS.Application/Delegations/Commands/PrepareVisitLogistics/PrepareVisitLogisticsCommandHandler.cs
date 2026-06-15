using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.PrepareVisitLogistics;

public sealed class PrepareVisitLogisticsCommandHandler : IRequestHandler<PrepareVisitLogisticsCommand, PrepareVisitLogisticsResponse>
{
    public Task<PrepareVisitLogisticsResponse> Handle(PrepareVisitLogisticsCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Prepare Visit Logistics has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}