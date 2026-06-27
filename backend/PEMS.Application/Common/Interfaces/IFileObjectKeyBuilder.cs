using PEMS.Application.Common.Files;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Builds the opaque, collision-free <c>files.object_key</c> for a new upload. The original filename
/// is never used directly as the key (only its sanitized extension), so the key is safe from path
/// traversal and name collisions.
/// </summary>
public interface IFileObjectKeyBuilder
{
    string Build(FilePurpose purpose, long uploadedBy, string originalFileName);
}
