using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Documents;

namespace PEMS.Application.Profiles.Commands.UploadProfileAvatar;

/// <summary>
/// UC-15 avatar upload. All validation lives here (not a FluentValidation validator) so the handler
/// owns the exact error codes. Flow: validate file (size / mime / extension / magic bytes) → load the
/// ACTIVE current user → upload to Google Drive → insert a <c>files</c> row → point
/// <c>users.avatar_url</c> at the backend proxy. No DB transaction can span Drive, so a DB failure
/// after a successful upload triggers a best-effort Drive delete to avoid orphaned files.
/// </summary>
public sealed class UploadProfileAvatarCommandHandler
    : IRequestHandler<UploadProfileAvatarCommand, UploadProfileAvatarResponse>
{
    private const long MaxAvatarBytes = 2 * 1024 * 1024; // 2 MB
    private const string StorageProvider = "GOOGLE_DRIVE";
    private const string FilePurpose = "USER_AVATAR";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IGoogleDriveStorageService _drive;
    private readonly IDateTimeService _clock;

    public UploadProfileAvatarCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IGoogleDriveStorageService drive,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _drive = drive;
        _clock = clock;
    }

    public async Task<UploadProfileAvatarResponse> Handle(
        UploadProfileAvatarCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();

        // --- File validation (defence in depth — the frontend validates too) ---
        if (request.Content is null || request.Content.Length == 0)
            throw new BusinessRuleException("Vui lòng chọn ảnh đại diện.", "AVATAR_FILE_REQUIRED");

        if (request.Content.LongLength > MaxAvatarBytes)
            throw new BusinessRuleException("Ảnh đại diện không được vượt quá 2MB.", "AVATAR_FILE_TOO_LARGE");

        var ext = NormalizeExtension(request.FileName);
        var mime = NormalizeMime(request.ContentType, request.Content, ext);
        if (mime is null)
            throw new BusinessRuleException(
                "Ảnh đại diện chỉ hỗ trợ JPG, PNG hoặc WEBP.", "AVATAR_INVALID_TYPE");

        // --- Load the current user; only ACTIVE users may change their avatar ---
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");

        if (!string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Tài khoản không còn hoạt động.", "USER_INACTIVE");

        // Opaque, collision-free key: avatars/{userId}/{yyyyMMddHHmmss}_{guid}{ext}.
        var objectKey = $"avatars/{userId}/{_clock.VietnamNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
        var checksum = Convert.ToHexString(SHA256.HashData(request.Content)).ToLowerInvariant();

        string? uploadedExternalFileId = null;
        try
        {
            var upload = await _drive.UploadAvatarAsync(request.Content, objectKey, mime, cancellationToken);
            uploadedExternalFileId = upload.ExternalFileId;

            var file = new UploadedFile
            {
                StorageProvider = StorageProvider,
                ObjectKey = objectKey,
                OriginalFilename = SafeOriginalName(request.FileName, ext),
                MimeType = mime,
                FileSize = upload.FileSize,
                ChecksumSha256 = checksum,
                ExternalFileId = upload.ExternalFileId,
                WebViewUrl = upload.WebViewUrl,
                DownloadUrl = upload.DownloadUrl,
                ThumbnailUrl = upload.ThumbnailUrl,
                FilePurpose = FilePurpose,
                UploadedBy = userId,
                UploadedAt = _clock.VietnamNow,
            };
            _db.Files.Add(file);
            await _db.SaveChangesAsync(cancellationToken); // assigns file.FileId

            var avatarUrl = $"/api/files/{file.FileId}/content";
            user.AvatarUrl = avatarUrl;
            user.UpdatedAt = _clock.UtcNow;
            user.UpdatedBy = userId;
            await _db.SaveChangesAsync(cancellationToken);

            return new UploadProfileAvatarResponse
            {
                FileId = (long)file.FileId,
                AvatarUrl = avatarUrl,
                WebViewUrl = upload.WebViewUrl,
                ThumbnailUrl = upload.ThumbnailUrl,
            };
        }
        catch
        {
            // No DB transaction spans Drive: if the row/user update failed after a successful upload,
            // delete the just-uploaded Drive file so we don't leak orphans.
            if (!string.IsNullOrWhiteSpace(uploadedExternalFileId))
            {
                try { await _drive.DeleteAsync(uploadedExternalFileId!, cancellationToken); }
                catch { /* best-effort cleanup — already logged in the service */ }
            }
            throw;
        }
    }

    /// <summary>Lower-cased extension (incl. dot) limited to the avatar allowlist, else empty.</summary>
    private static string NormalizeExtension(string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" ? ext : string.Empty;
    }

    /// <summary>
    /// Returns the canonical MIME type when the declared content type, extension AND magic bytes all
    /// agree on jpeg/png/webp; otherwise null (rejects SVG, spoofed content types, mismatched bytes).
    /// </summary>
    private static string? NormalizeMime(string? contentType, byte[] content, string ext)
    {
        if (ext.Length == 0) return null;

        var declared = (contentType ?? string.Empty).Trim().ToLowerInvariant();

        if (IsJpeg(content) && declared is "image/jpeg" or "image/jpg" && ext is ".jpg" or ".jpeg")
            return "image/jpeg";
        if (IsPng(content) && declared == "image/png" && ext == ".png")
            return "image/png";
        if (IsWebp(content) && declared == "image/webp" && ext == ".webp")
            return "image/webp";

        return null;
    }

    private static bool IsJpeg(byte[] b) =>
        b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

    private static bool IsPng(byte[] b) =>
        b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
        && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A;

    private static bool IsWebp(byte[] b) =>
        b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46  // "RIFF"
        && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50;             // "WEBP"

    /// <summary>Strips any path, keeps a sane filename for display/audit (never used as a disk path).</summary>
    private static string SafeOriginalName(string? fileName, string ext)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) name = $"avatar{ext}";
        return name.Length > 255 ? name[^255..] : name;
    }
}
