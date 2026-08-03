namespace PEMS.Application.Common.Storage;

/// <summary>
/// Why a stored file could not be read. One code per distinguishable cause, because "không tải được
/// tệp" is the same sentence for a file somebody deleted, a credential that was never granted access,
/// and a <c>files</c> row that never pointed at anything real — and those three need three different
/// people to fix them.
///
/// <para>
/// Honest limitation, worth knowing before trusting these to triage: Google Drive answers <c>404</c>
/// both for a file that is gone AND for a file the credential simply cannot see, because revealing the
/// difference would let a caller probe for the existence of other people's files. So
/// <see cref="FileNotFound"/> means "Drive will not show it to us" — not "it was deleted".
/// <see cref="FileForbidden"/> is the narrower, unambiguous case: Drive acknowledged the file and
/// refused the read (<c>403</c>), which is a permission/scope problem on our side of the connection.
/// </para>
/// </summary>
public static class StorageErrorCodes
{
    /// <summary>
    /// Drive returned <c>404</c>: deleted, trashed, or not visible to the configured credential.
    /// Ambiguous by Drive's design — see the type remarks before reporting this as "đã bị xoá".
    /// </summary>
    public const string FileNotFound = "STORAGE_FILE_NOT_FOUND";

    /// <summary>
    /// Drive returned <c>403</c>: the file exists and the credential is authenticated, but the read was
    /// refused — a missing share, an insufficient OAuth scope, or a quota/rate limit.
    /// </summary>
    public const string FileForbidden = "STORAGE_FILE_FORBIDDEN";

    /// <summary>
    /// Drive returned <c>401</c>: the access token was rejected. A configuration/token problem, not a
    /// fact about the file.
    /// </summary>
    public const string AuthFailed = "STORAGE_AUTH_FAILED";

    /// <summary>
    /// The <c>files</c> row cannot address any content: a <c>GOOGLE_DRIVE</c> row with no
    /// <c>external_file_id</c>, or a <c>LOCAL</c> row whose object key resolves outside the storage
    /// root. Nothing was asked of the provider — the record itself is the defect, so this is a data
    /// repair rather than an access problem.
    /// </summary>
    public const string FileReferenceInvalid = "STORAGE_FILE_REFERENCE_INVALID";

    /// <summary>Anything else — network failure, 5xx, an unparseable response.</summary>
    public const string Unavailable = "STORAGE_UNAVAILABLE";
}

/// <summary>
/// The outcome of asking whether a stored file's bytes can actually be read, as opposed to whether a
/// <c>files</c> row exists. Callers that must not build on an unreadable file (the setup-progress
/// email's mandatory report) branch on this; callers for whom the file is decorative (a partner logo)
/// log it and carry on.
/// </summary>
public enum StoredFileAvailability
{
    /// <summary>The bytes were read.</summary>
    Available,

    /// <summary>Provider says no such file — see <see cref="StorageErrorCodes.FileNotFound"/>.</summary>
    NotFound,

    /// <summary>Provider refused the read — see <see cref="StorageErrorCodes.FileForbidden"/>.</summary>
    Forbidden,

    /// <summary>The row does not address any content — see <see cref="StorageErrorCodes.FileReferenceInvalid"/>.</summary>
    ReferenceInvalid,

    /// <summary>Reachability could not be established (network, 5xx, timeout).</summary>
    Unavailable,
}
