using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.UpdateVisitInstancePreparationNote;

/// <summary>
/// Persists the host's preparation note. Host-only, prep-window-only; clears the value when the note
/// is null/empty. The edit is audited (who/when lives in audit_logs, not extra columns on the row).
/// </summary>
public sealed class UpdateVisitInstancePreparationNoteCommandHandler
    : IRequestHandler<UpdateVisitInstancePreparationNoteCommand, UpdateVisitInstancePreparationNoteResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public UpdateVisitInstancePreparationNoteCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<UpdateVisitInstancePreparationNoteResponse> Handle(
        UpdateVisitInstancePreparationNoteCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Only the official current host may edit; Staff Leader/HO/Visitor are read-only here.
        if (instance.CurrentHostUserId != actorId)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được sửa ghi chú chung.");

        // Editable only during the preparation window.
        if (instance.Status != VisitInstanceStatus.Assigned && instance.Status != VisitInstanceStatus.BeforeVisit)
            throw new ConflictException("Chỉ có thể sửa ghi chú chung trong giai đoạn chuẩn bị.");

        var now = _clock.VietnamNow;
        var normalized = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        instance.PreparationNote = normalized;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;
        instance.RowVersion += 1;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = instance.CampusId,
            Action = "UPDATE_VISIT_PREPARATION_NOTE",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateVisitInstancePreparationNoteResponse(
            instance.VisitInstanceId, instance.PreparationNote, "Đã lưu ghi chú chung.");
    }
}
