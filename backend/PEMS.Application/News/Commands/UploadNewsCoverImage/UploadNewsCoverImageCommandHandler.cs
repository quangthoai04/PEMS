using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Application.News.Commands.UploadNewsCoverImage;

/// <summary>
/// Uploads a news cover image to Google Drive via the shared <see cref="IFileUploadService"/>.
/// Only Staff (regular) and Student — the roles allowed to create news — can call this.
/// Returns the fileId to include in the subsequent CreateNews request.
/// </summary>
public sealed class UploadNewsCoverImageCommandHandler
    : IRequestHandler<UploadNewsCoverImageCommand, UploadNewsCoverImageResponse>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService  _fileUpload;

    public UploadNewsCoverImageCommandHandler(
        ICurrentUserService currentUser,
        IFileUploadService  fileUpload)
    {
        _currentUser = currentUser;
        _fileUpload  = fileUpload;
    }

    public async Task<UploadNewsCoverImageResponse> Handle(
        UploadNewsCoverImageCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
            throw new ForbiddenException("Bạn chưa đăng nhập.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;
        var subRole  = _currentUser.SubRole  ?? string.Empty;

        var isAllowed = (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff)
                     || roleCode == RoleCodes.Student;
        if (!isAllowed)
            throw new ForbiddenException("Chỉ Staff thường và Student mới có thể tải ảnh bìa tin tức.");

        if (request.Content is null || request.Content.Length == 0)
            throw new BusinessRuleException("Vui lòng chọn ảnh bìa.", "FILE_REQUIRED");

        await using var stream = new MemoryStream(request.Content, writable: false);
        var uploaded = await _fileUpload.UploadBusinessFileAsync(
            stream,
            request.FileName,
            request.ContentType ?? string.Empty,
            request.Content.LongLength,
            FilePurpose.NewsImage,
            (long)userId,
            cancellationToken);

        return new UploadNewsCoverImageResponse
        {
            FileId       = uploaded.FileId,
            FileUrl      = uploaded.FileUrl,
            ThumbnailUrl = uploaded.ThumbnailUrl,
            MimeType     = uploaded.MimeType,
            FileSize     = uploaded.FileSize
        };
    }
}
