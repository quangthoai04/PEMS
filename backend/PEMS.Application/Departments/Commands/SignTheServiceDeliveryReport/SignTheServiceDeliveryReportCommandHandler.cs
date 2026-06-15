using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Departments.Commands.SignTheServiceDeliveryReport;

public sealed class SignTheServiceDeliveryReportCommandHandler : IRequestHandler<SignTheServiceDeliveryReportCommand, SignTheServiceDeliveryReportResponse>
{
    public Task<SignTheServiceDeliveryReportResponse> Handle(SignTheServiceDeliveryReportCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Sign The Service Delivery Report has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}