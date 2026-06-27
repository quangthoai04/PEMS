using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Documents;

namespace PEMS.Application.Files.Commands.UploadFile;

public sealed class UploadFileCommandHandler : IRequestHandler<UploadFileCommand, UploadedFileDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly IGoogleDriveStorageService _googleDrive;
    private readonly IFileValidationService _validation;
    private readonly IConfiguration _configuration;

    public UploadFileCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService storage,
        IGoogleDriveStorageService googleDrive,
        IFileValidationService validation,
        IConfiguration configuration)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
        _googleDrive = googleDrive;
        _validation = validation;
        _configuration = configuration;
    }

    public async Task<UploadedFileDto> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        if (request.Content is null || request.Content.Length == 0)
            throw new ValidationException("Tệp tải lên rỗng hoặc không hợp lệ.");

        var fileName = string.IsNullOrWhiteSpace(request.FileName) ? "file" : Path.GetFileName(request.FileName.Trim());

        // Denylist check (extension + mime + size) — throws BusinessRuleException on a bad file.
        _validation.Validate(fileName, request.ContentType, request.Content.Length);

        var now = DateTime.Now;
        var file = new UploadedFile
        {
            OriginalFilename = fileName,
            MimeType = request.ContentType,
            UploadedBy = userId,
            UploadedAt = now,
            FilePurpose = request.Purpose,
        };

        if (request.Purpose == "EMAIL_INLINE" || request.Purpose == "EMAIL_ATTACHMENT")
        {
            var uploadResult = await _googleDrive.UploadFileAsync(request.Content, fileName, request.ContentType ?? "application/octet-stream", null, cancellationToken);
            file.StorageProvider = "GOOGLE_DRIVE";
            file.ExternalFileId = uploadResult.ExternalFileId;
            file.FileSize = uploadResult.FileSize;
            file.WebViewUrl = uploadResult.WebViewUrl;
            file.DownloadUrl = uploadResult.DownloadUrl;
            file.ThumbnailUrl = uploadResult.ThumbnailUrl;
        }
        else
        {
            StoredFileInfo stored;
            await using (var ms = new MemoryStream(request.Content, writable: false))
            {
                stored = await _storage.SaveAsync(ms, fileName, request.ContentType, request.Purpose, cancellationToken);
            }
            file.StorageProvider = stored.StorageProvider;
            file.BucketName = stored.BucketName;
            file.ObjectKey = stored.ObjectKey;
            file.FileSize = stored.FileSize;
            file.ChecksumSha256 = stored.ChecksumSha256;
        }

        _db.Files.Add(file);
        await _db.SaveChangesAsync(cancellationToken);

        if (file.StorageProvider != "GOOGLE_DRIVE")
        {
            // Stable URL pointing at our authenticated download endpoint (used by email MIME + UI).
            var baseUrl = (_configuration["App:PublicApiBaseUrl"] ?? string.Empty).TrimEnd('/');
            var downloadUrl = string.IsNullOrEmpty(baseUrl) ? null : $"{baseUrl}/api/files/{file.FileId}/download";
            var isImage = request.ContentType is { } m && m.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            file.DownloadUrl = downloadUrl;
            file.WebViewUrl = downloadUrl;
            file.ThumbnailUrl = isImage ? downloadUrl : null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new UploadedFileDto
        {
            FileId = file.FileId,
            OriginalFilename = file.OriginalFilename,
            MimeType = file.MimeType,
            FileSize = file.FileSize,
            DownloadUrl = file.DownloadUrl,
            WebViewUrl = file.WebViewUrl,
            ThumbnailUrl = file.ThumbnailUrl,
        };
    }
}
