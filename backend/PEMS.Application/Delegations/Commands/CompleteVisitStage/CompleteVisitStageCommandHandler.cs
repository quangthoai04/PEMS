using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.CompleteVisitStage;

/// <summary>
/// Advances a campus instance one operational stage forward. Only the official current host of
/// the instance may do so. Transition guards return clean business errors (409) instead of letting
/// an invalid status or a DB trigger surface as a generic 500.
/// </summary>
public sealed class CompleteVisitStageCommandHandler
    : IRequestHandler<CompleteVisitStageCommand, CompleteVisitStageResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public CompleteVisitStageCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<CompleteVisitStageResponse> Handle(
        CompleteVisitStageCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId
                                      && c.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Operational stage transitions are a HOST-only action (the official current host of THIS
        // instance). Staff Leader/HO monitor read-only; Visitor/Dept/Student have no process control.
        if (instance.CurrentHostUserId != actorId)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được cập nhật tiến độ tiếp khách.");

        if (instance.Status == VisitInstanceStatus.Cancelled)
            throw new ConflictException("Cơ sở này đã bị hủy nên không thể cập nhật tiến độ.");
        if (instance.Status == VisitInstanceStatus.Closed)
            throw new ConflictException("Cơ sở này đã đóng đoàn nên không thể cập nhật tiến độ.");

        var now = _clock.UtcNow;
        string newStatus;
        string action;
        string message;

        switch (request.Stage)
        {
            case VisitStageKeys.Before:
                // Finish preparation → start the visit.
                if (instance.Status != VisitInstanceStatus.Assigned && instance.Status != VisitInstanceStatus.BeforeVisit)
                    throw new ConflictException("Không thể bắt đầu tiếp khách. Cơ sở chưa ở giai đoạn chuẩn bị.");

                // Precondition: no mandatory preparation item may still be blocking. Today the only
                // persisted blocking item is a participant invitation still awaiting a response
                // (status INVITED). When the agenda/logistics persistence APIs land, extend this
                // check (e.g. unconfirmed logistics, missing agenda) — same 409 surface.
                var missing = new List<string>();

                var pendingInvites = await _db.VisitParticipants
                    .CountAsync(p => p.VisitInstanceId == instance.VisitInstanceId
                                     && p.Status == ParticipantStatuses.Invited, cancellationToken);
                if (pendingInvites > 0)
                    missing.Add($"{pendingInvites} lời mời tham gia chưa được phản hồi");

                var agendaCount = await _db.VisitAgendas
                    .CountAsync(a => a.VisitInstanceId == instance.VisitInstanceId, cancellationToken);
                if (agendaCount == 0)
                    missing.Add("lịch trình (agenda) chưa có mục nào");

                if (missing.Count > 0)
                    throw new ConflictException(
                        "Chưa thể chuyển sang giai đoạn đang tiếp khách vì còn hạng mục chuẩn bị chưa hoàn tất: "
                        + string.Join("; ", missing) + ".");

                newStatus = VisitInstanceStatus.DuringVisit;
                action = "COMPLETE_BEFORE_VISIT";
                message = "Đã hoàn thành chuẩn bị. Chuyển sang giai đoạn đang tiếp khách.";
                break;

            case VisitStageKeys.During:
                if (instance.Status != VisitInstanceStatus.DuringVisit)
                    throw new ConflictException("Không thể hoàn thành tiếp khách. Cơ sở chưa ở giai đoạn đang tiếp khách.");
                newStatus = VisitInstanceStatus.AfterVisit;
                action = "COMPLETE_DURING_VISIT";
                message = "Đã hoàn thành tiếp khách. Chuyển sang giai đoạn sau tiếp khách.";
                break;

            case VisitStageKeys.After:
                if (instance.Status != VisitInstanceStatus.AfterVisit)
                    throw new ConflictException("Không thể đóng đoàn. Cơ sở chưa ở giai đoạn sau tiếp khách.");
                newStatus = VisitInstanceStatus.Closed;
                action = "CLOSE_VISIT_INSTANCE";
                message = "Đã đóng đoàn. Hồ sơ tiếp khách được lưu trữ.";
                instance.ClosedBy = actorId;
                instance.ClosedAt = now;
                break;

            default:
                throw new BusinessRuleException("Giai đoạn không hợp lệ.");
        }

        instance.Status = newStatus;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;
        instance.RowVersion += 1;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = action,
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new CompleteVisitStageResponse(
            instance.VisitRequestId, instance.VisitInstanceId, instance.Status, message);
    }
}
