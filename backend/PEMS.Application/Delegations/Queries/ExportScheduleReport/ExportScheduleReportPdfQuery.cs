using MediatR;

namespace PEMS.Application.Delegations.Queries.ExportScheduleReport;

/// <summary>"Báo cáo Lịch trình" PDF for a campus instance — same scope rule as the VisitProcess
/// detail screen it is downloaded from (Host / Staff Leader of the campus / HO / Visitor owner /
/// accepted participant). <paramref name="LanguageCode"/> "en" machine-translates the free-text
/// content (delegation name, purpose, organizations, agenda) to English before rendering; anything
/// else renders the original Vietnamese content.</summary>
public sealed record ExportScheduleReportPdfQuery(ulong VisitRequestId, ulong VisitInstanceId, string LanguageCode = "vi")
    : IRequest<byte[]>;
