using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Utils;

namespace PEMS.Application.Calendars.Commands.UpdatePersonalEvent
{
    public sealed class UpdatePersonalEventCommandHandler : IRequestHandler<UpdatePersonalEventCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public UpdatePersonalEventCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(UpdatePersonalEventCommand request, CancellationToken cancellationToken)
        {
            var ev = await _context.CalendarEvents.FirstOrDefaultAsync(e => e.CalendarEventId == request.CalendarEventId, cancellationToken);
            if (ev == null) throw new NotFoundException("Lịch cá nhân không tồn tại");

            ulong userId = _currentUserService.UserId.Value;
            if (ev.OwnerUserId != userId) throw new ForbiddenException("Không có quyền chỉnh sửa lịch cá nhân này");

            if (string.IsNullOrWhiteSpace(request.Title)) throw new ValidationException("Vui lòng nhập tiêu đề");
            if (string.IsNullOrWhiteSpace(request.Date) || string.IsNullOrWhiteSpace(request.StartTime) || string.IsNullOrWhiteSpace(request.EndTime))
                throw new ValidationException("Vui lòng nhập thời gian hợp lệ");

            if (!DateTime.TryParse($"{request.Date}T{request.StartTime}:00", out var startAt) ||
                !DateTime.TryParse($"{request.Date}T{request.EndTime}:00", out var endAt))
            {
                throw new ValidationException("Định dạng thời gian không hợp lệ");
            }

            if (endAt <= startAt) throw new ValidationException("Thời gian kết thúc phải lớn hơn thời gian bắt đầu");

            var hasOverlap = await ScheduleConflictChecker.HasConflictAsync(
                _context, userId, startAt, endAt, request.CalendarEventId, null, cancellationToken);

            if (hasOverlap)
            {
                throw new ValidationException("Khung giờ cập nhật lịch cá nhân bị trùng với đơn/thư hoặc lịch khác trong ngày! Vui lòng chọn khung giờ khác.");
            }

            ev.Title = request.Title;
            ev.Description = request.Description;
            ev.StartAt = startAt;
            ev.EndAt = endAt;
            ev.UpdatedAt = VietnamTime.Now();

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}