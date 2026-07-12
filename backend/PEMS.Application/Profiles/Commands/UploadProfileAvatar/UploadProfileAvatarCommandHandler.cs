using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Profiles.Commands.UploadProfileAvatar;

/// <summary>
/// UC-15 avatar upload. The handler owns the business rules (authenticated + ACTIVE user); the actual
/// file plumbing — validation, checksum, object key, Drive upload, <c>files</c> insert and orphan
/// cleanup — is delegated to the shared <see cref="IFileUploadService"/> with
/// <see cref="FilePurpose.UserAvatar"/>. On success it points <c>users.avatar_url</c> at the backend
/// proxy (<c>/api/files/{fileId}/content</c>). The API response is unchanged from before the refactor.
/// </summary>
public sealed class UploadProfileAvatarCommandHandler
    : IRequestHandler<UploadProfileAvatarCommand, UploadProfileAvatarResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;
    private readonly IDateTimeService _clock;

    public UploadProfileAvatarCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileUploadService fileUpload,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _fileUpload = fileUpload;
        _clock = clock;
    }

    public async Task<UploadProfileAvatarResponse> Handle(
        UploadProfileAvatarCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        if (request.Content is null || request.Content.Length == 0)
            throw new BusinessRuleException("Vui lòng chọn ảnh đại diện.", "AVATAR_FILE_REQUIRED");

        // Load the current user; only ACTIVE users may change their avatar.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");

        if (!string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Tài khoản không còn hoạt động.", "USER_INACTIVE");

        // Delegate all file handling to the shared service (validation rules for USER_AVATAR enforce
        // JPG/PNG/WEBP ≤ 2MB with magic-byte checks, and reject SVG / spoofed content types).
        await using var content = new MemoryStream(request.Content, writable: false);
        var uploaded = await _fileUpload.UploadBusinessFileAsync(
            content,
            request.FileName ?? "avatar",
            request.ContentType ?? string.Empty,
            request.Content.LongLength,
            FilePurpose.UserAvatar,
            (long)userId,
            cancellationToken);

        user.AvatarUrl = uploaded.FileUrl;
        user.UpdatedAt = _clock.VietnamNow;
        user.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);

        return new UploadProfileAvatarResponse
        {
            FileId = uploaded.FileId,
            AvatarUrl = uploaded.FileUrl,
            WebViewUrl = uploaded.WebViewUrl,
            ThumbnailUrl = uploaded.ThumbnailUrl,
        };
    }
}
