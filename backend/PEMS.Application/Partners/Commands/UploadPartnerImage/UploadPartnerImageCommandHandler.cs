using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Commands.UploadPartnerImage;

/// <summary>
/// Uploads a partner logo/cover to Google Drive via the shared <see cref="IFileUploadService"/>,
/// nesting it under a per-partner-code subfolder of the configured partner root
/// (<c>DocumentPartnerFolderId / {partner_code} / Ảnh {1|2}</c>) so every partner's images live
/// together, mirroring the <c>VR-{code}/{campus}/Ảnh</c> structure the visit-photo feature already
/// uses. <see cref="IGoogleDriveStorageService.EnsureChildFolderAsync"/> is idempotent (find-or-create),
/// so no local folder-id cache table is needed for a single logo + single cover per partner.
/// </summary>
public sealed class UploadPartnerImageCommandHandler
    : IRequestHandler<UploadPartnerImageCommand, UploadPartnerImageResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;
    private readonly IGoogleDriveStorageService _drive;
    private readonly IFileStorageFolderResolver _folderResolver;

    public UploadPartnerImageCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileUploadService fileUpload,
        IGoogleDriveStorageService drive,
        IFileStorageFolderResolver folderResolver)
    {
        _db = db;
        _currentUser = currentUser;
        _fileUpload = fileUpload;
        _drive = drive;
        _folderResolver = folderResolver;
    }

    public async Task<UploadPartnerImageResponse> Handle(
        UploadPartnerImageCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
            throw new ForbiddenException("Bạn chưa đăng nhập.");

        var partner = await _db.Partners
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanEditPartner(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền chỉnh sửa đối tác này.", 403);

        if (request.Content is null || request.Content.Length == 0)
            throw new BusinessRuleException("Vui lòng chọn ảnh.", "FILE_REQUIRED");

        var purpose = request.Kind == PartnerImageKind.Logo ? FilePurpose.PartnerLogo : FilePurpose.PartnerCover;
        var rootFolderId = _folderResolver.ResolveFolderId(purpose);

        // Draft/pending-approval partners can still lack a code (assigned on approval) — fall back to
        // the numeric id so the folder can always be created (mirrors VisitPhotoFolderService's VR-{id}).
        var folderName = !string.IsNullOrWhiteSpace(partner.PartnerCode)
            ? partner.PartnerCode!
            : $"Partner-{partner.PartnerId}";
        var partnerFolder = await _drive.EnsureChildFolderAsync(folderName, rootFolderId, cancellationToken);

        var displayName = request.Kind == PartnerImageKind.Logo ? "Ảnh 1" : "Ảnh 2";
        var ext = Path.GetExtension(request.FileName);
        var driveFileName = string.IsNullOrEmpty(ext) ? displayName : $"{displayName}{ext}";

        await using var stream = new MemoryStream(request.Content, writable: false);
        var uploaded = await _fileUpload.UploadBusinessFileAsync(
            stream,
            driveFileName,
            request.ContentType ?? string.Empty,
            request.Content.LongLength,
            purpose,
            (long)userId,
            partnerFolder.ExternalFolderId,
            cancellationToken);

        return new UploadPartnerImageResponse
        {
            FileId       = uploaded.FileId,
            FileUrl      = uploaded.FileUrl,
            ThumbnailUrl = uploaded.ThumbnailUrl,
            MimeType     = uploaded.MimeType,
            FileSize     = uploaded.FileSize,
        };
    }
}
