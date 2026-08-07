using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Storage;
using PEMS.Application.Emails.Common;
using PEMS.Application.Files.Common;
using PEMS.Application.Files.Queries.GetFileContent;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.FileStorage;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Viewing and re-downloading an attachment — what the reader is told when it does not work.
///
/// <para>
/// The authorization matrix (who may read which file) is covered by
/// <see cref="FileDownloadAuthorizationTests"/> and is deliberately not repeated here. What this suite
/// asserts is the OTHER half, which the preview button made visible for the first time: a refusal has
/// to say WHICH refusal it is. "Không tải được tệp" was the same sentence for a file the user has no
/// claim on, a file the storage credential cannot read, a row that never addressed anything, and a
/// network blip — four situations needing four different people to act.
/// </para>
/// <para>
/// The distinction that matters most is the first one. A caller refused by
/// <see cref="FileAccessAuthorizationService"/> must never be reported the same way as a caller we
/// accepted but could not serve, or an operator reading the logs will go looking for a permission bug
/// in the product when the actual problem is a Drive share, and vice versa.
/// </para>
/// </summary>
public sealed class FilePreviewDownloadTests : IDisposable
{
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "pems-preview-" + Guid.NewGuid().ToString("N"));

    private const ulong Base = 991_700;
    private const ulong CampusId = Base + 1;
    private const ulong IcDeptId = Base + 2;
    private const ulong OwnerA = Base + 10;
    private const ulong OutsiderB = Base + 11;

    private const string DriveExternalId = "preview-drive-1a2b3c-secret-id";

    private static string Mail(ulong userId) => $"preview-{userId}@partner.example.com";

    public void Dispose()
    {
        try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true); }
        catch (IOException) { /* a temp dir left behind must never fail a test run */ }
    }

    // ── Rig ─────────────────────────────────────────────────────────────────

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId { get; init; }
        public string? Email { get; init; }
        public ulong? RoleId => null;
        public string? RoleCode { get; init; }
        public string? SubRole => null;
        public ulong? PrimaryCampusId => CampusId;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class NoHttpClients : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    /// <summary>
    /// A Drive whose read fails exactly the way the test is about. Every other member throws, so a test
    /// that accidentally routed somewhere else fails loudly instead of passing for the wrong reason.
    /// </summary>
    private sealed class ScriptedDrive : IGoogleDriveStorageService
    {
        private readonly Func<Stream>? _onDownload;

        public ScriptedDrive(Func<Stream>? onDownload = null) => _onDownload = onDownload;

        /// <summary>The read the provider itself refused / could not answer.</summary>
        public static ScriptedDrive Failing(Exception failure) => new(() => throw failure);

        public Task<Stream> DownloadAsync(string externalFileId, CancellationToken ct = default)
            => Task.FromResult((_onDownload ?? throw new NotSupportedException())());

        public Task<GoogleDriveUploadResult> UploadAvatarAsync(
            byte[] content, string driveFileName, string contentType, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<GoogleDriveUploadResult> UploadFileAsync(
            byte[] content, string driveFileName, string contentType, string? folderId = null,
            CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<GoogleDriveDownloadResult> DownloadRangeAsync(
            string externalFileId, long? from, long? to, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task DeleteAsync(string externalFileId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<GoogleDriveFolderResult> EnsureChildFolderAsync(
            string folderName, string parentFolderId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<string> CheckConnectionAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private static ICurrentUserService Viewer(ulong id)
        => new FakeCurrentUser { UserId = id, Email = Mail(id), RoleCode = "STAFF" };

    private LocalFileStorageService Storage() => new(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:LocalRoot"] = _storageRoot })
            .Build(),
        new NoHttpClients(),
        new NoServices(),
        NullLogger<LocalFileStorageService>.Instance);

    private GetFileContentQueryHandler Handler(
        ApplicationDbContext db, ICurrentUserService user, IGoogleDriveStorageService? drive = null)
        => new(db, user, Storage(), drive ?? new ScriptedDrive(),
            new FileAccessAuthorizationService(db, user, new SentEmailObjectScope(db, user)));

    private Task<FileContentDto> Read(
        ApplicationDbContext db, ICurrentUserService user, ulong fileId,
        IGoogleDriveStorageService? drive = null)
        => Handler(db, user, drive).Handle(new GetFileContentQuery(fileId), CancellationToken.None);

    // ── Seed ────────────────────────────────────────────────────────────────

    private static async Task SeedWorldAsync(ApplicationDbContext db)
    {
        await CleanupRowsAsync(db);

        var roles = await db.Database.SqlQueryRaw<RoleRow>(
            "SELECT role_id AS RoleId, role_code AS RoleCode FROM roles").ToListAsync();
        var staffRole = roles.First(r => r.RoleCode == "STAFF").RoleId;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, status) VALUES ({0}, 'PRV', {1}, 'ACTIVE')",
            CampusId, "PEMS Preview Campus");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'IC', 'ACTIVE')",
            IcDeptId, CampusId, "PEMS Preview IC");

        async Task User(ulong id, string name)
            => await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, "
                + $"department_id, status) VALUES ({id}, {{0}}, {{1}}, {staffRole}, 'STAFF', "
                + $"{CampusId}, {IcDeptId}, 'ACTIVE')",
                name, Mail(id));

        await User(OwnerA, "Preview A");
        await User(OutsiderB, "Preview B");
    }

    /// <summary>
    /// A real file on disk plus its <c>files</c> row. Nothing references it, so the uploader — and only
    /// the uploader — may read it. That keeps these tests about the STORAGE outcome rather than about
    /// re-deriving the access matrix.
    /// </summary>
    private async Task<ulong> SeedLocalFileAsync(
        ApplicationDbContext db, string originalName = "bao-cao.pdf")
    {
        var objectKey = $"preview/{Guid.NewGuid():N}.pdf";
        var fullPath = Path.Combine(_storageRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 });

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO files (storage_provider, object_key, original_filename, mime_type, file_size, "
            + $"uploaded_by, uploaded_at, file_purpose) VALUES ('LOCAL', {{0}}, {{1}}, 'application/pdf', "
            + $"6, {OwnerA}, NOW(), '{FilePurposeDbValues.Other}')",
            objectKey, originalName);

        return await db.Files.AsNoTracking()
            .Where(f => f.ObjectKey == objectKey).Select(f => f.FileId).FirstAsync();
    }

    /// <summary>A Drive-backed row. <paramref name="externalId"/> null models a row addressing nothing.</summary>
    private static async Task<ulong> SeedDriveFileAsync(ApplicationDbContext db, string? externalId)
    {
        var objectKey = "preview/" + Guid.NewGuid().ToString("N") + ".pdf";

        // A literal NULL rather than a parameter: EF's raw-SQL builder has no store mapping for DBNull.
        if (externalId is null)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO files (storage_provider, object_key, original_filename, mime_type, file_size, "
                + "external_file_id, uploaded_by, uploaded_at, file_purpose) "
                + $"VALUES ('GOOGLE_DRIVE', {{0}}, 'tai-lieu.pdf', 'application/pdf', 6, NULL, {OwnerA}, "
                + $"NOW(), '{FilePurposeDbValues.Other}')",
                objectKey);
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO files (storage_provider, object_key, original_filename, mime_type, file_size, "
                + "external_file_id, uploaded_by, uploaded_at, file_purpose) "
                + $"VALUES ('GOOGLE_DRIVE', {{0}}, 'tai-lieu.pdf', 'application/pdf', 6, {{1}}, {OwnerA}, "
                + $"NOW(), '{FilePurposeDbValues.Other}')",
                objectKey, externalId);
        }

        return await db.Files.AsNoTracking()
            .Where(f => f.ObjectKey == objectKey).Select(f => f.FileId).FirstAsync();
    }

    private sealed record RoleRow(ulong RoleId, string RoleCode);

    /// <summary>
    /// Puts the fixture's id band back to empty, children first.
    ///
    /// <para>
    /// This used to delete the four rows below directly, which only worked while nothing referenced them.
    /// It is the first statement of <see cref="SeedWorldAsync"/>, so on the run where something DID
    /// reference them the whole class died in setup without reaching a line of product code — nine tests
    /// reporting a foreign-key error instead of the thing they were written to check. Which referrer it
    /// tripped over varied by run; enumerating them by hand only moved the failure along.
    /// </para>
    /// <para>
    /// Order still matters between the roots. <c>files.uploaded_by</c> references <c>users</c> ON DELETE
    /// SET NULL, so removing the users first would blank the column the files root identifies its rows by
    /// and leave them behind unowned.
    /// </para>
    /// </summary>
    private static Task CleanupRowsAsync(ApplicationDbContext db)
        => FixtureCleanup.For(db)
            .Root("files", $"uploaded_by BETWEEN {Base} AND {Base + 100}")
            .Root("users", $"user_id BETWEEN {Base} AND {Base + 100}")
            .Root("departments", $"department_id = {IcDeptId}")
            .Root("campuses", $"campus_id = {CampusId}")
            .RunAsync();

    // ── A. The happy path, and the refusal that is about the USER ────────────

    [Fact]
    public async Task An_entitled_reader_gets_the_bytes_and_a_filename_safe_to_put_in_a_header()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        // A stored name carrying a path and a header break — the two things a filename must never take
        // into Content-Disposition.
        var fileId = await SeedLocalFileAsync(db, "../../etc/bao cáo\r\nSet-Cookie: x=1.pdf");

        var result = await Read(db, Viewer(OwnerA), fileId);

        Assert.NotEmpty(result.Content);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.DoesNotContain('\r', result.FileName);
        Assert.DoesNotContain('\n', result.FileName);
        Assert.DoesNotContain("/", result.FileName);
        Assert.DoesNotContain("..", result.FileName);
        // Vietnamese survives: the name is sanitised, not stripped down to ASCII.
        Assert.Contains("cáo", result.FileName);

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_reader_with_no_claim_is_refused_and_the_refusal_is_about_them_not_the_storage()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedLocalFileAsync(db);

        // ForbiddenException, NOT a storage code. Confusing the two sends every investigation of a
        // permission complaint to whoever administers Drive, and vice versa.
        var error = await Assert.ThrowsAsync<ForbiddenException>(
            () => Read(db, Viewer(OutsiderB), fileId));

        Assert.DoesNotContain("STORAGE_", error.Message ?? string.Empty);

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_file_id_that_does_not_exist_is_reported_as_missing()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() => Read(db, Viewer(OwnerA), 999_999_999));

        await CleanupRowsAsync(db);
    }

    // ── B. Refusals that are about the STORAGE, one code each ────────────────

    [Fact]
    public async Task Storage_refusing_the_read_is_a_storage_permission_problem_not_a_user_one()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedDriveFileAsync(db, DriveExternalId);
        var drive = ScriptedDrive.Failing(new BusinessRuleException(
            "Google Drive từ chối quyền đọc tệp này.", StorageErrorCodes.FileForbidden));

        // The USER was allowed through — this is the credential being refused, and it says so.
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Read(db, Viewer(OwnerA), fileId, drive));

        Assert.Equal(StorageErrorCodes.FileForbidden, error.ErrorCode);

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_rejected_storage_credential_is_reported_as_an_authentication_failure()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedDriveFileAsync(db, DriveExternalId);
        var drive = ScriptedDrive.Failing(new BusinessRuleException(
            "Kết nối Google Drive không còn hợp lệ.", StorageErrorCodes.AuthFailed));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Read(db, Viewer(OwnerA), fileId, drive));

        Assert.Equal(StorageErrorCodes.AuthFailed, error.ErrorCode);

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_row_that_addresses_nothing_is_reported_as_an_invalid_reference()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        // A Drive row with no external id. Nothing is asked of the provider: the RECORD is the defect,
        // so this is a data repair, not an access problem and not an outage.
        var driveFileId = await SeedDriveFileAsync(db, externalId: null);

        var driveError = await Assert.ThrowsAsync<NotFoundException>(
            () => Read(db, Viewer(OwnerA), driveFileId, ScriptedDrive.Failing(
                new InvalidOperationException("the provider must never be called for this row"))));
        Assert.Equal(StorageErrorCodes.FileReferenceInvalid, driveError.ErrorCode);

        // The same defect on the local side: a row with no object key.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO files (storage_provider, object_key, original_filename, mime_type, file_size, "
            + $"uploaded_by, uploaded_at, file_purpose) VALUES ('LOCAL', '', 'trong.pdf', "
            + $"'application/pdf', 6, {OwnerA}, NOW(), '{FilePurposeDbValues.Other}')");
        var localFileId = await db.Files.AsNoTracking()
            .Where(f => f.UploadedBy == OwnerA && f.ObjectKey == "")
            .Select(f => f.FileId).FirstAsync();

        var localError = await Assert.ThrowsAsync<NotFoundException>(
            () => Read(db, Viewer(OwnerA), localFileId));
        Assert.Equal(StorageErrorCodes.FileReferenceInvalid, localError.ErrorCode);

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task A_transient_storage_failure_is_reported_as_unavailable_rather_than_as_a_missing_file()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedDriveFileAsync(db, DriveExternalId);

        // A raw transport failure — nothing the provider classified for us.
        var drive = ScriptedDrive.Failing(new HttpRequestException("Connection reset by peer"));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Read(db, Viewer(OwnerA), fileId, drive));

        Assert.Equal(StorageErrorCodes.Unavailable, error.ErrorCode);

        await CleanupRowsAsync(db);
    }

    [Fact]
    public async Task The_bytes_being_gone_is_not_reported_as_the_row_being_broken()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        // A well-formed LOCAL row whose file is not on disk. The record is fine; the content is gone.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO files (storage_provider, object_key, original_filename, mime_type, file_size, "
            + $"uploaded_by, uploaded_at, file_purpose) VALUES ('LOCAL', {{0}}, 'khong-co.pdf', "
            + $"'application/pdf', 6, {OwnerA}, NOW(), '{FilePurposeDbValues.Other}')",
            "preview/" + Guid.NewGuid().ToString("N") + ".pdf");
        var ghostId = await db.Files.AsNoTracking()
            .Where(f => f.UploadedBy == OwnerA).OrderByDescending(f => f.FileId)
            .Select(f => f.FileId).FirstAsync();

        var error = await Assert.ThrowsAsync<NotFoundException>(() => Read(db, Viewer(OwnerA), ghostId));

        Assert.Equal(StorageErrorCodes.FileNotFound, error.ErrorCode);
        Assert.DoesNotContain(_storageRoot, error.Message ?? string.Empty);

        await CleanupRowsAsync(db);
    }

    // ── C. What a failure is allowed to say ──────────────────────────────────

    [Fact]
    public async Task A_storage_failure_names_neither_the_provider_id_nor_the_raw_exception()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        await SeedWorldAsync(db);

        var fileId = await SeedDriveFileAsync(db, DriveExternalId);
        const string rawText = "GaxiosError: quotaExceeded for serviceAccount pems@iam.gserviceaccount.com";
        var drive = ScriptedDrive.Failing(new HttpRequestException(rawText));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Read(db, Viewer(OwnerA), fileId, drive));

        var message = error.Message ?? string.Empty;
        Assert.DoesNotContain(DriveExternalId, message);       // the provider's file id
        Assert.DoesNotContain("gserviceaccount", message);     // the credential
        Assert.DoesNotContain("GaxiosError", message);         // the raw exception
        Assert.DoesNotContain("quotaExceeded", message);

        await CleanupRowsAsync(db);
    }

    // ── D. The header/rendering rules, as plain functions ────────────────────

    [Theory]
    [InlineData("../../../etc/passwd", "passwd")]
    [InlineData("C:\\Users\\a\\bao-cao.pdf", "bao-cao.pdf")]
    [InlineData("bao-cao\r\nSet-Cookie: a=b.pdf", "bao-caoSet-Cookie: a=b.pdf")]
    [InlineData("say \"hi\".txt", "say hi.txt")]
    [InlineData("   ", "file")]
    [InlineData(null, "file")]
    [InlineData("...", "file")]
    [InlineData("Báo cáo tiếp khách.pdf", "Báo cáo tiếp khách.pdf")]
    public void A_filename_is_reduced_to_a_leaf_with_no_control_characters(string? stored, string expected)
        => Assert.Equal(expected, FileResponseSafety.SafeFileName(stored));

    [Theory]
    [InlineData("application/pdf", "application/pdf")]
    [InlineData("image/png", "image/png")]
    [InlineData("text/plain; charset=utf-8", "text/plain")]
    // A document the browser would execute in our origin is never NAMED as one on the inline route.
    [InlineData("text/html", "application/octet-stream")]
    [InlineData("text/html; charset=utf-8", "application/octet-stream")]
    [InlineData("image/svg+xml", "application/octet-stream")]
    [InlineData("application/xhtml+xml", "application/octet-stream")]
    [InlineData("", "application/octet-stream")]
    [InlineData(null, "application/octet-stream")]
    public void An_inline_content_type_never_names_something_the_browser_would_execute(
        string? stored, string expected)
        => Assert.Equal(expected, FileResponseSafety.SafeInlineContentType(stored));
}
