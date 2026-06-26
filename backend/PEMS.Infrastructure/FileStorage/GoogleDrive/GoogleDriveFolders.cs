using Microsoft.Extensions.Options;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.FileStorage.GoogleDrive;

/// <summary>Resolves <see cref="IFileStorageFolders"/> from the bound Google Drive config.</summary>
public sealed class GoogleDriveFolders : IFileStorageFolders
{
    private readonly GoogleDriveOptions _options;

    public GoogleDriveFolders(IOptions<GoogleDriveOptions> options) => _options = options.Value;

    public string AvatarFolderId => _options.AvatarFolderId;
}
