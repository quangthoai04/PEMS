using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;

namespace PEMS.Infrastructure.FileStorage.GoogleDrive;

/// <summary>
/// Resolves a Google Drive folder id from a <see cref="FilePurpose"/> using the configured
/// <see cref="GoogleDriveOptions"/>. Purposes without a dedicated folder fall back to a sensible
/// shared folder (documents, then the root) so a new feature works before its folder is provisioned.
/// </summary>
public sealed class GoogleDriveFolderResolver : IFileStorageFolderResolver
{
    private readonly GoogleDriveOptions _options;

    public GoogleDriveFolderResolver(IOptions<GoogleDriveOptions> options) => _options = options.Value;

    public string ResolveFolderId(FilePurpose purpose)
    {
        var folderId = purpose switch
        {
            FilePurpose.UserAvatar => _options.AvatarFolderId,
            FilePurpose.GalleryImage => _options.GalleryFolderId,
            FilePurpose.GalleryVideo => _options.GalleryFolderId,
            FilePurpose.NewsImage => _options.NewsFolderId,
            FilePurpose.NewsAttachment => _options.NewsFolderId,
            FilePurpose.Document => _options.DocumentPartnerFolderId,
            FilePurpose.MinutesAttachment => _options.MinutesFolderId,
            FilePurpose.VisitRequestAttachment => _options.VisitRequestDocumentFolderId,
            FilePurpose.PartnerDocument => _options.DocumentPartnerFolderId,
            FilePurpose.LogisticsAttachment => _options.DocumentPartnerFolderId,
            FilePurpose.BusinessCard => _options.DocumentPartnerFolderId,
            _ => _options.DocumentPartnerFolderId,
        };

        // Fall back to the root folder so an un-provisioned purpose still has somewhere to land.
        if (string.IsNullOrWhiteSpace(folderId))
            folderId = _options.RootFolderId;

        if (string.IsNullOrWhiteSpace(folderId))
            throw new BusinessRuleException(
                $"Google Drive chưa được cấu hình thư mục cho mục đích {purpose}.",
                "GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED");

        return folderId!;
    }
}
