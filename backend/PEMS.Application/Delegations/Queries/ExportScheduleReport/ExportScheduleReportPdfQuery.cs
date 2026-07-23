using MediatR;

namespace PEMS.Application.Delegations.Queries.ExportScheduleReport;

/// <summary>"Báo cáo Lịch trình" PDF for a campus instance — same scope rule as the VisitProcess
/// detail screen it is downloaded from (Host / Staff Leader of the campus / HO / Visitor owner /
/// accepted participant).</summary>
public sealed record ExportScheduleReportPdfQuery(ulong VisitRequestId, ulong VisitInstanceId)
    : IRequest<byte[]>;
