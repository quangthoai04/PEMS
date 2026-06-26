using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Documents;

namespace PEMS.Application.Profiles.Commands.UploadProfileAvatar;

/// <summary>
/// UC-15 avatar upload. Resolves the caller from the JWT, validates the image (type / size /
/// magic bytes), uploads it to the Google Drive avatars folder, records the metadata in
/// <c>files</c>, and points <c>users.avatar_url</c> at the backend proxy route. The binary is
/// never stored in the database. If the DB write fails after the upload, the just-uploaded
/// Drive file is removed (best effort) so no orphan is left behind.
/// </summary>
public sealed class UploadProfileAvatarCommandHandler
    : IRequestHandler<UploadProfileAvatarCommand, UploadProfileAvatarResponse>
{
    private const long MaxAvatarSizeBytes = 2L * 1024 * 1024; // 2 MB

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp",
    };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IFileStorageService _storage;
    private readonly IFileStorageFolders _folders;

    public UploadProfileAvatarCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        IFileStorageService storage,
        IFileStorageFolders folders)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _storage = storage;
        _folders = folders;
    }

    public async Task<UploadProfileAvatarResponse> Handle(
        UploadProfileAvatarCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new AuthenticationFailedException("Phiên đăng nhập không hợp lệ.");

        // ── file presence + size ──────────────────────────────────────────────
        if (request.FileStream is null || request.FileSize <= 0)
            throw new BusinessRuleException("Vui lòng chọn ảnh đại diện.", "AVATAR_FILE_REQUIRED");

        if (request.FileSize > MaxAvatarSizeBytes)
            throw new BusinessRuleException("Ảnh đại diện không được vượt quá 2MB.", "AVATAR_FILE_TOO_LARGE");

        // ── declared type / extension ─────────────────────────────────────────
        var extension = Path.GetExtension(request.OriginalFileName ?? string.Empty);
        if (!AllowedMimeTypes.Contains(request.ContentType ?? string.Empty)
            || string.IsNullOrEmpty(extension)
            || !AllowedExtensions.Contains(extension))
        {
            throw new BusinessRuleException("Ảnh đại diện chỉ hỗ trợ JPG, PNG hoặc WEBP.", "AVATAR_INVALID_TYPE");
        }

        // Buffer once so we can sniff the magic bytes AND re-read for the upload, and to bound
        // the size server-side regardless of what the client reported.
        var buffer = await ReadBoundedAsync(request.FileStream, MaxAvatarSizeBytes, cancellationToken);
        if (buffer.Length == 0)
            throw new BusinessRuleException("Vui lòng chọn ảnh đại diện.", "AVATAR_FILE_REQUIRED");
        if (buffer.Length > MaxAvatarSizeBytes)
            throw new BusinessRuleException("Ảnh đại diện không được vượt quá 2MB.", "AVATAR_FILE_TOO_LARGE");

        // ── magic bytes — reject a renamed/forged file (and any SVG) ───────────
        if (!LooksLikeAllowedImage(buffer))
            throw new BusinessRuleException("Ảnh đại diện chỉ hỗ trợ JPG, PNG hoặc WEBP.", "AVATAR_INVALID_TYPE");

        // ── caller must exist and be ACTIVE ────────────────────────────────────
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");

        if (user.Status != UserStatuses.Active)
            throw new ForbiddenException("Tài khoản không còn hoạt động.");

        // ── SHA-256 of the binary content (audit / integrity / future dedupe) — from the bytes
        //    ONLY, never the filename/Drive id/URL, and computed BEFORE upload so a hash failure
        //    aborts the whole operation. The same buffer is uploaded, so no stream-reset risk. ──
        var checksumSha256 = FileChecksumHelper.ComputeSha256Hex(buffer);
        if (checksumSha256.Length != 64)
            throw new BusinessRuleException("Không thể xử lý ảnh đại diện. Vui lòng thử lại.", "UPLOAD_AVATAR_FAILED");

        // ── safe, collision-free object key: avatars/{userId}/{timestamp}_{guid}.{ext} ──
        var safeExt = extension.ToLowerInvariant();
        var objectKey = $"avatars/{userId}/{_clock.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{safeExt}";

        string? uploadedExternalFileId = null;
        try
        {
            using var uploadStream = new MemoryStream(buffer, writable: false);
            var upload = await _storage.UploadAsync(
                uploadStream, objectKey, request.ContentType!, _folders.AvatarFolderId, cancellationToken);
            uploadedExternalFileId = upload.ExternalFileId;

            var fileRow = new UploadedFile
            {
                StorageProvider = "GOOGLE_DRIVE",
                ObjectKey = objectKey,
                OriginalFilename = SanitizeFileName(request.OriginalFileName),
                MimeType = request.ContentType,
                FileSize = buffer.Length,
                ChecksumSha256 = checksumSha256,
                UploadedBy = userId,
                UploadedAt = _clock.UtcNow,
                ExternalFileId = upload.ExternalFileId,
                WebViewUrl = upload.WebViewUrl,
                DownloadUrl = upload.DownloadUrl,
                ThumbnailUrl = upload.ThumbnailUrl,
                FilePurpose = "USER_AVATAR",
            };
            _db.Files.Add(fileRow);
            await _db.SaveChangesAsync(cancellationToken); // assigns FileId

            user.AvatarUrl = $"/api/files/{fileRow.FileId}/content";
            user.UpdatedAt = _clock.UtcNow;
            user.UpdatedBy = userId;
            await _db.SaveChangesAsync(cancellationToken);

            return new UploadProfileAvatarResponse
            {
                FileId = (long)fileRow.FileId,
                AvatarUrl = user.AvatarUrl,
                WebViewUrl = upload.WebViewUrl,
                ThumbnailUrl = upload.ThumbnailUrl,
            };
        }
        catch
        {
            // The DB transaction cannot span Google Drive: if anything after the upload fails,
            // delete the orphaned Drive file so storage stays consistent.
            if (!string.IsNullOrWhiteSpace(uploadedExternalFileId))
            {
                try { await _storage.DeleteAsync(uploadedExternalFileId, cancellationToken); }
                catch { /* logged in the storage service; manual cleanup if even this fails */ }
            }
            throw;
        }
    }

    /// <summary>Reads at most <paramref name="maxBytes"/>+1 bytes so an oversize stream can be detected.</summary>
    private static async Task<byte[]> ReadBoundedAsync(Stream source, long maxBytes, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk.AsMemory(0, chunk.Length), ct)) > 0)
        {
            ms.Write(chunk, 0, read);
            if (ms.Length > maxBytes) break; // caller rejects on > maxBytes
        }
        return ms.ToArray();
    }

    /// <summary>Magic-byte sniff for JPEG / PNG / WEBP (RIFF....WEBP). SVG and other types fail here.</summary>
    private static bool LooksLikeAllowedImage(byte[] b)
    {
        // JPEG: FF D8 FF
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return true;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return true;

        // WEBP: "RIFF" .... "WEBP"
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
            && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
            return true;

        return false;
    }

    private static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "avatar";

        var name = Path.GetFileName(fileName.Trim());
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name.Length > 255 ? name[^255..] : name;
    }
}
