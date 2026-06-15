using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.UpdateVisitLogistics;

public sealed class UpdateVisitLogisticsCommandHandler : IRequestHandler<UpdateVisitLogisticsCommand, UpdateVisitLogisticsResponse>
{
    public Task<UpdateVisitLogisticsResponse> Handle(UpdateVisitLogisticsCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Update Visit Logistics has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}