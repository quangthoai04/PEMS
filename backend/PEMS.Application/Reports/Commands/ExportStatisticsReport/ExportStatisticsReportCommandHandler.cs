using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Reports.Commands.ExportStatisticsReport;

public sealed class ExportStatisticsReportCommandHandler : IRequestHandler<ExportStatisticsReportCommand, ExportStatisticsReportResponse>
{
    public Task<ExportStatisticsReportResponse> Handle(ExportStatisticsReportCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Export Statistics Report has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}