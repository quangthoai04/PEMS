using PEMS.Application.Common.Files;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// The one entry point every business upload should use. Given a stream and a <see cref="FilePurpose"/>
/// it validates the file, computes its checksum, builds the object key, resolves the storage folder,
/// uploads the bytes, inserts the <c>files</c> row, and on a DB failure rolls back the storage upload.
/// Callers only deal with their own entity afterwards (linking <see cref="UploadedFileDto.FileId"/>).
/// </summary>
public interface IFileUploadService
{
    Task<UploadedFileDto> UploadBusinessFileAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        long fileSize,
        FilePurpose purpose,
        long uploadedBy,
        CancellationToken cancellationToken);

    /// <summary>
    /// Same pipeline, but uploads into an explicit Drive folder instead of the purpose's root
    /// (e.g. visit photos land in <c>VR-{visit_request_id}/{campus_code}</c> below the configured
    /// root). Validation, checksum, object key, <c>files</c> row and rollback are identical.
    /// </summary>
    Task<UploadedFileDto> UploadBusinessFileAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        long fileSize,
        FilePurpose purpose,
        long uploadedBy,
        string targetFolderId,
        CancellationToken cancellationToken);
}
