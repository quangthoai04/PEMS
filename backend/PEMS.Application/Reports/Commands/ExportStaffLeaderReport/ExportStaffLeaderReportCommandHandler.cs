using MediatR;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Reports.Commands.ExportStaffLeaderReport;

public class ExportStaffLeaderReportCommandHandler : IRequestHandler<ExportStaffLeaderReportCommand, ExportStaffLeaderReportResult>
{
    public Task<ExportStaffLeaderReportResult> Handle(ExportStaffLeaderReportCommand request, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Partnership Engagement Management System");
        sb.AppendLine("Staff Leader Campus Operation Report");
        sb.AppendLine($"Export Format: {request.ExportFormat}");
        // CSV logic mockup
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        
        return Task.FromResult(new ExportStaffLeaderReportResult
        {
            Content = bytes,
            ContentType = "text/csv",
            FileName = $"PEMS_StaffLeader_Campus_Report_{System.DateTime.Now:yyyyMMdd_HHmm}.csv"
        });
    }
}
