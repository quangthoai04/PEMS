using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Delegations.VisitPhotos;
using PEMS.Domain.Constants;

namespace PEMS.Application.News.Commands.UploadNewsCoverImage;

/// <summary>
/// Uploads a news cover/section image to Google Drive via the shared <see cref="IFileUploadService"/>.
/// Only Staff (regular) and Student — the roles allowed to create news — can call this.
/// When <see cref="UploadNewsCoverImageCommand.VisitInstanceId"/> is set, the image is routed into
/// that đoàn's Drive folder (reusing <see cref="IVisitPhotoFolderService"/>, the same
/// VR-{visit_request_id}/{campus_code} structure the Student visit-photo feature already
/// provisions) instead of the flat News folder — whether the file was freshly captured or picked
/// from elsewhere, the server re-uploads the bytes itself, so it always lands in the right folder.
/// Returns the fileId to include in the subsequent CreateNews/EditNews request.
/// </summary>
public sealed class UploadNewsCoverImageCommandHandler
    : IRequestHandler<UploadNewsCoverImageCommand, UploadNewsCoverImageResponse>
{
    private readonly ICurrentUserService     _currentUser;
    private readonly IFileUploadService      _fileUpload;
    private readonly IApplicationDbContext   _dbContext;
    private readonly IVisitPhotoFolderService _visitPhotoFolders;

    public UploadNewsCoverImageCommandHandler(
        ICurrentUserService      currentUser,
        IFileUploadService       fileUpload,
        IApplicationDbContext    dbContext,
        IVisitPhotoFolderService visitPhotoFolders)
    {
        _currentUser       = currentUser;
        _fileUpload        = fileUpload;
        _dbContext         = dbContext;
        _visitPhotoFolders = visitPhotoFolders;
    }

    public async Task<UploadNewsCoverImageResponse> Handle(
        UploadNewsCoverImageCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
            throw new ForbiddenException("Bạn chưa đăng nhập.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;
        var subRole  = _currentUser.SubRole  ?? string.Empty;

        var isStaffLeader = roleCode == RoleCodes.Staff && subRole == UserSubRoles.Leader;
        var isAllowed = (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff)
                     || isStaffLeader
                     || roleCode == RoleCodes.Student;
        if (!isAllowed)
            throw new ForbiddenException("Chỉ Staff và Student mới có thể tải ảnh bìa tin tức.");

        // Leader chỉ được viết tin tức khi tự host 1 đoàn (see CreateNewsCommandHandler) — nên
        // ảnh bìa của Leader cũng phải gắn với đoàn đó; access thật kiểm ở
        // VisitInstanceMediaAccessScope bên dưới.
        if (isStaffLeader && !request.VisitInstanceId.HasValue)
            throw new ForbiddenException("Chỉ Staff và Student mới có thể tải ảnh bìa tin tức không gắn đoàn.");

        if (request.Content is null || request.Content.Length == 0)
            throw new BusinessRuleException("Vui lòng chọn ảnh bìa.", "FILE_REQUIRED");

        await using var stream = new MemoryStream(request.Content, writable: false);

        var uploaded = request.VisitInstanceId.HasValue
            ? await UploadIntoVisitInstanceFolderAsync(request, stream, userId, cancellationToken)
            : await _fileUpload.UploadBusinessFileAsync(
                stream,
                request.FileName,
                request.ContentType ?? string.Empty,
                request.Content!.LongLength,
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

    private async Task<UploadedFileDto> UploadIntoVisitInstanceFolderAsync(
        UploadNewsCoverImageCommand request, Stream stream, ulong userId, CancellationToken cancellationToken)
    {
        var scope = await VisitInstanceMediaAccessScope.ResolveAsync(
            _dbContext, _currentUser, request.VisitInstanceId!.Value, cancellationToken);

        var target = await _visitPhotoFolders.EnsureUploadTargetAsync(
            scope.Instance, scope.CampusCode, userId, cancellationToken);

        return await _fileUpload.UploadBusinessFileAsync(
            stream,
            request.FileName,
            request.ContentType ?? string.Empty,
            request.Content!.LongLength,
            FilePurpose.NewsImage,
            (long)userId,
            target.CampusFolderExternalId,
            cancellationToken);
    }
}
