using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Partners.Documents.Commands.UploadPartnerDocument;

public sealed class UploadPartnerDocumentCommandHandler
    : IRequestHandler<UploadPartnerDocumentCommand, PartnerDocumentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public UploadPartnerDocumentCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PartnerDocumentDto> Handle(
        UploadPartnerDocumentCommand request, CancellationToken cancellationToken)
    {
        var partner = await _db.Partners
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanManagePartnerChildren(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền thêm tài liệu cho đối tác này.", 403);

        var file = await _db.Files.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FileId == request.FileId, cancellationToken)
            ?? throw new NotFoundException("File", request.FileId);

        var now = _clock.VietnamNow;
        var document = new Document
        {
            FileId = file.FileId,
            OwnerType = "PARTNER",
            OwnerId = partner.PartnerId,
            CampusId = partner.OwnerCampusId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DocumentCategory = string.IsNullOrWhiteSpace(request.DocumentCategory) ? null : request.DocumentCategory.Trim(),
            Status = "PUBLISHED",
            CreatedAt = now,
            CreatedBy = _currentUser.UserId,
        };
        _db.Documents.Add(document);
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = partner.OwnerCampusId,
            Action = "UPLOAD_PARTNER_DOCUMENT",
            EntityType = "Document",
            EntityId = null,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new PartnerDocumentDto
        {
            DocumentId = document.DocumentId,
            FileId = document.FileId,
            Title = document.Title,
            Description = document.Description,
            DocumentCategory = document.DocumentCategory,
            Status = document.Status,
            OriginalFilename = file.OriginalFilename,
            MimeType = file.MimeType,
            FileSize = file.FileSize is { } size ? (long?)size : null,
            DownloadUrl = file.DownloadUrl,
            CreatedAt = document.CreatedAt,
        };
    }
}
