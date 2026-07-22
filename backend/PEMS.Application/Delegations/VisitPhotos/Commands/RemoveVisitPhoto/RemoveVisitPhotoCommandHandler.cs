using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.VisitPhotos.Commands.RemoveVisitPhoto;

public sealed class RemoveVisitPhotoCommandHandler : IRequestHandler<RemoveVisitPhotoCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RemoveVisitPhotoCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RemoveVisitPhotoCommand request, CancellationToken cancellationToken)
    {
        var photo = await _db.VisitPhotos
            .FirstOrDefaultAsync(p => p.VisitPhotoId == request.VisitPhotoId, cancellationToken)
            ?? throw new NotFoundException("VisitPhoto", request.VisitPhotoId);

        // Scope gate resolves participation from the photo's OWN instance id (anti-IDOR).
        var scope = await VisitInstanceMediaAccessScope.ResolveAsync(
            _db, _currentUser, photo.VisitInstanceId, cancellationToken);

        if (photo.UploadedBy != scope.UserId)
            throw new ForbiddenException("Bạn chỉ có thể xóa ảnh do chính mình tải lên.");

        if (photo.Status == "REMOVED")
            throw new BusinessRuleException("Ảnh này đã được xóa trước đó.", "VISIT_PHOTO_ALREADY_REMOVED");

        var now = VietnamTime.Now();
        photo.Status = "REMOVED";
        photo.RemovedAt = now;
        photo.RemovedBy = scope.UserId;
        photo.RemovalReason = request.Reason.Trim();

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = scope.UserId,
            Action = "REMOVE_VISIT_PHOTO",
            EntityType = "VisitPhoto",
            EntityId = photo.VisitPhotoId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
