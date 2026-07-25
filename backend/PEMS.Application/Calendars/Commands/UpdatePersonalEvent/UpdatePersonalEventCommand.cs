using MediatR;

namespace PEMS.Application.Calendars.Commands.UpdatePersonalEvent
{
    public class UpdatePersonalEventCommand : IRequest<bool>
    {
        public ulong CalendarEventId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Date { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
    }
}