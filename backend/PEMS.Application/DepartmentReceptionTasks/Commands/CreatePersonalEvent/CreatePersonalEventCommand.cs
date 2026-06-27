using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Calendar;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.CreatePersonalEvent
{
    public class CreatePersonalEventCommand : IRequest<ulong>
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Date { get; set; } // YYYY-MM-DD
        public string StartTime { get; set; } // HH:mm
        public string EndTime { get; set; } // HH:mm
    }

    public class CreatePersonalEventCommandHandler : IRequestHandler<CreatePersonalEventCommand, ulong>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public CreatePersonalEventCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<ulong> Handle(CreatePersonalEventCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Title)) throw new Exception("Vui lòng nhập tiêu đề");
            if (string.IsNullOrWhiteSpace(request.Date) || string.IsNullOrWhiteSpace(request.StartTime) || string.IsNullOrWhiteSpace(request.EndTime))
                throw new Exception("Vui lòng nhập thời gian hợp lệ");

            if (!DateTime.TryParse($"{request.Date}T{request.StartTime}:00", out var startAt) ||
                !DateTime.TryParse($"{request.Date}T{request.EndTime}:00", out var endAt))
            {
                throw new Exception("Định dạng thời gian không hợp lệ");
            }

            if (endAt <= startAt) throw new Exception("Thời gian kết thúc phải lớn hơn thời gian bắt đầu");

            ulong userId = _currentUserService.UserId.Value;

            var ev = new CalendarEvent
            {
                OwnerUserId = userId,
                Title = request.Title,
                Description = request.Description,
                StartAt = startAt,
                EndAt = endAt,
                SourceType = "PERSONAL",
                Timezone = "Asia/Ho_Chi_Minh",
                Visibility = "PRIVATE",
                Status = "ACTIVE",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.CalendarEvents.Add(ev);
            await _context.SaveChangesAsync(cancellationToken);

            return ev.CalendarEventId;
        }
    }
}
