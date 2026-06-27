namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Exposes the well-known destination folder ids of the configured storage provider so
/// application handlers can pick a target without depending on provider-specific config.
/// </summary>
public interface IFileStorageFolders
{
    /// <summary>Folder that holds user avatars.</summary>
    string AvatarFolderId { get; }
}
