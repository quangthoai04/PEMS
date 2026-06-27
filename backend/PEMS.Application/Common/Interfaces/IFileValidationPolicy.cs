using PEMS.Application.Common.Files;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Supplies the <see cref="FileValidationRule"/> for a given <see cref="FilePurpose"/>. Centralizes
/// "what files are allowed where" so avatars, gallery images and documents each get their own limits
/// instead of one rule for everything.
/// </summary>
public interface IFileValidationPolicy
{
    FileValidationRule GetRule(FilePurpose purpose);
}
