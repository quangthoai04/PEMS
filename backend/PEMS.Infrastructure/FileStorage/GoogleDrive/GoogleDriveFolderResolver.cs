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

            FilePurpose.GalleryAreaCover or FilePurpose.GalleryAreaCoverVideo => _options.GalleryAreaFolderId,
            FilePurpose.GalleryLocationCover => _options.GalleryLocationFolderId,
            FilePurpose.GalleryItemImage or FilePurpose.GalleryItemVideo => _options.GalleryItemFolderId,
            FilePurpose.GalleryDelegationImage or FilePurpose.GalleryDelegationVideo
                => _options.GalleryDelegationFolderId,
            FilePurpose.GalleryAudio => _options.GalleryAudioFolderId,

            // Legacy purposes from before per-folder routing: prefer the item folder so nothing new
            // lands in the shared gallery folder, but keep it as a fallback for old call sites.
            FilePurpose.GalleryImage or FilePurpose.GalleryVideo =>
                !string.IsNullOrWhiteSpace(_options.GalleryItemFolderId)
                    ? _options.GalleryItemFolderId
                    : _options.GalleryFolderId,

            FilePurpose.NewsImage => _options.NewsFolderId,
            FilePurpose.NewsAttachment => _options.NewsFolderId,
            FilePurpose.Document => _options.DocumentPartnerFolderId,
            FilePurpose.MinutesAttachment => _options.MinutesFolderId,
            FilePurpose.VisitRequestAttachment => _options.VisitRequestDocumentFolderId,
            FilePurpose.VisitRequestPhoto => _options.VisitRequestPhotoFolderId,
            FilePurpose.PartnerDocument => _options.DocumentPartnerFolderId,
            // Logo/cover land in the same partner root as PARTNER_DOCUMENT; the upload handler nests
            // them one level deeper under a per-partner-code subfolder (see UploadPartnerImageCommandHandler).
            FilePurpose.PartnerLogo or FilePurpose.PartnerCover => _options.DocumentPartnerFolderId,
            FilePurpose.LogisticsAttachment => _options.DocumentPartnerFolderId,
            FilePurpose.BusinessCard => _options.DocumentPartnerFolderId,
            _ => _options.DocumentPartnerFolderId,
        };

        // The dedicated gallery folders must never fall back silently — a missing id would quietly
        // put covers/items/delegation media in the wrong folder, defeating the per-folder routing.
        // Visit request photos are equally strict: their VR-{id}/{campus} tree hangs off the
        // configured root, so landing anywhere else would break the mandated Drive structure.
        var isDedicatedGalleryPurpose = purpose is
            FilePurpose.GalleryAreaCover or FilePurpose.GalleryAreaCoverVideo
            or FilePurpose.GalleryLocationCover
            or FilePurpose.GalleryItemImage or FilePurpose.GalleryItemVideo
            or FilePurpose.GalleryDelegationImage or FilePurpose.GalleryDelegationVideo
            or FilePurpose.GalleryAudio
            or FilePurpose.VisitRequestPhoto;

        // Fall back to the root folder so an un-provisioned purpose still has somewhere to land.
        if (string.IsNullOrWhiteSpace(folderId) && !isDedicatedGalleryPurpose)
            folderId = _options.RootFolderId;

        if (string.IsNullOrWhiteSpace(folderId))
            throw new BusinessRuleException(
                $"Google Drive chưa được cấu hình thư mục cho mục đích {purpose}.",
                "GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED");

        return folderId!;
    }
}
