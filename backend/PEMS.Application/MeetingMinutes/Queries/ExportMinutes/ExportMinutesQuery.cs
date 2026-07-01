using MediatR;

namespace PEMS.Application.MeetingMinutes.Queries.ExportMinutes;

public class ExportMinutesPdfQuery : IRequest<byte[]>
{
    public ulong MinutesId { get; set; }
}

public class ExportMinutesExcelQuery : IRequest<byte[]>
{
    public ulong MinutesId { get; set; }
}
