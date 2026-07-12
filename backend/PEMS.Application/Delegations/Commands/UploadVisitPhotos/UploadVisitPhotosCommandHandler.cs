using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Files.Commands.UploadFile;
using PEMS.Domain.Entities.Documents;

using PEMS.Application.Common;
namespace PEMS.Application.Delegations.Commands.UploadVisitPhotos;

public sealed class UploadVisitPhotosCommandHandler : IRequestHandler<UploadVisitPhotosCommand, UploadVisitPhotosResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;

    public UploadVisitPhotosCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IMediator mediator)
    {
        _db = db;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public async Task<UploadVisitPhotosResponse> Handle(UploadVisitPhotosCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken);

        if (instance == null)
            throw new NotFoundException("Visit instance not found.");

        // Quyền: Phải là Host, hoặc participant được assigned
        bool isHost = instance.CurrentHostUserId == userId;
        bool isAcceptedParticipant = await _db.VisitParticipants.AnyAsync(
            p => p.VisitInstanceId == request.VisitInstanceId
                 && p.UserId == userId
                 && !p.IsHost
                 && (p.Status == "ACCEPTED" || p.Status == "ASSIGNED"),
            cancellationToken);

        bool isLive = instance.Status != "CLOSED" && instance.Status != "CANCELLED" && instance.VisitRequest.Status != "CANCELLED";
        bool mediaUploader = (isHost || isAcceptedParticipant) && isLive && instance.Status == "AFTER_VISIT";

        if (!mediaUploader)
            throw new ForbiddenException("Bạn không có quyền tải lên media cho chuyến thăm này.");

        if (instance.Status != "AFTER_VISIT" && instance.Status != "DURING_VISIT")
            throw new BusinessRuleException("Chỉ được tải lên media trong hoặc sau khi tiếp khách.");

        foreach (var fileDto in request.Files)
        {
            // Tái sử dụng UploadFileCommand để lưu file vật lý + file metadata
            var uploadResult = await _mediator.Send(new UploadFileCommand
            {
                Content = fileDto.Content,
                FileName = fileDto.FileName,
                ContentType = fileDto.ContentType,
                Purpose = "VISIT_MEDIA"
            }, cancellationToken);

            var doc = new Document
            {
                FileId = uploadResult.FileId,
                OwnerType = "VISIT_INSTANCE_MEDIA",
                OwnerId = request.VisitInstanceId,
                CampusId = instance.CampusId,
                Title = fileDto.FileName,
                Description = "",
                Status = "PUBLISHED", // Tự động publish
                CreatedAt = VietnamTime.Now(),
                CreatedBy = userId
            };

            _db.Documents.Add(doc);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new UploadVisitPhotosResponse();
    }
}