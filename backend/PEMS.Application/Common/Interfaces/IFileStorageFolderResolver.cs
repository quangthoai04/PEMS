using PEMS.Application.Common.Files;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Maps a <see cref="FilePurpose"/> to the storage folder id its files belong in, so business
/// handlers never hard-code a Google Drive folder id. Implemented in Infrastructure against
/// <c>GoogleDriveOptions</c>. Throws <c>GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED</c> when the resolved
/// folder is missing from config.
/// </summary>
public interface IFileStorageFolderResolver
{
    string ResolveFolderId(FilePurpose purpose);
}
