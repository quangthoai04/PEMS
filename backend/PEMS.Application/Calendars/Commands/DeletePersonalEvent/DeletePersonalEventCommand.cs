using MediatR;

namespace PEMS.Application.Calendars.Commands.DeletePersonalEvent
{
    public class DeletePersonalEventCommand : IRequest<bool>
    {
        public ulong CalendarEventId { get; set; }
        public DeletePersonalEventCommand() { }
        public DeletePersonalEventCommand(ulong id) { CalendarEventId = id; }
    }
}